-- ============================================================
-- 0019_sync_preparacion.sql
-- Prepara el esquema para sincronizar instalaciones locales (modo Desktop, SQL Server Express,
-- offline-first) con la base central en Azure SQL. No cambia ningún comportamiento actual: solo
-- agrega columnas y tablas que la sincronización usará cuando se habilite (Sync:Enabled).
--
-- 1. SyncId (UNIQUEIDENTIFIER): identidad GLOBAL y estable de cada fila entre nodos. Los Id IDENTITY
--    locales chocan entre sucursales (la Venta 15 de la PC A no es la Venta 15 de la PC B); el
--    SyncId no. NEWSEQUENTIALID() evita la fragmentación de índices de un GUID aleatorio.
-- 2. SyncVersion (ROWVERSION): SQL Server la incrementa sola en cada INSERT/UPDATE, sin tocar los
--    stored procedures. Permite pedir "todo lo que cambió desde la versión X" (sync incremental).
-- 3. market.NodoSync: identidad de ESTA instalación y marca de hasta dónde se sincronizó.
-- 4. market.SyncOutbox: cola de cambios pendientes de enviar a la nube (patrón outbox), para
--    documentos transaccionales que deben llegar exactamente una vez (ventas, compras, caja...).
--
-- Las entidades Dapper no mapean estas columnas: los SELECT * existentes las ignoran sin romper.
-- Idempotente: cada ALTER comprueba si la columna ya existe.
-- ============================================================

DECLARE @tablas TABLE (Nombre SYSNAME PRIMARY KEY);
INSERT INTO @tablas (Nombre) VALUES
    ('Empresa'), ('Sucursal'), ('RolCatalogo'), ('Usuario'),
    ('Categoria'), ('Proveedor'), ('Cliente'), ('Producto'), ('TipoPrecio'),
    ('Inventario'), ('MovimientoInventario'),
    ('Caja'), ('MovimientoCaja'),
    ('Venta'), ('DetalleVenta'), ('PagoVenta'),
    ('Compra'), ('DetalleCompra'),
    ('Credito'), ('AbonoCredito');

DECLARE @tabla SYSNAME, @sql NVARCHAR(MAX);
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR SELECT Nombre FROM @tablas;
OPEN cur;
FETCH NEXT FROM cur INTO @tabla;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF COL_LENGTH(N'market.' + @tabla, N'SyncId') IS NULL
    BEGIN
        SET @sql = N'ALTER TABLE market.' + QUOTENAME(@tabla)
                 + N' ADD SyncId UNIQUEIDENTIFIER NOT NULL CONSTRAINT ' + QUOTENAME(N'DF_' + @tabla + N'_SyncId')
                 + N' DEFAULT NEWSEQUENTIALID();';
        EXEC sp_executesql @sql;

        SET @sql = N'CREATE UNIQUE INDEX ' + QUOTENAME(N'UX_' + @tabla + N'_SyncId')
                 + N' ON market.' + QUOTENAME(@tabla) + N' (SyncId);';
        EXEC sp_executesql @sql;
    END

    IF COL_LENGTH(N'market.' + @tabla, N'SyncVersion') IS NULL
    BEGIN
        SET @sql = N'ALTER TABLE market.' + QUOTENAME(@tabla) + N' ADD SyncVersion ROWVERSION;';
        EXEC sp_executesql @sql;
    END

    FETCH NEXT FROM cur INTO @tabla;
END
CLOSE cur;
DEALLOCATE cur;
GO

-- Identidad de la instalación. Una sola fila por base local (la central en Azure no la necesita).
IF OBJECT_ID(N'market.NodoSync', N'U') IS NULL
CREATE TABLE market.NodoSync (
    NodoId                      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_NodoSync PRIMARY KEY DEFAULT NEWID(),
    Nombre                      NVARCHAR(100)    NOT NULL DEFAULT HOST_NAME(),
    EmpresaId                   INT              NULL,
    SucursalId                  INT              NULL,
    FechaCreacionUtc            DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    UltimaSyncUtc               DATETIME2        NULL,
    -- Última SyncVersion local ya enviada (PUSH incremental) y última versión central recibida (PULL).
    UltimaVersionEnviada        BINARY(8)        NULL,
    UltimaVersionRecibida       BINARY(8)        NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM market.NodoSync)
    INSERT INTO market.NodoSync (Nombre) VALUES (HOST_NAME());
GO

IF OBJECT_ID(N'market.SyncOutbox', N'U') IS NULL
CREATE TABLE market.SyncOutbox (
    Id              BIGINT IDENTITY     NOT NULL CONSTRAINT PK_SyncOutbox PRIMARY KEY,
    EmpresaId       INT                 NOT NULL,
    Entidad         NVARCHAR(60)        NOT NULL,   -- 'Venta', 'Compra', 'Caja', 'AbonoCredito'...
    SyncId          UNIQUEIDENTIFIER    NOT NULL,   -- SyncId de la fila afectada
    Operacion       CHAR(1)             NOT NULL CONSTRAINT CK_SyncOutbox_Operacion CHECK (Operacion IN ('I','U','D')),
    PayloadJson     NVARCHAR(MAX)       NULL,       -- snapshot del documento (FOR JSON) al momento del cambio
    CreadoUtc       DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
    EnviadoUtc      DATETIME2           NULL,
    Intentos        INT                 NOT NULL DEFAULT 0,
    UltimoError     NVARCHAR(1000)      NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SyncOutbox_Pendientes')
    CREATE INDEX IX_SyncOutbox_Pendientes ON market.SyncOutbox (EnviadoUtc, Id) INCLUDE (Entidad, SyncId);
GO

-- Encola un cambio para la nube. Pensado para llamarse desde los procedures de documentos
-- (usp_Venta_Crear, usp_Compra_Crear, usp_Caja_Cerrar, ...) cuando se habilite la sincronización.
CREATE OR ALTER PROCEDURE market.usp_SyncOutbox_Encolar
    @EmpresaId INT, @Entidad NVARCHAR(60), @SyncId UNIQUEIDENTIFIER, @Operacion CHAR(1), @PayloadJson NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO market.SyncOutbox (EmpresaId, Entidad, SyncId, Operacion, PayloadJson)
    VALUES (@EmpresaId, @Entidad, @SyncId, @Operacion, @PayloadJson);
END
GO

CREATE OR ALTER PROCEDURE market.usp_SyncOutbox_ObtenerPendientes
    @TamanoLote INT = 200
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@TamanoLote) Id, EmpresaId, Entidad, SyncId, Operacion, PayloadJson, CreadoUtc, Intentos
    FROM market.SyncOutbox
    WHERE EnviadoUtc IS NULL
    ORDER BY Id;
END
GO

CREATE OR ALTER PROCEDURE market.usp_SyncOutbox_MarcarEnviado
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.SyncOutbox SET EnviadoUtc = SYSUTCDATETIME(), UltimoError = NULL WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE market.usp_SyncOutbox_MarcarError
    @Id BIGINT, @Error NVARCHAR(1000)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.SyncOutbox SET Intentos = Intentos + 1, UltimoError = @Error WHERE Id = @Id;
END
GO
