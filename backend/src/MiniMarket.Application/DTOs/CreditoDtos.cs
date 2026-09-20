namespace MiniMarket.Application.DTOs;

public record AbonoCreditoCreateDto(string Metodo, decimal Monto, string? Referencia);

public record AbonoCreditoDto(int Id, DateTime Fecha, string Metodo, decimal Monto, string? Referencia, string UsuarioNombre);

/// <summary>Fila del listado. Vencido = PENDIENTE con fecha de vencimiento ya pasada (se calcula al consultar, no se persiste).</summary>
public record CreditoResumenDto(
    int Id, int VentaId, int VentaFolio, int ClienteId, string ClienteNombre,
    DateTime FechaCreacion, DateTime? FechaVencimiento,
    decimal MontoOriginal, decimal SaldoPendiente, string Estado, bool Vencido
);

/// <summary>TotalVenta - MontoOriginal = lo que el cliente pagó en el momento de la venta.</summary>
public record CreditoDto(
    int Id, int VentaId, int VentaFolio, int ClienteId, string ClienteNombre,
    DateTime FechaCreacion, DateTime? FechaVencimiento, DateTime? FechaCancelacion,
    decimal TotalVenta, decimal MontoOriginal, decimal SaldoPendiente, string Estado, bool Vencido,
    IReadOnlyList<AbonoCreditoDto> Abonos
);
