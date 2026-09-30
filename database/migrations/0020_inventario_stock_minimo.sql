-- ============================================================
-- 0020_inventario_stock_minimo.sql
-- Permite configurar el stock mínimo de un producto por sucursal desde la pantalla de Inventario.
-- Cuando el stock actual llega a ese mínimo, el frontend muestra una alerta (campanita de
-- notificaciones). Si el producto aún no tiene fila en market.Inventario, se crea con stock 0.
-- ============================================================

CREATE OR ALTER PROCEDURE market.usp_Inventario_ActualizarStockMinimo
    @ProductoId INT, @SucursalId INT, @StockMinimo DECIMAL(18,4)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE market.Inventario SET StockMinimo = @StockMinimo, FechaActualizacion = SYSUTCDATETIME()
    WHERE ProductoId = @ProductoId AND SucursalId = @SucursalId;

    IF @@ROWCOUNT = 0
        INSERT INTO market.Inventario (ProductoId, SucursalId, StockActual, StockMinimo, FechaActualizacion)
        VALUES (@ProductoId, @SucursalId, 0, @StockMinimo, SYSUTCDATETIME());
END
GO
