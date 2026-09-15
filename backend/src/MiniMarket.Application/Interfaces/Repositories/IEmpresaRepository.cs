using MiniMarket.Domain.Entities;

namespace MiniMarket.Application.Interfaces.Repositories;

public interface IEmpresaRepository
{
    Task<Empresa?> ObtenerPorIdAsync(int id);
    Task ActualizarAsync(Empresa empresa);
}
