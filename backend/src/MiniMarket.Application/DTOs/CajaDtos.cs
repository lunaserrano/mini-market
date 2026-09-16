namespace MiniMarket.Application.DTOs;

public record CajaDto(
    int Id, int SucursalId, int UsuarioAperturaId, string UsuarioAperturaNombre, DateTime FechaApertura, decimal MontoInicial,
    int? UsuarioCierreId, string? UsuarioCierreNombre, DateTime? FechaCierre, decimal? MontoFinalDeclarado, decimal? MontoFinalSistema,
    decimal? Diferencia, string Estado
);

public record AperturaCajaRequest(decimal MontoInicial);

public record CierreCajaRequest(decimal MontoFinalDeclarado);

public record MovimientoCajaDto(int Id, int CajaId, string Tipo, string Concepto, decimal Monto, DateTime Fecha);

public record MovimientoCajaCreateDto(string Tipo, string Concepto, decimal Monto);
