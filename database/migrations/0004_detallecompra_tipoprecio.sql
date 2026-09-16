-- ============================================================
-- 0004_detallecompra_tipoprecio.sql
-- Las compras ya no reciben un "factor" (CantidadBase) escrito libremente por línea: ahora
-- referencian una presentación real del producto (TipoPrecio — el mismo catálogo que ya usan las
-- Ventas: Unidad, Cartón x30, Caja x360...), resuelta de forma autoritativa en el servidor
-- (CompraService.CrearAsync), igual que ya hace VentaService con las ventas.
-- Nullable a propósito: las compras históricas (previas a este cambio) quedan sin presentación
-- asociada — nunca se inventa un valor con backfill.
-- ============================================================

ALTER TABLE DetalleCompra ADD
    TipoPrecioId INT NULL CONSTRAINT FK_DetCompra_TipoPrecio FOREIGN KEY REFERENCES TipoPrecio(Id);
