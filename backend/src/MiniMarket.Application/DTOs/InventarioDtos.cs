namespace MiniMarket.Application.DTOs;

public record InventarioDto(int ProductoId, string ProductoNombre, int SucursalId, decimal StockActual, decimal StockMinimo);

public record AjusteInventarioRequest(int ProductoId, int SucursalId, decimal CantidadAjuste, string Observacion);

public record MovimientoInventarioDto(
    int Id,
    int ProductoId,
    string ProductoNombre,
    int SucursalId,
    string TipoMovimiento,
    decimal Cantidad,
    decimal StockResultante,
    string? DocumentoOrigenTipo,
    int? DocumentoOrigenId,
    string? Observacion,
    DateTime FechaMovimiento
);

public record MovimientoInventarioFiltro(int? ProductoId, int? SucursalId, DateTime? Desde, DateTime? Hasta);
