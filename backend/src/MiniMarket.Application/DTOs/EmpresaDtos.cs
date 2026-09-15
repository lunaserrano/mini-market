namespace MiniMarket.Application.DTOs;

/// <summary>Configuración general de la empresa — incluye la moneda, para que cada despliegue
/// (por país) pueda cambiarla desde un mantenimiento sin tocar código.</summary>
public record EmpresaDto(
    int Id,
    string Nombre,
    string? RazonSocial,
    string? IdentificacionFiscal,
    string ZonaHoraria,
    string CodigoMoneda,
    string SimboloMoneda,
    decimal TasaImpuesto
);

public record EmpresaUpdateDto(
    string Nombre,
    string? RazonSocial,
    string? IdentificacionFiscal,
    string ZonaHoraria,
    string CodigoMoneda,
    string SimboloMoneda,
    decimal TasaImpuesto
);
