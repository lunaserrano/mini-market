namespace MiniMarket.Application.DTOs;

public record DetalleVentaCreateDto(int ProductoId, int TipoPrecioId, decimal Cantidad, decimal Descuento);

public record PagoVentaCreateDto(string Metodo, decimal Monto, string? Referencia);

/// <summary>
/// AlCredito: la parte no pagada (Total - Pagos) queda como deuda del cliente (requiere ClienteId y permiso
/// creditos.otorgar). En ese caso Pagos puede venir vacío o cubrir solo una parte del total.
/// </summary>
public record VentaCreateDto(
    int? ClienteId,
    IReadOnlyList<DetalleVentaCreateDto> Detalles,
    IReadOnlyList<PagoVentaCreateDto> Pagos,
    bool AlCredito = false,
    DateTime? FechaVencimiento = null
);

public record DetalleVentaDto(
    int ProductoId, string ProductoNombre, int TipoPrecioId, string TipoPrecioNombre,
    decimal Cantidad, decimal CantidadBaseCalculada, decimal PrecioUnitario, decimal Descuento, decimal Subtotal
);

public record PagoVentaDto(string Metodo, decimal Monto, string? Referencia);

public record VentaDto(
    int Id, int Folio, DateTime Fecha, int? ClienteId, string Estado,
    decimal Subtotal, decimal DescuentoTotal, decimal ImpuestoTotal, decimal Total,
    IReadOnlyList<DetalleVentaDto> Detalles,
    IReadOnlyList<PagoVentaDto> Pagos
);

/// <summary>SaldoCredito: deuda vigente de la venta si se hizo a crédito y aún está pendiente; null en cualquier otro caso.</summary>
public record VentaResumenDto(int Id, int Folio, DateTime Fecha, string? ClienteNombre, decimal Total, string Estado, decimal? SaldoCredito);

public record AnularVentaRequest(string Motivo, bool RestituirStock = true);
