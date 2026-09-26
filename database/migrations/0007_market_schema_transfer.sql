-- ============================================================
-- 0007_market_schema_transfer.sql
-- Mueve todas las tablas de dbo al esquema market (por seguridad: separa los objetos de aplicación
-- del esquema dbo por defecto, de forma que los permisos de BD puedan otorgarse de forma acotada —
-- ver database/schema/market/13_security_users.sql). No se recrean las tablas (se perdería la data):
-- ALTER SCHEMA ... TRANSFER solo cambia el esquema dueño, conservando filas, índices, FKs e identity.
--
-- Idempotente: cada TRANSFER solo se ejecuta si la tabla todavía está en dbo (una base creada desde
-- cero con database/schema/market/01_schema_and_tables.sql ya nace en market y no tiene nada que mover).
-- ============================================================

IF SCHEMA_ID('market') IS NULL
    EXEC('CREATE SCHEMA market');
GO

DECLARE @tabla sysname, @sql NVARCHAR(400);
DECLARE tablas CURSOR LOCAL FAST_FORWARD FOR
    SELECT value FROM (VALUES
        ('Empresa'),('Sucursal'),('RolCatalogo'),('Usuario'),
        ('Categoria'),('Proveedor'),('Cliente'),
        ('Producto'),('TipoPrecio'),
        ('Inventario'),('MovimientoInventario'),
        ('Caja'),('MovimientoCaja'),
        ('Venta'),('DetalleVenta'),('PagoVenta'),
        ('Compra'),('DetalleCompra'),
        ('Permiso'),('RolPermiso'),
        ('RefreshToken'),('EventoSeguridad'),
        ('Credito'),('AbonoCredito')
    ) AS t(value);

OPEN tablas;
FETCH NEXT FROM tablas INTO @tabla;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
               WHERE s.name = 'dbo' AND t.name = @tabla)
    BEGIN
        SET @sql = N'ALTER SCHEMA market TRANSFER dbo.[' + @tabla + N']';
        EXEC (@sql);
    END
    FETCH NEXT FROM tablas INTO @tabla;
END
CLOSE tablas;
DEALLOCATE tablas;
GO
