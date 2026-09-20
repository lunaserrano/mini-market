using MiniMarket.Domain.Entities;

namespace MiniMarket.Application.Interfaces;

public interface IPasswordHasher
{
    string Hash(string plainPassword);
    bool Verify(string plainPassword, string passwordHash);
    /// <summary>Hace el mismo trabajo costoso que <see cref="Verify"/> contra un hash falso, para que un usuario inexistente no responda más rápido que uno existente.</summary>
    void SimularVerificacion(string plainPassword);
}

public sealed record AccessToken(string Token, DateTime ExpiraUtc);

public interface IJwtTokenGenerator
{
    /// <summary>Genera un JWT de vida corta con claims empresa_id/sucursal_id/usuario_id/rol y un claim "permiso" por cada permiso.</summary>
    AccessToken Generar(Usuario usuario, RolCatalogo rol, IReadOnlyCollection<string> permisos);
}

public interface IRefreshTokenGenerator
{
    /// <summary>Token aleatorio criptográficamente seguro + su hash SHA-256 (el único que se persiste).</summary>
    (string Token, string Hash) Generar();
    string Hashear(string token);
}

/// <summary>Datos de la petición HTTP actual para la auditoría (IP y User-Agent). Vacíos fuera de una petición.</summary>
public interface IRequestInfo
{
    string? Ip { get; }
    string? UserAgent { get; }
}

/// <summary>Punto de entrada no bloqueante para la auditoría de actividad (peticiones HTTP y acciones de la UI).</summary>
public interface IAuditoriaCola
{
    bool Encolar(EventoSeguridad evento);
}

/// <summary>Registra eventos en la auditoría de seguridad. Nunca lanza: un fallo al auditar no debe tumbar la operación.</summary>
public interface ISeguridadAuditor
{
    Task RegistrarAsync(string tipo, int? empresaId, int? actorUsuarioId, int? usuarioObjetivoId, string? detalle = null);
}
