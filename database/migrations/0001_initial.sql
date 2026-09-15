-- ============================================================
-- 0001_initial.sql
-- Esquema inicial de MiniMarket (POS + backoffice multi-tenant)
-- Aplicado por DbUp (MiniMarket.Infrastructure) contra SQL Server.
-- Convenciones:
--   - Columnas en PascalCase (mapeo 1:1 con las entidades POCO de Dapper).
--   - Catálogos/maestros usan Estado CHAR(1) 'A'/'I' (soft delete).
--   - Documentos transaccionales (Venta/Compra) usan Estado 'COMPLETADA'/'ANULADA' (nunca se borran).
--   - Fechas transaccionales en datetime2 UTC (SYSUTCDATETIME()); la zona horaria de presentación
--     vive en Empresa.ZonaHoraria.
--   - Toda FK es ON DELETE NO ACTION; nunca cascade sobre datos transaccionales.
--   - Todas las tablas relevantes llevan EmpresaId/SucursalId con índice explícito (SQL Server no
--     indexa FKs automáticamente), para performance y para una futura Row-Level Security.
-- ============================================================

-- ============================================================
-- EMPRESA Y SUCURSAL (multi-tenant)
-- ============================================================

CREATE TABLE Empresa (
    Id                      INT IDENTITY PRIMARY KEY,
    Nombre                  NVARCHAR(150)   NOT NULL,
    RazonSocial             NVARCHAR(200)   NULL,
    IdentificacionFiscal    NVARCHAR(50)    NULL,
    ZonaHoraria             NVARCHAR(60)    NOT NULL DEFAULT 'America/Guatemala',
    Estado                  CHAR(1)         NOT NULL DEFAULT 'A' CHECK (Estado IN ('A','I')),
    FechaCreacion           DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE Sucursal (
    Id              INT IDENTITY PRIMARY KEY,
    EmpresaId       INT             NOT NULL,
    Nombre          NVARCHAR(150)   NOT NULL,
    Direccion       NVARCHAR(250)   NULL,
    Telefono        NVARCHAR(50)    NULL,
    Estado          CHAR(1)         NOT NULL DEFAULT 'A' CHECK (Estado IN ('A','I')),
    FechaCreacion   DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Sucursal_Empresa FOREIGN KEY (EmpresaId) REFERENCES Empresa(Id) ON DELETE NO ACTION
);
CREATE INDEX IX_Sucursal_EmpresaId ON Sucursal(EmpresaId);

-- ============================================================
-- ROLES Y USUARIOS
-- fix #5: Usuario.Rol era texto libre sin catálogo ni constraint.
-- ============================================================

CREATE TABLE RolCatalogo (
    Id      INT IDENTITY PRIMARY KEY,
    Codigo  NVARCHAR(20)  NOT NULL UNIQUE,     -- 'admin' | 'supervisor' | 'cajero'
    Nombre  NVARCHAR(50)  NOT NULL
);

CREATE TABLE Usuario (
    Id                      INT IDENTITY PRIMARY KEY,
    EmpresaId               INT             NOT NULL,
    SucursalId              INT             NULL,
    RolId                   INT             NOT NULL,
    NombreCompleto          NVARCHAR(150)   NOT NULL,
    Username                NVARCHAR(50)    NOT NULL,
    PasswordHash            NVARCHAR(255)   NOT NULL,
    Estado                  CHAR(1)         NOT NULL DEFAULT 'A' CHECK (Estado IN ('A','I')),
    CreadoPorUsuarioId      INT             NULL,
    FechaCreacion           DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    ModificadoPorUsuarioId  INT             NULL,
    FechaModificacion       DATETIME2       NULL,
    CONSTRAINT FK_Usuario_Empresa   FOREIGN KEY (EmpresaId)  REFERENCES Empresa(Id)   ON DELETE NO ACTION,
    CONSTRAINT FK_Usuario_Sucursal  FOREIGN KEY (SucursalId) REFERENCES Sucursal(Id)  ON DELETE NO ACTION,
    CONSTRAINT FK_Usuario_Rol       FOREIGN KEY (RolId)      REFERENCES RolCatalogo(Id)
);
CREATE INDEX IX_Usuario_EmpresaId ON Usuario(EmpresaId);
CREATE INDEX IX_Usuario_SucursalId ON Usuario(SucursalId);
-- Username único por empresa (no global: dos empresas distintas pueden tener el mismo username).
CREATE UNIQUE INDEX UX_Usuario_Empresa_Username ON Usuario(EmpresaId, Username);

-- ============================================================
-- CATEGORÍAS, PROVEEDORES, CLIENTES
-- fix #6: Cliente no existía en el script original (el frontend ya la modelaba).
-- ============================================================

CREATE TABLE Categoria (
    Id                      INT IDENTITY PRIMARY KEY,
    EmpresaId               INT             NOT NULL,
    Nombre                  NVARCHAR(100)   NOT NULL,
    Descripcion             NVARCHAR(250)   NULL,
    Estado                  CHAR(1)         NOT NULL DEFAULT 'A' CHECK (Estado IN ('A','I')),
    CreadoPorUsuarioId      INT             NULL,
    FechaCreacion           DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    ModificadoPorUsuarioId  INT             NULL,
    FechaModificacion       DATETIME2       NULL,
    CONSTRAINT FK_Categoria_Empresa FOREIGN KEY (EmpresaId) REFERENCES Empresa(Id) ON DELETE NO ACTION
);
CREATE INDEX IX_Categoria_EmpresaId ON Categoria(EmpresaId);

CREATE TABLE Proveedor (
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
    CONSTRAINT FK_Proveedor_Empresa FOREIGN KEY (EmpresaId) REFERENCES Empresa(Id) ON DELETE NO ACTION
);
CREATE INDEX IX_Proveedor_EmpresaId ON Proveedor(EmpresaId);

CREATE TABLE Cliente (
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
    CONSTRAINT FK_Cliente_Empresa FOREIGN KEY (EmpresaId) REFERENCES Empresa(Id) ON DELETE NO ACTION
);
CREATE INDEX IX_Cliente_EmpresaId ON Cliente(EmpresaId);

-- ============================================================
-- PRODUCTOS Y TIPOS DE PRECIO
-- fix #2: Producto ya no pertenece a una Sucursal, sino a la Empresa (catálogo compartido).
-- fix #7: TipoPrecio.CantidadBase pasa de INT a DECIMAL (soporta granel) + un solo EsDefault por producto.
-- ============================================================

CREATE TABLE Producto (
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
    TasaImpuesto            DECIMAL(5,2)    NOT NULL DEFAULT 0,
    Estado                  CHAR(1)         NOT NULL DEFAULT 'A' CHECK (Estado IN ('A','I')),
    CreadoPorUsuarioId      INT             NULL,
    FechaCreacion           DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    ModificadoPorUsuarioId  INT             NULL,
    FechaModificacion       DATETIME2       NULL,
    CONSTRAINT FK_Producto_Empresa    FOREIGN KEY (EmpresaId)   REFERENCES Empresa(Id)    ON DELETE NO ACTION,
    CONSTRAINT FK_Producto_Categoria  FOREIGN KEY (CategoriaId) REFERENCES Categoria(Id),
    CONSTRAINT FK_Producto_Proveedor  FOREIGN KEY (ProveedorId) REFERENCES Proveedor(Id)
);
CREATE INDEX IX_Producto_EmpresaId ON Producto(EmpresaId);
CREATE INDEX IX_Producto_CategoriaId ON Producto(CategoriaId);
-- Código de barras único por empresa (no global: dos empresas pueden usar codificación interna repetida).
CREATE UNIQUE INDEX UX_Producto_Empresa_CodigoBarras ON Producto(EmpresaId, CodigoBarras) WHERE CodigoBarras IS NOT NULL;

CREATE TABLE TipoPrecio (
    Id              INT IDENTITY PRIMARY KEY,
    ProductoId      INT             NOT NULL,
    Nombre          NVARCHAR(50)    NOT NULL,           -- Unidad, Docena, Mayoreo, Granel...
    CantidadBase    DECIMAL(18,4)   NOT NULL CHECK (CantidadBase > 0),
    PrecioVenta     DECIMAL(18,2)   NOT NULL CHECK (PrecioVenta >= 0),
    PrecioCompra    DECIMAL(18,2)   NULL,
    EsDefault       BIT             NOT NULL DEFAULT 0,
    Estado          CHAR(1)         NOT NULL DEFAULT 'A' CHECK (Estado IN ('A','I')),
    CONSTRAINT FK_TipoPrecio_Producto FOREIGN KEY (ProductoId) REFERENCES Producto(Id) ON DELETE NO ACTION
);
CREATE INDEX IX_TipoPrecio_ProductoId ON TipoPrecio(ProductoId);
-- Garantiza un único EsDefault = 1 por producto.
CREATE UNIQUE INDEX UX_TipoPrecio_Producto_Default ON TipoPrecio(ProductoId) WHERE EsDefault = 1;

-- ============================================================
-- INVENTARIO Y MOVIMIENTOS
-- fix #1: Inventario ahora se lleva por (ProductoId, SucursalId), no solo por producto.
-- fix #3: MovimientoInventario gana EmpresaId/SucursalId/UsuarioId, StockResultante y
--         referencia estructurada al documento origen (antes era un campo de texto libre).
-- ============================================================

CREATE TABLE Inventario (
    Id                  INT IDENTITY PRIMARY KEY,
    ProductoId          INT             NOT NULL,
    SucursalId          INT             NOT NULL,
    StockActual         DECIMAL(18,4)   NOT NULL DEFAULT 0,
    StockMinimo         DECIMAL(18,4)   NOT NULL DEFAULT 0,
    FechaActualizacion  DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Inventario_Producto  FOREIGN KEY (ProductoId) REFERENCES Producto(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_Inventario_Sucursal  FOREIGN KEY (SucursalId) REFERENCES Sucursal(Id) ON DELETE NO ACTION
);
CREATE UNIQUE INDEX UX_Inventario_Producto_Sucursal ON Inventario(ProductoId, SucursalId);
CREATE INDEX IX_Inventario_SucursalId ON Inventario(SucursalId);

CREATE TABLE MovimientoInventario (
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
    CONSTRAINT FK_MovInventario_Empresa   FOREIGN KEY (EmpresaId)  REFERENCES Empresa(Id)   ON DELETE NO ACTION,
    CONSTRAINT FK_MovInventario_Sucursal  FOREIGN KEY (SucursalId) REFERENCES Sucursal(Id)  ON DELETE NO ACTION,
    CONSTRAINT FK_MovInventario_Producto  FOREIGN KEY (ProductoId) REFERENCES Producto(Id)  ON DELETE NO ACTION,
    CONSTRAINT FK_MovInventario_Usuario   FOREIGN KEY (UsuarioId)  REFERENCES Usuario(Id)
);
CREATE INDEX IX_MovInventario_ProductoId ON MovimientoInventario(ProductoId);
CREATE INDEX IX_MovInventario_SucursalId ON MovimientoInventario(SucursalId);
CREATE INDEX IX_MovInventario_EmpresaId ON MovimientoInventario(EmpresaId);
CREATE INDEX IX_MovInventario_FechaMovimiento ON MovimientoInventario(FechaMovimiento);

-- ============================================================
-- CAJA
-- fix #4: se agrega MovimientoCaja; el original solo tenía apertura/cierre.
-- ============================================================

CREATE TABLE Caja (
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
    CONSTRAINT FK_Caja_Empresa          FOREIGN KEY (EmpresaId)         REFERENCES Empresa(Id)   ON DELETE NO ACTION,
    CONSTRAINT FK_Caja_Sucursal         FOREIGN KEY (SucursalId)        REFERENCES Sucursal(Id)  ON DELETE NO ACTION,
    CONSTRAINT FK_Caja_UsuarioApertura  FOREIGN KEY (UsuarioAperturaId) REFERENCES Usuario(Id),
    CONSTRAINT FK_Caja_UsuarioCierre    FOREIGN KEY (UsuarioCierreId)   REFERENCES Usuario(Id)
);
CREATE INDEX IX_Caja_SucursalId ON Caja(SucursalId);
CREATE INDEX IX_Caja_EmpresaId ON Caja(EmpresaId);
-- Solo puede haber una caja ABIERTA a la vez por usuario (evita doble apertura concurrente).
CREATE UNIQUE INDEX UX_Caja_UsuarioApertura_Abierta ON Caja(UsuarioAperturaId) WHERE Estado = 'ABIERTA';

CREATE TABLE MovimientoCaja (
    Id                      INT IDENTITY PRIMARY KEY,
    CajaId                  INT             NOT NULL,
    Tipo                    NVARCHAR(10)    NOT NULL CHECK (Tipo IN ('INGRESO','EGRESO')),
    Concepto                NVARCHAR(200)   NOT NULL,
    Monto                   DECIMAL(18,2)   NOT NULL CHECK (Monto > 0),
    UsuarioId               INT             NOT NULL,
    Fecha                   DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    DocumentoReferenciaTipo NVARCHAR(20)    NULL,
    DocumentoReferenciaId   INT             NULL,
    CONSTRAINT FK_MovCaja_Caja     FOREIGN KEY (CajaId)    REFERENCES Caja(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_MovCaja_Usuario  FOREIGN KEY (UsuarioId) REFERENCES Usuario(Id)
);
CREATE INDEX IX_MovCaja_CajaId ON MovimientoCaja(CajaId);

-- ============================================================
-- VENTAS
-- fix #8: se agrega Folio, Estado (anulación sin borrar) y desglose Subtotal/Descuento/Impuesto/Total.
-- ============================================================

CREATE TABLE Venta (
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
    CONSTRAINT FK_Venta_Empresa   FOREIGN KEY (EmpresaId)  REFERENCES Empresa(Id)  ON DELETE NO ACTION,
    CONSTRAINT FK_Venta_Sucursal  FOREIGN KEY (SucursalId) REFERENCES Sucursal(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_Venta_Caja      FOREIGN KEY (CajaId)     REFERENCES Caja(Id),
    CONSTRAINT FK_Venta_Cliente   FOREIGN KEY (ClienteId)  REFERENCES Cliente(Id),
    CONSTRAINT FK_Venta_Usuario   FOREIGN KEY (UsuarioId)  REFERENCES Usuario(Id)
);
CREATE INDEX IX_Venta_Fecha ON Venta(Fecha);
CREATE INDEX IX_Venta_SucursalId ON Venta(SucursalId);
CREATE INDEX IX_Venta_EmpresaId ON Venta(EmpresaId);
-- Folio correlativo por sucursal (no global).
CREATE UNIQUE INDEX UX_Venta_Sucursal_Folio ON Venta(SucursalId, Folio);

CREATE TABLE DetalleVenta (
    Id                      INT IDENTITY PRIMARY KEY,
    VentaId                 INT             NOT NULL,
    ProductoId              INT             NOT NULL,
    TipoPrecioId            INT             NOT NULL,
    Cantidad                DECIMAL(18,4)   NOT NULL CHECK (Cantidad > 0),
    CantidadBaseCalculada   DECIMAL(18,4)   NOT NULL,
    PrecioUnitario          DECIMAL(18,2)   NOT NULL,
    Descuento               DECIMAL(18,2)   NOT NULL DEFAULT 0,
    Subtotal                DECIMAL(18,2)   NOT NULL,
    CONSTRAINT FK_DetVenta_Venta       FOREIGN KEY (VentaId)      REFERENCES Venta(Id) ON DELETE CASCADE,
    CONSTRAINT FK_DetVenta_Producto    FOREIGN KEY (ProductoId)   REFERENCES Producto(Id),
    CONSTRAINT FK_DetVenta_TipoPrecio  FOREIGN KEY (TipoPrecioId) REFERENCES TipoPrecio(Id)
);
CREATE INDEX IX_DetVenta_VentaId ON DetalleVenta(VentaId);

CREATE TABLE PagoVenta (
    Id          INT IDENTITY PRIMARY KEY,
    VentaId     INT             NOT NULL,
    Metodo      NVARCHAR(20)    NOT NULL CHECK (Metodo IN ('EFECTIVO','TARJETA','TRANSFERENCIA')),
    Monto       DECIMAL(18,2)   NOT NULL CHECK (Monto > 0),
    Referencia  NVARCHAR(100)   NULL,
    Fecha       DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_PagoVenta_Venta FOREIGN KEY (VentaId) REFERENCES Venta(Id) ON DELETE CASCADE
);
CREATE INDEX IX_PagoVenta_VentaId ON PagoVenta(VentaId);

-- ============================================================
-- COMPRAS
-- ============================================================

CREATE TABLE Compra (
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
    CONSTRAINT FK_Compra_Empresa    FOREIGN KEY (EmpresaId)   REFERENCES Empresa(Id)   ON DELETE NO ACTION,
    CONSTRAINT FK_Compra_Sucursal   FOREIGN KEY (SucursalId)  REFERENCES Sucursal(Id)  ON DELETE NO ACTION,
    CONSTRAINT FK_Compra_Proveedor  FOREIGN KEY (ProveedorId) REFERENCES Proveedor(Id),
    CONSTRAINT FK_Compra_Usuario    FOREIGN KEY (UsuarioId)   REFERENCES Usuario(Id)
);
CREATE INDEX IX_Compra_SucursalId ON Compra(SucursalId);
CREATE INDEX IX_Compra_EmpresaId ON Compra(EmpresaId);
CREATE INDEX IX_Compra_ProveedorId ON Compra(ProveedorId);

CREATE TABLE DetalleCompra (
    Id                      INT IDENTITY PRIMARY KEY,
    CompraId                INT             NOT NULL,
    ProductoId              INT             NOT NULL,
    Cantidad                DECIMAL(18,4)   NOT NULL CHECK (Cantidad > 0),
    CantidadBaseCalculada   DECIMAL(18,4)   NOT NULL,
    CostoUnitario           DECIMAL(18,2)   NOT NULL,
    Subtotal                DECIMAL(18,2)   NOT NULL,
    CONSTRAINT FK_DetCompra_Compra    FOREIGN KEY (CompraId)   REFERENCES Compra(Id) ON DELETE CASCADE,
    CONSTRAINT FK_DetCompra_Producto  FOREIGN KEY (ProductoId) REFERENCES Producto(Id)
);
CREATE INDEX IX_DetCompra_CompraId ON DetalleCompra(CompraId);
