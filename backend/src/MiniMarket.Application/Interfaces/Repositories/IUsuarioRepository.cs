using MiniMarket.Application.DTOs;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Application.Interfaces.Repositories;

public interface IUsuarioRepository
{
    Task<Usuario?> ObtenerPorUsernameAsync(int empresaId, string username);
    Task<Usuario?> ObtenerPorIdAsync(int empresaId, int id);
    /// <summary>Sin filtro de empresa: solo para resolver la sesión de un refresh token (que pertenece a un usuario, no a un request con tenant).</summary>
    Task<Usuario?> ObtenerParaSesionAsync(int usuarioId);
    Task<IReadOnlyList<Usuario>> ListarAsync(int empresaId);
    Task<int> CrearAsync(Usuario usuario);
    Task ActualizarAsync(Usuario usuario);
    Task CambiarEstadoAsync(int empresaId, int id, string estado, int modificadoPorUsuarioId);
    /// <summary>Cambia el hash, limpia intentos fallidos y bloqueo, y fija PasswordCambiadaUtc.</summary>
    Task ActualizarPasswordAsync(int empresaId, int id, string passwordHash, bool debeCambiarPassword, int? modificadoPorUsuarioId);
    /// <summary>
    /// Suma un intento fallido de forma atómica. Al alcanzar <paramref name="maxIntentos"/> bloquea hasta
    /// <paramref name="bloqueoHastaUtc"/> y reinicia el contador. Devuelve el BloqueadoHasta resultante (null si no quedó bloqueada).
    /// </summary>
    Task<DateTime?> RegistrarIntentoFallidoAsync(int empresaId, int id, int maxIntentos, DateTime bloqueoHastaUtc);
    Task RegistrarLoginOkAsync(int empresaId, int id, DateTime ahoraUtc);
    Task DesbloquearAsync(int empresaId, int id);
    /// <summary>Usuarios activos con rol Administrador en la empresa, sin contar a <paramref name="excluirUsuarioId"/>.</summary>
    Task<int> ContarAdministradoresActivosAsync(int empresaId, int? excluirUsuarioId);
    Task<bool> ExisteSucursalAsync(int empresaId, int sucursalId);
}

public interface IRolRepository
{
    Task<IReadOnlyList<RolDto>> ListarAsync(int empresaId);
    Task<RolCatalogo?> ObtenerPorIdAsync(int empresaId, int id);
    /// <summary>Códigos de permiso asignados en RolPermiso (el admin no tiene filas: ver PermisoService).</summary>
    Task<IReadOnlyList<string>> ObtenerCodigosPermisosAsync(int rolId);
    /// <summary>Crea el rol y sus permisos en una sola transacción. Devuelve el Id.</summary>
    Task<int> CrearAsync(RolCatalogo rol, IReadOnlyCollection<string> permisos);
    /// <summary>Actualiza nombre/descripción y reemplaza los permisos (en una transacción) si <paramref name="permisos"/> no es null.</summary>
    Task ActualizarAsync(RolCatalogo rol, IReadOnlyCollection<string>? permisos);
    Task EliminarAsync(int empresaId, int id);
    Task<int> ContarUsuariosAsync(int empresaId, int rolId);
}

public interface IRefreshTokenRepository
{
    Task<int> CrearAsync(RefreshToken token);
    Task<RefreshToken?> ObtenerPorHashAsync(string tokenHash);
    /// <summary>Revoca el token si seguía activo. Devuelve false si ya estaba revocado (condición de carrera).</summary>
    Task<bool> RevocarAsync(int id, DateTime ahoraUtc);
    Task MarcarReemplazoAsync(int id, int reemplazadoPorId);
    Task RevocarFamiliaAsync(Guid familiaId, DateTime ahoraUtc);
    Task RevocarTodosDeUsuarioAsync(int usuarioId, DateTime ahoraUtc);
    /// <summary>Elimina tokens del usuario expirados o revocados antes de <paramref name="antesDeUtc"/> (mantenimiento de la tabla).</summary>
    Task PurgarAntiguosAsync(int usuarioId, DateTime antesDeUtc);
}

public interface IEventoSeguridadRepository
{
    Task RegistrarAsync(EventoSeguridad evento);
    /// <summary>Inserta varios eventos en una sola transacción (los usa el escritor en segundo plano de la auditoría).</summary>
    Task RegistrarLoteAsync(IReadOnlyCollection<EventoSeguridad> eventos);
    Task<PaginaResultado<EventoSeguridadDto>> ListarAsync(int empresaId, AuditoriaFiltro filtro);
}
