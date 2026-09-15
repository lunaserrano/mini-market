-- ============================================================
-- 0003_iva_empresa.sql
-- Mueve la tasa de IVA de "por producto" a "por empresa" (un solo mantenimiento en Configuración,
-- en vez de tener que ingresarla en cada producto). También documenta el cambio de significado de
-- TipoPrecio.PrecioVenta: a partir de aquí es SIEMPRE el precio final que paga el cliente
-- (impuesto incluido) — ver VentaService.CrearAsync, que ahora calcula el IVA "hacia adentro"
-- (desglosa Subtotal/ImpuestoTotal a partir de PrecioVenta) en vez de sumarlo encima. Esto es lo
-- que ya mostraba el POS en pantalla, así que alinea el total que ve el cajero con el que valida
-- el backend.
-- Default: 13% (IVA vigente en El Salvador).
-- ============================================================

ALTER TABLE Empresa ADD
    TasaImpuesto DECIMAL(5,2) NOT NULL CONSTRAINT DF_Empresa_TasaImpuesto DEFAULT 13;

-- Producto.TasaImpuesto queda obsoleto: se elimina (había que dropear primero su constraint
-- DEFAULT sin nombre, generado automáticamente por SQL Server en 0001_initial.sql).
DECLARE @constraintName NVARCHAR(200);
SELECT @constraintName = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON c.default_object_id = dc.object_id
WHERE dc.parent_object_id = OBJECT_ID('Producto') AND c.name = 'TasaImpuesto';

IF @constraintName IS NOT NULL
    EXEC('ALTER TABLE Producto DROP CONSTRAINT ' + @constraintName);

ALTER TABLE Producto DROP COLUMN TasaImpuesto;
