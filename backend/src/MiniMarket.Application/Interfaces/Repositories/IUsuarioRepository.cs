using MiniMarket.Domain.Entities;

namespace MiniMarket.Application.Interfaces.Repositories;

public interface IUsuarioRepository
{
    Task<Usuario?> ObtenerPorUsernameAsync(int empresaId, string username);
    Task<Usuario?> ObtenerPorIdAsync(int empresaId, int id);
    Task<string> ObtenerCodigoRolAsync(int rolId);
    Task<IReadOnlyList<Usuario>> ListarAsync(int empresaId);
    Task<int> CrearAsync(Usuario usuario);
    Task ActualizarAsync(Usuario usuario);
    Task CambiarEstadoAsync(int empresaId, int id, string estado);
    Task ActualizarPasswordAsync(int empresaId, int id, string passwordHash);
}

public interface IRolRepository
{
    Task<RolCatalogo?> ObtenerPorCodigoAsync(string codigo);
    Task<IReadOnlyList<RolCatalogo>> ListarAsync();
}
