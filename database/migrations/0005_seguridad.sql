-- ============================================================
-- 0005_seguridad.sql
-- Módulo de seguridad: roles por empresa, catálogo de permisos, refresh tokens, bloqueo de cuentas
-- y auditoría de eventos de seguridad.
--
--  * RolCatalogo deja de ser global: cada empresa tiene sus propios roles (los 3 base como
--    EsSistema=1, más los que el admin cree desde la UI).
--  * Permiso es un catálogo global; PermisoCatalogSync (Infrastructure) lo mantiene al día desde
--    Domain/Security/Permisos.cs en cada arranque, por lo que aquí solo se siembra el conjunto inicial.
--  * El rol 'admin' NO lleva filas en RolPermiso: se resuelve como "todos los permisos del catálogo".
-- ============================================================

-- ------------------------------------------------------------
-- 1. RolCatalogo por empresa
-- ------------------------------------------------------------
ALTER TABLE RolCatalogo ADD
    EmpresaId               INT             NULL,
    Descripcion             NVARCHAR(250)   NULL,
    EsSistema               BIT             NOT NULL CONSTRAINT DF_RolCatalogo_EsSistema DEFAULT 0,
    Estado                  CHAR(1)         NOT NULL CONSTRAINT DF_RolCatalogo_Estado DEFAULT 'A',
    CreadoPorUsuarioId      INT             NULL,
    FechaCreacion           DATETIME2       NOT NULL CONSTRAINT DF_RolCatalogo_FechaCreacion DEFAULT SYSUTCDATETIME(),
    ModificadoPorUsuarioId  INT             NULL,
    FechaModificacion       DATETIME2       NULL;
GO

-- El UNIQUE original sobre Codigo tiene nombre autogenerado: se busca en el catálogo del sistema.
DECLARE @uq sysname = (
    SELECT kc.name
    FROM sys.key_constraints kc
    JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    WHERE kc.parent_object_id = OBJECT_ID('RolCatalogo') AND kc.type = 'UQ' AND c.name = 'Codigo');
IF @uq IS NOT NULL EXEC('ALTER TABLE RolCatalogo DROP CONSTRAINT [' + @uq + ']');
GO

-- Los códigos de roles personalizados se derivan del nombre (slug), por eso se amplía la columna.
ALTER TABLE RolCatalogo ALTER COLUMN Codigo NVARCHAR(50) NOT NULL;
ALTER TABLE RolCatalogo ALTER COLUMN Nombre NVARCHAR(100) NOT NULL;
GO

-- Copia los roles globales existentes a cada empresa, repunta los usuarios y elimina los globales.
INSERT INTO RolCatalogo (Codigo, Nombre, EmpresaId, EsSistema, Descripcion)
SELECT r.Codigo, r.Nombre, e.Id, 1,
       CASE r.Codigo
            WHEN 'admin'      THEN N'Acceso total al sistema. No editable.'
            WHEN 'supervisor' THEN N'Gestión operativa: catálogo, inventario, compras y anulaciones.'
            WHEN 'cajero'     THEN N'Punto de venta y operación de caja.'
       END
FROM RolCatalogo r
CROSS JOIN Empresa e
WHERE r.EmpresaId IS NULL;

UPDATE u
SET u.RolId = n.Id
FROM Usuario u
JOIN RolCatalogo o ON o.Id = u.RolId AND o.EmpresaId IS NULL
JOIN RolCatalogo n ON n.EmpresaId = u.EmpresaId AND n.Codigo = o.Codigo;

DELETE FROM RolCatalogo WHERE EmpresaId IS NULL;
GO

ALTER TABLE RolCatalogo ALTER COLUMN EmpresaId INT NOT NULL;
ALTER TABLE RolCatalogo ADD CONSTRAINT FK_RolCatalogo_Empresa FOREIGN KEY (EmpresaId) REFERENCES Empresa(Id) ON DELETE NO ACTION;
CREATE UNIQUE INDEX UX_RolCatalogo_Empresa_Codigo ON RolCatalogo(EmpresaId, Codigo);
GO

-- ------------------------------------------------------------
-- 2. Permisos
-- ------------------------------------------------------------
CREATE TABLE Permiso (
    Id          INT IDENTITY PRIMARY KEY,
    Codigo      NVARCHAR(60)    NOT NULL,
    Modulo      NVARCHAR(40)    NOT NULL,
    Nombre      NVARCHAR(100)   NOT NULL,
    Descripcion NVARCHAR(250)   NULL
);
CREATE UNIQUE INDEX UX_Permiso_Codigo ON Permiso(Codigo);

