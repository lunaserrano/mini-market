namespace MiniMarket.Domain.Entities;

/// <summary>Permiso del catálogo global (no lleva EmpresaId). Se mantiene desde Domain/Security/Permisos.cs.</summary>
public class Permiso
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Modulo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
}

/// <summary>Refresh token rotativo. Solo se persiste el hash SHA-256; el token en claro viaja una sola vez al cliente.</summary>
public class RefreshToken
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    /// <summary>Todos los tokens derivados de un mismo login comparten familia: si se reusa uno revocado, se revoca toda la familia.</summary>
    public Guid FamiliaId { get; set; }
    public DateTime CreadoUtc { get; set; }
    public DateTime ExpiraUtc { get; set; }
    public DateTime? RevocadoUtc { get; set; }
    public int? ReemplazadoPorId { get; set; }
    public string? Ip { get; set; }

    public bool EstaRevocado => RevocadoUtc is not null;
    public bool HaExpirado(DateTime ahoraUtc) => ExpiraUtc <= ahoraUtc;
}

/// <summary>Registro de auditoría: evento de seguridad, petición HTTP o acción de la UI (ver <see cref="Origen"/>).</summary>
public class EventoSeguridad
{
    public long Id { get; set; }
    public int? EmpresaId { get; set; }
    public int? ActorUsuarioId { get; set; }
    public int? UsuarioObjetivoId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string? Detalle { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public DateTime FechaUtc { get; set; }
    /// <summary>SEG (seguridad), API (petición HTTP) o UI (clic/navegación del navegador). Ver <see cref="OrigenEvento"/>.</summary>
    public string Origen { get; set; } = OrigenEvento.Seguridad;
    public string? Metodo { get; set; }
    public string? Ruta { get; set; }
    public short? StatusCode { get; set; }
    public int? DuracionMs { get; set; }
    /// <summary>JSON: cuerpo de la petición (con secretos ocultos) o descriptor del elemento clicado.</summary>
    public string? Datos { get; set; }
}

public static class OrigenEvento
{
    public const string Seguridad = "SEG";
    public const string Api = "API";
    public const string Ui = "UI";
}
