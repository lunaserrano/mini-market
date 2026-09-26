-- ============================================================
-- 08_procedures_compras.sql
-- Stored procedures de market para Compra y DetalleCompra.
-- Sustituyen el SQL embebido de CompraRepository (Infrastructure/Persistence).
-- ============================================================

CREATE OR ALTER PROCEDURE market.usp_Compra_Crear
    @EmpresaId INT, @SucursalId INT, @ProveedorId INT, @UsuarioId INT, @NumeroDocumentoProveedor NVARCHAR(50),
    @Fecha DATETIME2, @Subtotal DECIMAL(18,2), @ImpuestoTotal DECIMAL(18,2), @Total DECIMAL(18,2), @Estado NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO market.Compra (EmpresaId, SucursalId, ProveedorId, UsuarioId, NumeroDocumentoProveedor, Fecha,
        Subtotal, ImpuestoTotal, Total, Estado)
    OUTPUT INSERTED.Id
    VALUES (@EmpresaId, @SucursalId, @ProveedorId, @UsuarioId, @NumeroDocumentoProveedor, @Fecha,
        @Subtotal, @ImpuestoTotal, @Total, @Estado);
END
GO

CREATE OR ALTER PROCEDURE market.usp_DetalleCompra_Crear
    @CompraId INT, @ProductoId INT, @TipoPrecioId INT, @Cantidad DECIMAL(18,4), @CantidadBaseCalculada DECIMAL(18,4),
    @CostoUnitario DECIMAL(18,2), @Subtotal DECIMAL(18,2)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO market.DetalleCompra (CompraId, ProductoId, TipoPrecioId, Cantidad, CantidadBaseCalculada, CostoUnitario, Subtotal)
    VALUES (@CompraId, @ProductoId, @TipoPrecioId, @Cantidad, @CantidadBaseCalculada, @CostoUnitario, @Subtotal);
END
GO

CREATE OR ALTER PROCEDURE market.usp_Compra_ObtenerEntidad
    @EmpresaId INT, @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.Compra WHERE EmpresaId = @EmpresaId AND Id = @Id;
END
GO

-- Result set 1: cabecera con nombre del proveedor. Result set 2: detalle con nombres resueltos.
CREATE OR ALTER PROCEDURE market.usp_Compra_ObtenerDetalle
    @EmpresaId INT, @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.Id, c.Fecha, c.ProveedorId, pr.Nombre AS ProveedorNombre, c.NumeroDocumentoProveedor,
           c.Subtotal, c.ImpuestoTotal, c.Total, c.Estado
    FROM market.Compra c
    INNER JOIN market.Proveedor pr ON pr.Id = c.ProveedorId
    WHERE c.EmpresaId = @EmpresaId AND c.Id = @Id;

    SELECT d.ProductoId, p.Nombre AS ProductoNombre, d.TipoPrecioId, tp.Nombre AS TipoPrecioNombre,
           d.Cantidad, d.CantidadBaseCalculada, d.CostoUnitario, d.Subtotal
    FROM market.DetalleCompra d
    INNER JOIN market.Producto p ON p.Id = d.ProductoId
    LEFT JOIN market.TipoPrecio tp ON tp.Id = d.TipoPrecioId
    WHERE d.CompraId = @Id;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Compra_Listar
    @EmpresaId INT, @SucursalId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.Id, c.Fecha, pr.Nombre AS ProveedorNombre, c.Total, c.Estado
    FROM market.Compra c
    INNER JOIN market.Proveedor pr ON pr.Id = c.ProveedorId
    WHERE c.EmpresaId = @EmpresaId AND (@SucursalId IS NULL OR c.SucursalId = @SucursalId)
    ORDER BY c.Fecha DESC;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Compra_Anular
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.Compra SET Estado = 'ANULADA' WHERE Id = @Id;
END
GO