CREATE TABLE RolPermiso (
    RolId       INT NOT NULL,
    PermisoId   INT NOT NULL,
    CONSTRAINT PK_RolPermiso PRIMARY KEY (RolId, PermisoId),
    CONSTRAINT FK_RolPermiso_Rol     FOREIGN KEY (RolId)     REFERENCES RolCatalogo(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_RolPermiso_Permiso FOREIGN KEY (PermisoId) REFERENCES Permiso(Id)     ON DELETE NO ACTION
);
CREATE INDEX IX_RolPermiso_PermisoId ON RolPermiso(PermisoId);
GO

-- Conjunto inicial (PermisoCatalogSync completa Nombre/Descripcion y agrega los futuros al arrancar).
INSERT INTO Permiso (Codigo, Modulo, Nombre)
SELECT v.Codigo, LEFT(v.Codigo, CHARINDEX('.', v.Codigo) - 1), v.Codigo
FROM (VALUES
    ('ventas.ver'), ('ventas.crear'), ('ventas.anular'), ('ventas.ver_todas'),
    ('caja.operar'), ('caja.ver_todas'),
    ('productos.ver'), ('productos.gestionar'), ('productos.eliminar'),
    ('categorias.ver'), ('categorias.gestionar'), ('categorias.eliminar'),
    ('clientes.ver'), ('clientes.gestionar'), ('clientes.eliminar'),
    ('proveedores.ver'), ('proveedores.gestionar'), ('proveedores.eliminar'),
    ('inventario.consultar'), ('inventario.ver'), ('inventario.ajustar'),
    ('compras.ver'), ('compras.crear'), ('compras.anular'),
    ('empresa.editar'),
    ('usuarios.ver'), ('usuarios.crear'), ('usuarios.editar'), ('usuarios.cambiar_estado'),
    ('usuarios.reset_password'), ('usuarios.desbloquear'),
    ('roles.ver'), ('roles.gestionar'),
    ('auditoria.ver')
) AS v(Codigo);
GO

-- Permisos por defecto de supervisor y cajero: replican lo que hacían los [Authorize(Roles=...)] previos.
INSERT INTO RolPermiso (RolId, PermisoId)
SELECT r.Id, p.Id
FROM RolCatalogo r
JOIN (VALUES
    ('supervisor','ventas.ver'), ('supervisor','ventas.crear'), ('supervisor','ventas.anular'), ('supervisor','ventas.ver_todas'),
    ('supervisor','caja.operar'), ('supervisor','caja.ver_todas'),
    ('supervisor','productos.ver'), ('supervisor','productos.gestionar'),
    ('supervisor','categorias.ver'), ('supervisor','categorias.gestionar'),
    ('supervisor','clientes.ver'), ('supervisor','clientes.gestionar'),
    ('supervisor','proveedores.ver'), ('supervisor','proveedores.gestionar'),
    ('supervisor','inventario.consultar'), ('supervisor','inventario.ver'), ('supervisor','inventario.ajustar'),
    ('supervisor','compras.ver'), ('supervisor','compras.crear'), ('supervisor','compras.anular'),
    ('cajero','ventas.ver'), ('cajero','ventas.crear'),
    ('cajero','caja.operar'),
    ('cajero','productos.ver'),
    ('cajero','inventario.consultar')
) AS d(RolCodigo, PermisoCodigo) ON d.RolCodigo = r.Codigo
JOIN Permiso p ON p.Codigo = d.PermisoCodigo
WHERE r.EsSistema = 1;
GO

-- ------------------------------------------------------------
-- 3. Usuario: bloqueo de cuenta, cambio de contraseña forzado
-- ------------------------------------------------------------
ALTER TABLE Usuario ADD
    IntentosFallidos     INT        NOT NULL CONSTRAINT DF_Usuario_IntentosFallidos DEFAULT 0,
    BloqueadoHasta       DATETIME2  NULL,
    DebeCambiarPassword  BIT        NOT NULL CONSTRAINT DF_Usuario_DebeCambiarPassword DEFAULT 0,
    UltimoLoginUtc       DATETIME2  NULL,
    PasswordCambiadaUtc  DATETIME2  NULL;
GO

-- ------------------------------------------------------------
-- 4. Refresh tokens (se guarda solo el hash SHA-256, nunca el token)
-- ------------------------------------------------------------
CREATE TABLE RefreshToken (
    Id                  INT IDENTITY PRIMARY KEY,
    UsuarioId           INT              NOT NULL,
    TokenHash           NVARCHAR(64)     NOT NULL,
    FamiliaId           UNIQUEIDENTIFIER NOT NULL,
    CreadoUtc           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ExpiraUtc           DATETIME2        NOT NULL,
    RevocadoUtc         DATETIME2        NULL,
    ReemplazadoPorId    INT              NULL,
    Ip                  NVARCHAR(45)     NULL,
    CONSTRAINT FK_RefreshToken_Usuario FOREIGN KEY (UsuarioId) REFERENCES Usuario(Id) ON DELETE NO ACTION
);
CREATE UNIQUE INDEX UX_RefreshToken_TokenHash ON RefreshToken(TokenHash);
CREATE INDEX IX_RefreshToken_UsuarioId ON RefreshToken(UsuarioId);
CREATE INDEX IX_RefreshToken_FamiliaId ON RefreshToken(FamiliaId);
GO

-- ------------------------------------------------------------
-- 5. Auditoría de seguridad
-- ------------------------------------------------------------
CREATE TABLE EventoSeguridad (
    Id                  BIGINT IDENTITY PRIMARY KEY,
    EmpresaId           INT             NULL,
    ActorUsuarioId      INT             NULL,
    UsuarioObjetivoId   INT             NULL,
    Tipo                NVARCHAR(40)    NOT NULL,
    Detalle             NVARCHAR(500)   NULL,
    Ip                  NVARCHAR(45)    NULL,
    UserAgent           NVARCHAR(250)   NULL,
    FechaUtc            DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_EventoSeguridad_Empresa_Fecha ON EventoSeguridad(EmpresaId, FechaUtc DESC);
CREATE INDEX IX_EventoSeguridad_Tipo ON EventoSeguridad(Tipo);
GO
