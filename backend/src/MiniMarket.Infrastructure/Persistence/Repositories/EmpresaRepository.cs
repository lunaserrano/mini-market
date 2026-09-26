using System.Data;
using Dapper;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Infrastructure.Persistence.Repositories;

public class EmpresaRepository : IEmpresaRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public EmpresaRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Empresa?> ObtenerPorIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<Empresa>(
            "market.usp_Empresa_ObtenerPorId", new { id }, commandType: CommandType.StoredProcedure);
    }

    public async Task ActualizarAsync(Empresa empresa)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync("market.usp_Empresa_Actualizar", new
        {
            empresa.Id, empresa.Nombre, empresa.RazonSocial, empresa.IdentificacionFiscal,
            empresa.ZonaHoraria, empresa.CodigoMoneda, empresa.SimboloMoneda, empresa.TasaImpuesto
        }, commandType: CommandType.StoredProcedure);
    }

    public async Task<int> CrearAsync(string nombre, string? razonSocial, string zonaHoraria, string estado)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleAsync<int>("market.usp_Empresa_Crear",
            new { Nombre = nombre, RazonSocial = razonSocial, ZonaHoraria = zonaHoraria, Estado = estado },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<int> ContarTodasAsync()
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleAsync<int>("market.usp_Empresa_ContarTodas", commandType: CommandType.StoredProcedure);
    }
}

public class SucursalRepository : ISucursalRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    public SucursalRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<int> CrearAsync(int empresaId, string nombre, string? direccion, string estado)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleAsync<int>("market.usp_Sucursal_Crear",
            new { EmpresaId = empresaId, Nombre = nombre, Direccion = direccion, Estado = estado },
            commandType: CommandType.StoredProcedure);
    }
}
