using MiniMarket.Domain.Entities;

namespace MiniMarket.Application.Interfaces.Repositories;

public interface IEmpresaRepository
{
    Task<Empresa?> ObtenerPorIdAsync(int id);
    Task ActualizarAsync(Empresa empresa);
    /// <summary>Solo para el seed de desarrollo (DataSeeder): crea la empresa demo inicial.</summary>
    Task<int> CrearAsync(string nombre, string? razonSocial, string zonaHoraria, string estado);
    /// <summary>Empresas existentes en la base (0 = base recién creada, dispara el seed de desarrollo).</summary>
    Task<int> ContarTodasAsync();
}

public interface ISucursalRepository
{
    /// <summary>Solo para el seed de desarrollo (DataSeeder): crea la sucursal principal de la empresa demo.</summary>
    Task<int> CrearAsync(int empresaId, string nombre, string? direccion, string estado);
}
