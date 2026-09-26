-- ============================================================
-- 01_schema_and_tables.sql
-- Esquema "market": todas las tablas de MiniMarket (POS + backoffice multi-tenant), consolidadas
-- en el estado final tras las migraciones 0001-0006 de database/migrations, pero ya bajo el
-- esquema market en vez de dbo (antes: por seguridad, se separan datos/objetos de aplicación del
-- esquema dbo por defecto, de forma que los permisos de BD puedan otorgarse de forma acotada).
--
-- Este script asume una base de datos NUEVA y VACÍA. Para llevar una base existente (creada con
-- database/migrations en dbo) a este mismo estado, usa database/migrations/0007_market_schema_transfer.sql
-- en adelante, que hace ALTER SCHEMA ... TRANSFER en vez de CREATE TABLE (no se puede recrear una
-- tabla con datos).
--
-- Convenciones (idénticas a las de la versión anterior en dbo):
--   - Columnas en PascalCase (mapeo 1:1 con las entidades POCO de Dapper).
--   - Catálogos/maestros usan Estado CHAR(1) 'A'/'I' (soft delete).
--   - Documentos transaccionales (Venta/Compra) usan Estado 'COMPLETADA'/'ANULADA' (nunca se borran).
--   - Fechas transaccionales en datetime2 UTC (SYSUTCDATETIME()).
--   - Toda FK es ON DELETE NO ACTION salvo el detalle de un documento (DetalleVenta/DetalleCompra/
--     PagoVenta), que sí es CASCADE respecto de su cabecera.
-- ============================================================

--CREATE SCHEMA market;
--GO

-- ============================================================
-- EMPRESA Y SUCURSAL (multi-tenant)
-- ============================================================

