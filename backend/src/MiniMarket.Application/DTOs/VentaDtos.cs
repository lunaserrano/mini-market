namespace MiniMarket.Application.DTOs;

public record DetalleVentaCreateDto(int ProductoId, int TipoPrecioId, decimal Cantidad, decimal Descuento);

public record PagoVentaCreateDto(string Metodo, decimal Monto, string? Referencia);

public record VentaCreateDto(
    int? ClienteId,
    IReadOnlyList<DetalleVentaCreateDto> Detalles,
    IReadOnlyList<PagoVentaCreateDto> Pagos
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

public record VentaResumenDto(int Id, int Folio, DateTime Fecha, string? ClienteNombre, decimal Total, string Estado);

public record AnularVentaRequest(string Motivo);
