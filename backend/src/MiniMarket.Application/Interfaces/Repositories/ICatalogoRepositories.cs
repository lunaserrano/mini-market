using MiniMarket.Domain.Entities;

namespace MiniMarket.Application.Interfaces.Repositories;

public interface ICategoriaRepository
{
    Task<IReadOnlyList<Categoria>> ListarAsync(int empresaId);
    Task<Categoria?> ObtenerPorIdAsync(int empresaId, int id);
    Task<int> CrearAsync(Categoria categoria);
    Task ActualizarAsync(Categoria categoria);
    Task CambiarEstadoAsync(int empresaId, int id, string estado);
}

public interface IProveedorRepository
{
    Task<IReadOnlyList<Proveedor>> ListarAsync(int empresaId);
    Task<Proveedor?> ObtenerPorIdAsync(int empresaId, int id);
    Task<int> CrearAsync(Proveedor proveedor);
    Task ActualizarAsync(Proveedor proveedor);
    Task CambiarEstadoAsync(int empresaId, int id, string estado);
}

public interface IClienteRepository
{
    Task<IReadOnlyList<Cliente>> ListarAsync(int empresaId);
    Task<Cliente?> ObtenerPorIdAsync(int empresaId, int id);
    Task<int> CrearAsync(Cliente cliente);
    Task ActualizarAsync(Cliente cliente);
    Task CambiarEstadoAsync(int empresaId, int id, string estado);
}
