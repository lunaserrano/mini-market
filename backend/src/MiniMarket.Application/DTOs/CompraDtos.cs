namespace MiniMarket.Application.DTOs;

public record DetalleCompraCreateDto(int ProductoId, decimal Cantidad, decimal CantidadBase, decimal CostoUnitario);

public record CompraCreateDto(
    int ProveedorId,
    string? NumeroDocumentoProveedor,
    IReadOnlyList<DetalleCompraCreateDto> Detalles
);

public record DetalleCompraDto(int ProductoId, string ProductoNombre, decimal Cantidad, decimal CantidadBaseCalculada, decimal CostoUnitario, decimal Subtotal);

public record CompraDto(
    int Id, DateTime Fecha, int ProveedorId, string ProveedorNombre, string? NumeroDocumentoProveedor,
    decimal Subtotal, decimal ImpuestoTotal, decimal Total, string Estado,
    IReadOnlyList<DetalleCompraDto> Detalles
);

public record CompraResumenDto(int Id, DateTime Fecha, string ProveedorNombre, decimal Total, string Estado);
