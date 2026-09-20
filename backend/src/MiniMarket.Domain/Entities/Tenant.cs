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

/// <summary>
/// Rol de una empresa. Los 3 roles base (admin/supervisor/cajero) se crean por empresa con EsSistema=true
/// y no se pueden eliminar; el resto los crea el administrador desde la UI. Los permisos del rol
/// viven en RolPermiso. El rol de sistema "admin" no tiene filas: siempre tiene todo el catálogo.
/// </summary>
public class RolCatalogo : CatalogoEntity
{
    public const string CodigoAdmin = "admin";
    public const string CodigoSupervisor = "supervisor";
    public const string CodigoCajero = "cajero";

    /// <summary>Código estable usado en claims JWT y en código (slug del nombre en roles personalizados).</summary>
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool EsSistema { get; set; }

    public bool EsAdministrador => EsSistema && Codigo == CodigoAdmin;
}

public class Usuario : CatalogoEntity
{
    /// <summary>Sucursal por defecto/asignada del usuario. Nullable porque un admin puede no estar atado a una sola sucursal.</summary>
    public int? SucursalId { get; set; }
    public int RolId { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    public int IntentosFallidos { get; set; }
    public DateTime? BloqueadoHasta { get; set; }
    public bool DebeCambiarPassword { get; set; }
    public DateTime? UltimoLoginUtc { get; set; }
    public DateTime? PasswordCambiadaUtc { get; set; }

    public bool EstaBloqueado(DateTime ahoraUtc) => BloqueadoHasta is DateTime hasta && hasta > ahoraUtc;
}
