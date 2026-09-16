namespace MiniMarket.Application.DTOs;

/// <summary>
/// CostoUnidadMedida es el precio pagado por TODA la unidad de medida elegida (ej. $30 la caja
/// completa), nunca por unidad base — ya no se envía ningún factor de conversión: TipoPrecioId
/// referencia una presentación real del producto (el mismo catálogo que usan las Ventas) y el
/// servidor resuelve su CantidadBase de forma autoritativa (ver CompraService.CrearAsync).
/// </summary>
public record DetalleCompraCreateDto(int ProductoId, int TipoPrecioId, decimal Cantidad, decimal CostoUnidadMedida);

public record CompraCreateDto(
    int ProveedorId,
    string? NumeroDocumentoProveedor,
    IReadOnlyList<DetalleCompraCreateDto> Detalles
);

/// <summary>TipoPrecioId/TipoPrecioNombre son nullable porque las compras registradas antes de
/// este cambio no tienen presentación asociada (columna agregada en 0004, nullable a propósito).</summary>
public record DetalleCompraDto(int ProductoId, string ProductoNombre, int? TipoPrecioId, string? TipoPrecioNombre,
    decimal Cantidad, decimal CantidadBaseCalculada, decimal CostoUnitario, decimal Subtotal);

public record CompraDto(
    int Id, DateTime Fecha, int ProveedorId, string ProveedorNombre, string? NumeroDocumentoProveedor,
    decimal Subtotal, decimal ImpuestoTotal, decimal Total, string Estado,
    IReadOnlyList<DetalleCompraDto> Detalles
);

public record CompraResumenDto(int Id, DateTime Fecha, string ProveedorNombre, decimal Total, string Estado);
