using MiniMarket.Domain.Entities.Common;

namespace MiniMarket.Domain.Entities;

public class Empresa
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? RazonSocial { get; set; }
    public string? IdentificacionFiscal { get; set; }
    /// <summary>Id de zona horaria IANA (ej. "America/El_Salvador") usado para presentar fechas UTC al usuario.</summary>
    public string ZonaHoraria { get; set; } = "America/El_Salvador";
    /// <summary>Código ISO 4217 de la moneda (USD, GTQ, HNL, NIO, CRC, MXN...). Configurable por empresa/país.</summary>
    public string CodigoMoneda { get; set; } = "USD";
    /// <summary>Símbolo a mostrar en el frontend (ej. "$", "Q", "L"). No tiene que coincidir 1:1 con CodigoMoneda.</summary>
    public string SimboloMoneda { get; set; } = "$";
    /// <summary>Tasa de IVA/impuesto aplicada a todas las ventas (%). Único mantenimiento para toda la
    /// empresa — ya no se configura por producto. Default 13 (IVA vigente en El Salvador).</summary>
    public decimal TasaImpuesto { get; set; } = 13;
    public string Estado { get; set; } = "A";
    public DateTime FechaCreacion { get; set; }
}

public class Sucursal
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string Estado { get; set; } = "A";
    public DateTime FechaCreacion { get; set; }
}

/// <summary>Catálogo de roles (fix #5: antes era texto libre en Usuario.rol sin validación).</summary>
public class RolCatalogo
{
    public int Id { get; set; }
    /// <summary>Código estable usado en claims JWT y en código: "admin" | "supervisor" | "cajero".</summary>
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}

public class Usuario : CatalogoEntity
{
    /// <summary>Sucursal por defecto/asignada del usuario. Nullable porque un admin puede no estar atado a una sola sucursal.</summary>
    public int? SucursalId { get; set; }
    public int RolId { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
}