CREATE TABLE market.Empresa (
    Id                      INT IDENTITY PRIMARY KEY,
    Nombre                  NVARCHAR(150)   NOT NULL,
    RazonSocial             NVARCHAR(200)   NULL,
    IdentificacionFiscal    NVARCHAR(50)    NULL,
    ZonaHoraria             NVARCHAR(60)    NOT NULL DEFAULT 'America/Guatemala',
    CodigoMoneda            NVARCHAR(3)     NOT NULL DEFAULT 'USD',
    SimboloMoneda           NVARCHAR(5)     NOT NULL DEFAULT '$',
    TasaImpuesto            DECIMAL(5,2)    NOT NULL DEFAULT 13,
    Estado                  CHAR(1)         NOT NULL DEFAULT 'A' CHECK (Estado IN ('A','I')),
    FechaCreacion           DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE market.Sucursal (
    Id              INT IDENTITY PRIMARY KEY,
    EmpresaId       INT             NOT NULL,
    Nombre          NVARCHAR(150)   NOT NULL,
    Direccion       NVARCHAR(250)   NULL,
    Telefono        NVARCHAR(50)    NULL,
    Estado          CHAR(1)         NOT NULL DEFAULT 'A' CHECK (Estado IN ('A','I')),
    FechaCreacion   DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Sucursal_Empresa FOREIGN KEY (EmpresaId) REFERENCES market.Empresa(Id) ON DELETE NO ACTION
);
CREATE INDEX IX_Sucursal_EmpresaId ON market.Sucursal(EmpresaId);

-- Parámetros de configuración por empresa/sucursal (SucursalId = 0 = toda la empresa). Primer uso:
-- la bandera de auditoría (ver 0018_parametros.sql). Sin fila para una empresa => se asume habilitada.
CREATE TABLE market.Parametro (
    EmpresaId               INT             NOT NULL,
    SucursalId              INT             NOT NULL DEFAULT 0,
    AuditoriaHabilitada     BIT             NOT NULL DEFAULT 1,
    ModificadoPorUsuarioId  INT             NULL,
    FechaModificacion       DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Parametro PRIMARY KEY (EmpresaId, SucursalId),
    CONSTRAINT FK_Parametro_Empresa FOREIGN KEY (EmpresaId) REFERENCES market.Empresa(Id) ON DELETE NO ACTION
);

-- ============================================================
-- ROLES Y USUARIOS
-- ============================================================

CREATE TABLE market.RolCatalogo (
    Id                      INT IDENTITY PRIMARY KEY,
    EmpresaId               INT             NOT NULL,
    Codigo                  NVARCHAR(50)    NOT NULL,
    Nombre                  NVARCHAR(100)   NOT NULL,
    Descripcion             NVARCHAR(250)   NULL,
    EsSistema               BIT             NOT NULL DEFAULT 0,
    Estado                  CHAR(1)         NOT NULL DEFAULT 'A' CHECK (Estado IN ('A','I')),
    CreadoPorUsuarioId      INT             NULL,
    FechaCreacion           DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    ModificadoPorUsuarioId  INT             NULL,
    FechaModificacion       DATETIME2       NULL,
    CONSTRAINT FK_RolCatalogo_Empresa FOREIGN KEY (EmpresaId) REFERENCES market.Empresa(Id) ON DELETE NO ACTION
);
CREATE UNIQUE INDEX UX_RolCatalogo_Empresa_Codigo ON market.RolCatalogo(EmpresaId, Codigo);

CREATE TABLE market.Usuario (
    Id                      INT IDENTITY PRIMARY KEY,
    EmpresaId               INT             NOT NULL,
    SucursalId              INT             NULL,
    RolId                   INT             NOT NULL,
    NombreCompleto          NVARCHAR(150)   NOT NULL,
    Username                NVARCHAR(50)    NOT NULL,
    PasswordHash            NVARCHAR(255)   NOT NULL,
    Estado                  CHAR(1)         NOT NULL DEFAULT 'A' CHECK (Estado IN ('A','I')),
    IntentosFallidos        INT             NOT NULL DEFAULT 0,
    BloqueadoHasta          DATETIME2       NULL,
    DebeCambiarPassword     BIT             NOT NULL DEFAULT 0,
    UltimoLoginUtc          DATETIME2       NULL,
    PasswordCambiadaUtc     DATETIME2       NULL,
    CreadoPorUsuarioId      INT             NULL,
    FechaCreacion           DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    ModificadoPorUsuarioId  INT             NULL,
    FechaModificacion       DATETIME2       NULL,
    CONSTRAINT FK_Usuario_Empresa   FOREIGN KEY (EmpresaId)  REFERENCES market.Empresa(Id)      ON DELETE NO ACTION,
    CONSTRAINT FK_Usuario_Sucursal  FOREIGN KEY (SucursalId) REFERENCES market.Sucursal(Id)     ON DELETE NO ACTION,
    CONSTRAINT FK_Usuario_Rol       FOREIGN KEY (RolId)      REFERENCES market.RolCatalogo(Id)
);
CREATE INDEX IX_Usuario_EmpresaId ON market.Usuario(EmpresaId);
CREATE INDEX IX_Usuario_SucursalId ON market.Usuario(SucursalId);
CREATE UNIQUE INDEX UX_Usuario_Empresa_Username ON market.Usuario(EmpresaId, Username);

-- ============================================================
-- PERMISOS
-- ============================================================

CREATE TABLE market.Permiso (
    Id          INT IDENTITY PRIMARY KEY,
    Codigo      NVARCHAR(60)    NOT NULL,
    Modulo      NVARCHAR(40)    NOT NULL,
    Nombre      NVARCHAR(100)   NOT NULL,
    Descripcion NVARCHAR(250)   NULL
);
CREATE UNIQUE INDEX UX_Permiso_Codigo ON market.Permiso(Codigo);

CREATE TABLE market.RolPermiso (
    RolId       INT NOT NULL,
    PermisoId   INT NOT NULL,
    CONSTRAINT PK_RolPermiso PRIMARY KEY (RolId, PermisoId),
    CONSTRAINT FK_RolPermiso_Rol     FOREIGN KEY (RolId)     REFERENCES market.RolCatalogo(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_RolPermiso_Permiso FOREIGN KEY (PermisoId) REFERENCES market.Permiso(Id)     ON DELETE NO ACTION
);
CREATE INDEX IX_RolPermiso_PermisoId ON market.RolPermiso(PermisoId);

-- ============================================================
-- REFRESH TOKENS Y AUDITORÍA DE SEGURIDAD / ACTIVIDAD
-- ============================================================

CREATE TABLE market.RefreshToken (
    Id                  INT IDENTITY PRIMARY KEY,
    UsuarioId           INT              NOT NULL,
    TokenHash           NVARCHAR(64)     NOT NULL,
    FamiliaId           UNIQUEIDENTIFIER NOT NULL,
    CreadoUtc           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ExpiraUtc           DATETIME2        NOT NULL,
    RevocadoUtc         DATETIME2        NULL,
    ReemplazadoPorId    INT              NULL,
    Ip                  NVARCHAR(45)     NULL,
    CONSTRAINT FK_RefreshToken_Usuario FOREIGN KEY (UsuarioId) REFERENCES market.Usuario(Id) ON DELETE NO ACTION
);
CREATE UNIQUE INDEX UX_RefreshToken_TokenHash ON market.RefreshToken(TokenHash);
CREATE INDEX IX_RefreshToken_UsuarioId ON market.RefreshToken(UsuarioId);
CREATE INDEX IX_RefreshToken_FamiliaId ON market.RefreshToken(FamiliaId);

-- Origen: 'SEG' (seguridad: login/roles/usuarios), 'API' (petición HTTP, vía AuditoriaMiddleware),
-- 'UI' (clic/navegación del frontend, enviado por lotes).
CREATE TABLE market.EventoSeguridad (
    Id                  BIGINT IDENTITY PRIMARY KEY,
    EmpresaId           INT             NULL,
    ActorUsuarioId      INT             NULL,
    UsuarioObjetivoId   INT             NULL,
    Tipo                NVARCHAR(40)    NOT NULL,
    Detalle             NVARCHAR(500)   NULL,
    Ip                  NVARCHAR(45)    NULL,
    UserAgent           NVARCHAR(250)   NULL,
    FechaUtc            DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    Origen              VARCHAR(3)      NOT NULL DEFAULT 'SEG',
    Metodo              VARCHAR(10)     NULL,
    Ruta                NVARCHAR(300)   NULL,
    StatusCode          SMALLINT        NULL,
    DuracionMs          INT             NULL,
    Datos               NVARCHAR(MAX)   NULL
);
CREATE INDEX IX_EventoSeguridad_Empresa_Fecha ON market.EventoSeguridad(EmpresaId, FechaUtc DESC);
CREATE INDEX IX_EventoSeguridad_Tipo ON market.EventoSeguridad(Tipo);
CREATE INDEX IX_EventoSeguridad_Empresa_Origen_Fecha ON market.EventoSeguridad(EmpresaId, Origen, FechaUtc DESC);
CREATE INDEX IX_EventoSeguridad_Actor_Fecha ON market.EventoSeguridad(ActorUsuarioId, FechaUtc DESC);

-- ============================================================
-- CATEGORÍAS, PROVEEDORES, CLIENTES
-- ============================================================

CREATE TABLE market.Categoria (
    Id                      INT IDENTITY PRIMARY KEY,
    EmpresaId               INT             NOT NULL,
    Nombre                  NVARCHAR(100)   NOT NULL,
    Descripcion             NVARCHAR(250)   NULL,
    Estado                  CHAR(1)         NOT NULL DEFAULT 'A' CHECK (Estado IN ('A','I')),
    CreadoPorUsuarioId      INT             NULL,
    FechaCreacion           DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    ModificadoPorUsuarioId  INT             NULL,
    FechaModificacion       DATETIME2       NULL,
    CONSTRAINT FK_Categoria_Empresa FOREIGN KEY (EmpresaId) REFERENCES market.Empresa(Id) ON DELETE NO ACTION
);
CREATE INDEX IX_Categoria_EmpresaId ON market.Categoria(EmpresaId);

CREATE TABLE market.Proveedor (
    Id                      INT IDENTITY PRIMARY KEY,
    EmpresaId               INT             NOT NULL,
    Nombre                  NVARCHAR(150)   NOT NULL,
    Contacto                NVARCHAR(100)   NULL,
    Telefono                NVARCHAR(50)    NULL,
    Email                   NVARCHAR(150)   NULL,
    Direccion               NVARCHAR(250)   NULL,
    IdentificacionFiscal    NVARCHAR(50)    NULL,
    Estado                  CHAR(1)         NOT NULL DEFAULT 'A' CHECK (Estado IN ('A','I')),
    CreadoPorUsuarioId      INT             NULL,
    FechaCreacion           DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    ModificadoPorUsuarioId  INT             NULL,
    FechaModificacion       DATETIME2       NULL,
    CONSTRAINT FK_Proveedor_Empresa FOREIGN KEY (EmpresaId) REFERENCES market.Empresa(Id) ON DELETE NO ACTION
);
CREATE INDEX IX_Proveedor_EmpresaId ON market.Proveedor(EmpresaId);

CREATE TABLE market.Cliente (
    Id                      INT IDENTITY PRIMARY KEY,
    EmpresaId               INT             NOT NULL,
    Nombre                  NVARCHAR(150)   NOT NULL,
    IdentificacionFiscal    NVARCHAR(50)    NULL,
    Telefono                NVARCHAR(50)    NULL,
    Email                   NVARCHAR(150)   NULL,
    Direccion               NVARCHAR(250)   NULL,
    Estado                  CHAR(1)         NOT NULL DEFAULT 'A' CHECK (Estado IN ('A','I')),
    CreadoPorUsuarioId      INT             NULL,
    FechaCreacion           DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    ModificadoPorUsuarioId  INT             NULL,
    FechaModificacion       DATETIME2       NULL,
    CONSTRAINT FK_Cliente_Empresa FOREIGN KEY (EmpresaId) REFERENCES market.Empresa(Id) ON DELETE NO ACTION
);
CREATE INDEX IX_Cliente_EmpresaId ON market.Cliente(EmpresaId);

-- ============================================================
-- PRODUCTOS Y TIPOS DE PRECIO
-- ============================================================

CREATE TABLE market.Producto (
    Id                      INT IDENTITY PRIMARY KEY,
    EmpresaId               INT             NOT NULL,
    CategoriaId             INT             NOT NULL,
    ProveedorId             INT             NULL,
    Nombre                  NVARCHAR(150)   NOT NULL,
    Descripcion             NVARCHAR(500)   NULL,
    CodigoBarras            NVARCHAR(100)   NULL,
    CodigoInterno           NVARCHAR(50)    NULL,
    ImagenPath              NVARCHAR(500)   NULL,
    UnidadBase              NVARCHAR(20)    NOT NULL DEFAULT 'unidad',
    Estado                  CHAR(1)         NOT NULL DEFAULT 'A' CHECK (Estado IN ('A','I')),
    CreadoPorUsuarioId      INT             NULL,
    FechaCreacion           DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    ModificadoPorUsuarioId  INT             NULL,
    FechaModificacion       DATETIME2       NULL,
    CONSTRAINT FK_Producto_Empresa    FOREIGN KEY (EmpresaId)   REFERENCES market.Empresa(Id)    ON DELETE NO ACTION,
    CONSTRAINT FK_Producto_Categoria  FOREIGN KEY (CategoriaId) REFERENCES market.Categoria(Id),
    CONSTRAINT FK_Producto_Proveedor  FOREIGN KEY (ProveedorId) REFERENCES market.Proveedor(Id)
);
CREATE INDEX IX_Producto_EmpresaId ON market.Producto(EmpresaId);
CREATE INDEX IX_Producto_CategoriaId ON market.Producto(CategoriaId);
CREATE UNIQUE INDEX UX_Producto_Empresa_CodigoBarras ON market.Producto(EmpresaId, CodigoBarras) WHERE CodigoBarras IS NOT NULL;

CREATE TABLE market.TipoPrecio (
    Id              INT IDENTITY PRIMARY KEY,
    ProductoId      INT             NOT NULL,
    Nombre          NVARCHAR(50)    NOT NULL,
    CantidadBase    DECIMAL(18,4)   NOT NULL CHECK (CantidadBase > 0),
    PrecioVenta     DECIMAL(18,2)   NOT NULL CHECK (PrecioVenta >= 0),
    PrecioCompra    DECIMAL(18,2)   NULL,
    EsDefault       BIT             NOT NULL DEFAULT 0,
    Estado          CHAR(1)         NOT NULL DEFAULT 'A' CHECK (Estado IN ('A','I')),
    CONSTRAINT FK_TipoPrecio_Producto FOREIGN KEY (ProductoId) REFERENCES market.Producto(Id) ON DELETE NO ACTION
);
CREATE INDEX IX_TipoPrecio_ProductoId ON market.TipoPrecio(ProductoId);
CREATE UNIQUE INDEX UX_TipoPrecio_Producto_Default ON market.TipoPrecio(ProductoId) WHERE EsDefault = 1;

-- ============================================================
-- INVENTARIO Y MOVIMIENTOS
-- ============================================================

CREATE TABLE market.Inventario (
    Id                  INT IDENTITY PRIMARY KEY,
    ProductoId          INT             NOT NULL,
    SucursalId          INT             NOT NULL,
    StockActual         DECIMAL(18,4)   NOT NULL DEFAULT 0,
    StockMinimo         DECIMAL(18,4)   NOT NULL DEFAULT 0,
    FechaActualizacion  DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Inventario_Producto  FOREIGN KEY (ProductoId) REFERENCES market.Producto(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_Inventario_Sucursal  FOREIGN KEY (SucursalId) REFERENCES market.Sucursal(Id) ON DELETE NO ACTION
);
CREATE UNIQUE INDEX UX_Inventario_Producto_Sucursal ON market.Inventario(ProductoId, SucursalId);
CREATE INDEX IX_Inventario_SucursalId ON market.Inventario(SucursalId);

CREATE TABLE market.MovimientoInventario (
    Id                  INT IDENTITY PRIMARY KEY,
    EmpresaId           INT             NOT NULL,
    SucursalId          INT             NOT NULL,
    ProductoId          INT             NOT NULL,
    UsuarioId           INT             NOT NULL,
    TipoMovimiento      NVARCHAR(30)    NOT NULL CHECK (TipoMovimiento IN
                            ('EntradaCompra','SalidaVenta','AjustePositivo','AjusteNegativo',
                             'TrasladoEntrada','TrasladoSalida','DevolucionVenta','DevolucionCompra')),
    Cantidad            DECIMAL(18,4)   NOT NULL,
    StockResultante     DECIMAL(18,4)   NOT NULL,
    DocumentoOrigenTipo NVARCHAR(20)    NULL CHECK (DocumentoOrigenTipo IN ('Venta','Compra','AjusteManual','Traslado')),
    DocumentoOrigenId   INT             NULL,
    Observacion         NVARCHAR(250)   NULL,
    FechaMovimiento     DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_MovInventario_Empresa   FOREIGN KEY (EmpresaId)  REFERENCES market.Empresa(Id)   ON DELETE NO ACTION,
    CONSTRAINT FK_MovInventario_Sucursal  FOREIGN KEY (SucursalId) REFERENCES market.Sucursal(Id)  ON DELETE NO ACTION,
    CONSTRAINT FK_MovInventario_Producto  FOREIGN KEY (ProductoId) REFERENCES market.Producto(Id)  ON DELETE NO ACTION,
    CONSTRAINT FK_MovInventario_Usuario   FOREIGN KEY (UsuarioId)  REFERENCES market.Usuario(Id)
);
CREATE INDEX IX_MovInventario_ProductoId ON market.MovimientoInventario(ProductoId);
CREATE INDEX IX_MovInventario_SucursalId ON market.MovimientoInventario(SucursalId);
CREATE INDEX IX_MovInventario_EmpresaId ON market.MovimientoInventario(EmpresaId);
CREATE INDEX IX_MovInventario_FechaMovimiento ON market.MovimientoInventario(FechaMovimiento);

-- ============================================================
-- CAJA
-- ============================================================

CREATE TABLE market.Caja (
    Id                      INT IDENTITY PRIMARY KEY,
    EmpresaId               INT             NOT NULL,
    SucursalId              INT             NOT NULL,
    UsuarioAperturaId       INT             NOT NULL,
    FechaApertura           DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    MontoInicial            DECIMAL(18,2)   NOT NULL,
    UsuarioCierreId         INT             NULL,
    FechaCierre             DATETIME2       NULL,
    MontoFinalDeclarado     DECIMAL(18,2)   NULL,
    MontoFinalSistema       DECIMAL(18,2)   NULL,
    Diferencia              DECIMAL(18,2)   NULL,
    Estado                  NVARCHAR(20)    NOT NULL DEFAULT 'ABIERTA' CHECK (Estado IN ('ABIERTA','CERRADA')),
    CONSTRAINT FK_Caja_Empresa          FOREIGN KEY (EmpresaId)         REFERENCES market.Empresa(Id)   ON DELETE NO ACTION,
    CONSTRAINT FK_Caja_Sucursal         FOREIGN KEY (SucursalId)        REFERENCES market.Sucursal(Id)  ON DELETE NO ACTION,
    CONSTRAINT FK_Caja_UsuarioApertura  FOREIGN KEY (UsuarioAperturaId) REFERENCES market.Usuario(Id),
    CONSTRAINT FK_Caja_UsuarioCierre    FOREIGN KEY (UsuarioCierreId)   REFERENCES market.Usuario(Id)
);
CREATE INDEX IX_Caja_SucursalId ON market.Caja(SucursalId);
CREATE INDEX IX_Caja_EmpresaId ON market.Caja(EmpresaId);
CREATE UNIQUE INDEX UX_Caja_UsuarioApertura_Abierta ON market.Caja(UsuarioAperturaId) WHERE Estado = 'ABIERTA';

CREATE TABLE market.MovimientoCaja (
    Id                      INT IDENTITY PRIMARY KEY,
    CajaId                  INT             NOT NULL,
    Tipo                    NVARCHAR(10)    NOT NULL CHECK (Tipo IN ('INGRESO','EGRESO')),
    Concepto                NVARCHAR(200)   NOT NULL,
    Monto                   DECIMAL(18,2)   NOT NULL CHECK (Monto > 0),
    UsuarioId               INT             NOT NULL,
    Fecha                   DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    DocumentoReferenciaTipo NVARCHAR(20)    NULL,
    DocumentoReferenciaId   INT             NULL,
    CONSTRAINT FK_MovCaja_Caja     FOREIGN KEY (CajaId)    REFERENCES market.Caja(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_MovCaja_Usuario  FOREIGN KEY (UsuarioId) REFERENCES market.Usuario(Id)
);
CREATE INDEX IX_MovCaja_CajaId ON market.MovimientoCaja(CajaId);

-- ============================================================
-- VENTAS
-- ============================================================

CREATE TABLE market.Venta (
    Id                  INT IDENTITY PRIMARY KEY,
    EmpresaId           INT             NOT NULL,
    SucursalId          INT             NOT NULL,
    CajaId              INT             NOT NULL,
    ClienteId           INT             NULL,
    UsuarioId           INT             NOT NULL,
    Folio               INT             NOT NULL,
    Fecha               DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    Subtotal            DECIMAL(18,2)   NOT NULL,
    DescuentoTotal      DECIMAL(18,2)   NOT NULL DEFAULT 0,
    ImpuestoTotal       DECIMAL(18,2)   NOT NULL DEFAULT 0,
    Total               DECIMAL(18,2)   NOT NULL,
    Estado              NVARCHAR(20)    NOT NULL DEFAULT 'COMPLETADA' CHECK (Estado IN ('COMPLETADA','ANULADA')),
    MotivoAnulacion     NVARCHAR(250)   NULL,
    UsuarioAnulacionId  INT             NULL,
    FechaAnulacion      DATETIME2       NULL,
    CONSTRAINT FK_Venta_Empresa   FOREIGN KEY (EmpresaId)  REFERENCES market.Empresa(Id)  ON DELETE NO ACTION,
    CONSTRAINT FK_Venta_Sucursal  FOREIGN KEY (SucursalId) REFERENCES market.Sucursal(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_Venta_Caja      FOREIGN KEY (CajaId)     REFERENCES market.Caja(Id),
    CONSTRAINT FK_Venta_Cliente   FOREIGN KEY (ClienteId)  REFERENCES market.Cliente(Id),
    CONSTRAINT FK_Venta_Usuario   FOREIGN KEY (UsuarioId)  REFERENCES market.Usuario(Id)
);
CREATE INDEX IX_Venta_Fecha ON market.Venta(Fecha);
CREATE INDEX IX_Venta_SucursalId ON market.Venta(SucursalId);
CREATE INDEX IX_Venta_EmpresaId ON market.Venta(EmpresaId);
CREATE UNIQUE INDEX UX_Venta_Sucursal_Folio ON market.Venta(SucursalId, Folio);

CREATE TABLE market.DetalleVenta (
    Id                      INT IDENTITY PRIMARY KEY,
    VentaId                 INT             NOT NULL,
    ProductoId              INT             NOT NULL,
    TipoPrecioId            INT             NOT NULL,
    Cantidad                DECIMAL(18,4)   NOT NULL CHECK (Cantidad > 0),
    CantidadBaseCalculada   DECIMAL(18,4)   NOT NULL,
    PrecioUnitario          DECIMAL(18,2)   NOT NULL,
    Descuento               DECIMAL(18,2)   NOT NULL DEFAULT 0,
    Subtotal                DECIMAL(18,2)   NOT NULL,
    CONSTRAINT FK_DetVenta_Venta       FOREIGN KEY (VentaId)      REFERENCES market.Venta(Id) ON DELETE CASCADE,
    CONSTRAINT FK_DetVenta_Producto    FOREIGN KEY (ProductoId)   REFERENCES market.Producto(Id),
    CONSTRAINT FK_DetVenta_TipoPrecio  FOREIGN KEY (TipoPrecioId) REFERENCES market.TipoPrecio(Id)
);
CREATE INDEX IX_DetVenta_VentaId ON market.DetalleVenta(VentaId);

CREATE TABLE market.PagoVenta (
    Id          INT IDENTITY PRIMARY KEY,
    VentaId     INT             NOT NULL,
    Metodo      NVARCHAR(20)    NOT NULL CHECK (Metodo IN ('EFECTIVO','TARJETA','TRANSFERENCIA')),
    Monto       DECIMAL(18,2)   NOT NULL CHECK (Monto > 0),
    Referencia  NVARCHAR(100)   NULL,
    Fecha       DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_PagoVenta_Venta FOREIGN KEY (VentaId) REFERENCES market.Venta(Id) ON DELETE CASCADE
);
CREATE INDEX IX_PagoVenta_VentaId ON market.PagoVenta(VentaId);

-- ============================================================
-- COMPRAS
-- ============================================================

CREATE TABLE market.Compra (
    Id                          INT IDENTITY PRIMARY KEY,
    EmpresaId                   INT             NOT NULL,
    SucursalId                  INT             NOT NULL,
    ProveedorId                 INT             NOT NULL,
    UsuarioId                   INT             NOT NULL,
    NumeroDocumentoProveedor    NVARCHAR(50)    NULL,
    Fecha                       DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    Subtotal                    DECIMAL(18,2)   NOT NULL,
    ImpuestoTotal                DECIMAL(18,2)   NOT NULL DEFAULT 0,
    Total                       DECIMAL(18,2)   NOT NULL,
    Estado                      NVARCHAR(20)    NOT NULL DEFAULT 'COMPLETADA' CHECK (Estado IN ('COMPLETADA','ANULADA')),
    CONSTRAINT FK_Compra_Empresa    FOREIGN KEY (EmpresaId)   REFERENCES market.Empresa(Id)   ON DELETE NO ACTION,
    CONSTRAINT FK_Compra_Sucursal   FOREIGN KEY (SucursalId)  REFERENCES market.Sucursal(Id)  ON DELETE NO ACTION,
    CONSTRAINT FK_Compra_Proveedor  FOREIGN KEY (ProveedorId) REFERENCES market.Proveedor(Id),
    CONSTRAINT FK_Compra_Usuario    FOREIGN KEY (UsuarioId)   REFERENCES market.Usuario(Id)
);
CREATE INDEX IX_Compra_SucursalId ON market.Compra(SucursalId);
CREATE INDEX IX_Compra_EmpresaId ON market.Compra(EmpresaId);
CREATE INDEX IX_Compra_ProveedorId ON market.Compra(ProveedorId);

CREATE TABLE market.DetalleCompra (
    Id                      INT IDENTITY PRIMARY KEY,
    CompraId                INT             NOT NULL,
    ProductoId              INT             NOT NULL,
    TipoPrecioId            INT             NULL,
    Cantidad                DECIMAL(18,4)   NOT NULL CHECK (Cantidad > 0),
    CantidadBaseCalculada   DECIMAL(18,4)   NOT NULL,
    CostoUnitario           DECIMAL(18,2)   NOT NULL,
    Subtotal                DECIMAL(18,2)   NOT NULL,
    CONSTRAINT FK_DetCompra_Compra     FOREIGN KEY (CompraId)     REFERENCES market.Compra(Id) ON DELETE CASCADE,
    CONSTRAINT FK_DetCompra_Producto   FOREIGN KEY (ProductoId)   REFERENCES market.Producto(Id),
    CONSTRAINT FK_DetCompra_TipoPrecio FOREIGN KEY (TipoPrecioId) REFERENCES market.TipoPrecio(Id)
);
CREATE INDEX IX_DetCompra_CompraId ON market.DetalleCompra(CompraId);

-- ============================================================
-- CRÉDITOS
-- ============================================================

CREATE TABLE market.Credito (
    Id                  INT IDENTITY PRIMARY KEY,
    EmpresaId           INT             NOT NULL,
    VentaId             INT             NOT NULL,
    ClienteId           INT             NOT NULL,
    MontoOriginal       DECIMAL(18,2)   NOT NULL CHECK (MontoOriginal > 0),
    SaldoPendiente      DECIMAL(18,2)   NOT NULL CHECK (SaldoPendiente >= 0),
    Estado              NVARCHAR(20)    NOT NULL DEFAULT 'PENDIENTE' CHECK (Estado IN ('PENDIENTE','PAGADO','ANULADO')),
    FechaVencimiento    DATETIME2       NULL,
    FechaCreacion       DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    CreadoPorUsuarioId  INT             NOT NULL,
    FechaCancelacion    DATETIME2       NULL,
    CONSTRAINT FK_Credito_Empresa  FOREIGN KEY (EmpresaId) REFERENCES market.Empresa(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_Credito_Venta    FOREIGN KEY (VentaId)   REFERENCES market.Venta(Id)   ON DELETE NO ACTION,
    CONSTRAINT FK_Credito_Cliente  FOREIGN KEY (ClienteId) REFERENCES market.Cliente(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_Credito_Usuario  FOREIGN KEY (CreadoPorUsuarioId) REFERENCES market.Usuario(Id),
    CONSTRAINT CK_Credito_Saldo    CHECK (SaldoPendiente <= MontoOriginal)
);
CREATE UNIQUE INDEX UX_Credito_VentaId ON market.Credito(VentaId);
CREATE INDEX IX_Credito_Empresa_Estado ON market.Credito(EmpresaId, Estado);
CREATE INDEX IX_Credito_ClienteId ON market.Credito(ClienteId);

CREATE TABLE market.AbonoCredito (
    Id          INT IDENTITY PRIMARY KEY,
    CreditoId   INT             NOT NULL,
    CajaId      INT             NULL,
    UsuarioId   INT             NOT NULL,
    Metodo      NVARCHAR(20)    NOT NULL CHECK (Metodo IN ('EFECTIVO','TARJETA','TRANSFERENCIA')),
    Monto       DECIMAL(18,2)   NOT NULL CHECK (Monto > 0),
    Referencia  NVARCHAR(100)   NULL,
    Fecha       DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_AbonoCredito_Credito FOREIGN KEY (CreditoId) REFERENCES market.Credito(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_AbonoCredito_Caja    FOREIGN KEY (CajaId)    REFERENCES market.Caja(Id),
    CONSTRAINT FK_AbonoCredito_Usuario FOREIGN KEY (UsuarioId) REFERENCES market.Usuario(Id)
);
CREATE INDEX IX_AbonoCredito_CreditoId ON market.AbonoCredito(CreditoId);
GO
