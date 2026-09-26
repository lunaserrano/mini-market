-- ============================================================
-- 07_procedures_ventas.sql
-- Stored procedures de market para Venta, DetalleVenta y PagoVenta.
-- Sustituyen el SQL embebido de VentaRepository (Infrastructure/Persistence).
-- ============================================================

-- MAX+1 dentro de la misma transacción; combinado con el índice único (SucursalId, Folio) y el
-- UPDLOCK/HOLDLOCK, evita folios duplicados bajo concurrencia normal. Debe ejecutarse dentro de la
-- transacción activa de la venta (misma conexión).
CREATE OR ALTER PROCEDURE market.usp_Venta_ObtenerSiguienteFolio
    @SucursalId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL(MAX(Folio), 0) + 1
    FROM market.Venta WITH (UPDLOCK, HOLDLOCK)
    WHERE SucursalId = @SucursalId;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Venta_Crear
    @EmpresaId INT, @SucursalId INT, @CajaId INT, @ClienteId INT, @UsuarioId INT, @Folio INT, @Fecha DATETIME2,
    @Subtotal DECIMAL(18,2), @DescuentoTotal DECIMAL(18,2), @ImpuestoTotal DECIMAL(18,2), @Total DECIMAL(18,2), @Estado NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO market.Venta (EmpresaId, SucursalId, CajaId, ClienteId, UsuarioId, Folio, Fecha,
        Subtotal, DescuentoTotal, ImpuestoTotal, Total, Estado)
    OUTPUT INSERTED.Id
    VALUES (@EmpresaId, @SucursalId, @CajaId, @ClienteId, @UsuarioId, @Folio, @Fecha,
        @Subtotal, @DescuentoTotal, @ImpuestoTotal, @Total, @Estado);
END
GO

CREATE OR ALTER PROCEDURE market.usp_DetalleVenta_Crear
    @VentaId INT, @ProductoId INT, @TipoPrecioId INT, @Cantidad DECIMAL(18,4), @CantidadBaseCalculada DECIMAL(18,4),
    @PrecioUnitario DECIMAL(18,2), @Descuento DECIMAL(18,2), @Subtotal DECIMAL(18,2)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO market.DetalleVenta (VentaId, ProductoId, TipoPrecioId, Cantidad, CantidadBaseCalculada, PrecioUnitario, Descuento, Subtotal)
    VALUES (@VentaId, @ProductoId, @TipoPrecioId, @Cantidad, @CantidadBaseCalculada, @PrecioUnitario, @Descuento, @Subtotal);
END
GO

CREATE OR ALTER PROCEDURE market.usp_PagoVenta_Crear
    @VentaId INT, @Metodo NVARCHAR(20), @Monto DECIMAL(18,2), @Referencia NVARCHAR(100), @Fecha DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO market.PagoVenta (VentaId, Metodo, Monto, Referencia, Fecha)
    VALUES (@VentaId, @Metodo, @Monto, @Referencia, @Fecha);
END
GO

CREATE OR ALTER PROCEDURE market.usp_Venta_ObtenerEntidad
    @EmpresaId INT, @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.Venta WHERE EmpresaId = @EmpresaId AND Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE market.usp_DetalleVenta_ListarEntidad
    @VentaId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.DetalleVenta WHERE VentaId = @VentaId;
END
GO

-- Result set 1: cabecera de la venta. Result set 2: detalle con nombres resueltos. Result set 3: pagos.
CREATE OR ALTER PROCEDURE market.usp_Venta_ObtenerDetalle
    @EmpresaId INT, @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.Venta WHERE EmpresaId = @EmpresaId AND Id = @Id;

    SELECT d.ProductoId, p.Nombre AS ProductoNombre, d.TipoPrecioId, tp.Nombre AS TipoPrecioNombre,
           d.Cantidad, d.CantidadBaseCalculada, d.PrecioUnitario, d.Descuento, d.Subtotal
    FROM market.DetalleVenta d
    INNER JOIN market.Producto p ON p.Id = d.ProductoId
    INNER JOIN market.TipoPrecio tp ON tp.Id = d.TipoPrecioId
    WHERE d.VentaId = @Id;

    SELECT Metodo, Monto, Referencia FROM market.PagoVenta WHERE VentaId = @Id;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Venta_Listar
    @EmpresaId INT, @SucursalId INT = NULL, @UsuarioId INT = NULL, @CajaId INT = NULL,
    @Desde DATETIME2 = NULL, @Hasta DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT v.Id, v.Folio, v.Fecha, c.Nombre AS ClienteNombre, v.Total, v.Estado,
           CASE WHEN cr.Estado = 'PENDIENTE' THEN cr.SaldoPendiente END AS SaldoCredito
    FROM market.Venta v
    LEFT JOIN market.Cliente c ON c.Id = v.ClienteId
    LEFT JOIN market.Credito cr ON cr.VentaId = v.Id
    WHERE v.EmpresaId = @EmpresaId
      AND (@SucursalId IS NULL OR v.SucursalId = @SucursalId)
      AND (@UsuarioId IS NULL OR v.UsuarioId = @UsuarioId)
      AND (@CajaId IS NULL OR v.CajaId = @CajaId)
      AND (@Desde IS NULL OR v.Fecha >= @Desde)
      AND (@Hasta IS NULL OR v.Fecha <= @Hasta)
    ORDER BY v.Fecha DESC;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Venta_Anular
    @Id INT, @UsuarioAnulacionId INT, @Motivo NVARCHAR(250)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.Venta
    SET Estado = 'ANULADA', UsuarioAnulacionId = @UsuarioAnulacionId, MotivoAnulacion = @Motivo, FechaAnulacion = SYSUTCDATETIME()
    WHERE Id = @Id;
END
GO
