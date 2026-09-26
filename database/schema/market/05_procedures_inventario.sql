-- ============================================================
-- 05_procedures_inventario.sql
-- Stored procedures de market para Inventario y MovimientoInventario.
-- Sustituyen el SQL embebido de InventarioRepository (Infrastructure/Persistence).
-- ============================================================

CREATE OR ALTER PROCEDURE market.usp_Inventario_Listar
    @EmpresaId INT, @SucursalId INT = NULL, @ProductoId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.ProductoId, p.Nombre AS ProductoNombre, i.SucursalId, i.StockActual, i.StockMinimo
    FROM market.Inventario i
    INNER JOIN market.Producto p ON p.Id = i.ProductoId
    WHERE p.EmpresaId = @EmpresaId
      AND (@SucursalId IS NULL OR i.SucursalId = @SucursalId)
      AND (@ProductoId IS NULL OR i.ProductoId = @ProductoId)
    ORDER BY p.Nombre;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Inventario_Obtener
    @ProductoId INT, @SucursalId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.Inventario WHERE ProductoId = @ProductoId AND SucursalId = @SucursalId;
END
GO

-- UPDLOCK+ROWLOCK evita que dos ventas concurrentes lean el mismo stock antes de que la primera
-- haga commit. Debe ejecutarse dentro de la transacción activa de la venta (misma conexión).
CREATE OR ALTER PROCEDURE market.usp_Inventario_ObtenerParaActualizar
    @ProductoId INT, @SucursalId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM market.Inventario WITH (UPDLOCK, ROWLOCK)
    WHERE ProductoId = @ProductoId AND SucursalId = @SucursalId;
END
GO

CREATE OR ALTER PROCEDURE market.usp_Inventario_Crear
    @ProductoId INT, @SucursalId INT, @StockActual DECIMAL(18,4), @StockMinimo DECIMAL(18,4), @FechaActualizacion DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO market.Inventario (ProductoId, SucursalId, StockActual, StockMinimo, FechaActualizacion)
    OUTPUT INSERTED.Id
    VALUES (@ProductoId, @SucursalId, @StockActual, @StockMinimo, @FechaActualizacion);
END
GO

CREATE OR ALTER PROCEDURE market.usp_Inventario_ActualizarStock
    @ProductoId INT, @SucursalId INT, @NuevoStock DECIMAL(18,4)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.Inventario SET StockActual = @NuevoStock, FechaActualizacion = SYSUTCDATETIME()
    WHERE ProductoId = @ProductoId AND SucursalId = @SucursalId;
END
GO

CREATE OR ALTER PROCEDURE market.usp_MovimientoInventario_Registrar
    @EmpresaId INT, @SucursalId INT, @ProductoId INT, @UsuarioId INT, @TipoMovimiento NVARCHAR(30),
    @Cantidad DECIMAL(18,4), @StockResultante DECIMAL(18,4), @DocumentoOrigenTipo NVARCHAR(20),
    @DocumentoOrigenId INT, @Observacion NVARCHAR(250), @FechaMovimiento DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO market.MovimientoInventario (EmpresaId, SucursalId, ProductoId, UsuarioId, TipoMovimiento, Cantidad,
        StockResultante, DocumentoOrigenTipo, DocumentoOrigenId, Observacion, FechaMovimiento)
    OUTPUT INSERTED.Id
    VALUES (@EmpresaId, @SucursalId, @ProductoId, @UsuarioId, @TipoMovimiento, @Cantidad,
        @StockResultante, @DocumentoOrigenTipo, @DocumentoOrigenId, @Observacion, @FechaMovimiento);
END
GO

CREATE OR ALTER PROCEDURE market.usp_MovimientoInventario_Listar
    @EmpresaId INT, @ProductoId INT = NULL, @SucursalId INT = NULL, @Desde DATETIME2 = NULL, @Hasta DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT m.Id, m.ProductoId, p.Nombre AS ProductoNombre, m.SucursalId, m.TipoMovimiento, m.Cantidad,
           m.StockResultante, m.DocumentoOrigenTipo, m.DocumentoOrigenId, m.Observacion, m.FechaMovimiento
    FROM market.MovimientoInventario m
    INNER JOIN market.Producto p ON p.Id = m.ProductoId
    WHERE m.EmpresaId = @EmpresaId
      AND (@ProductoId IS NULL OR m.ProductoId = @ProductoId)
      AND (@SucursalId IS NULL OR m.SucursalId = @SucursalId)
      AND (@Desde IS NULL OR m.FechaMovimiento >= @Desde)
      AND (@Hasta IS NULL OR m.FechaMovimiento <= @Hasta)
    ORDER BY m.FechaMovimiento DESC;
END
GO
