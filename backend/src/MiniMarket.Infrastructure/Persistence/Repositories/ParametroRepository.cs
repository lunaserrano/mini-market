using System.Data;
using Dapper;
using MiniMarket.Application.Interfaces.Repositories;

namespace MiniMarket.Infrastructure.Persistence.Repositories;

public class ParametroRepository : IParametroRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ParametroRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool?> ObtenerAuditoriaHabilitadaAsync(int empresaId, int? sucursalId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<bool?>(
            "market.usp_Parametro_ObtenerAuditoriaHabilitada",
            new { EmpresaId = empresaId, SucursalId = sucursalId },
            commandType: CommandType.StoredProcedure);
    }

    public async Task ActualizarAuditoriaHabilitadaAsync(int empresaId, int sucursalId, bool habilitada, int? modificadoPorUsuarioId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync(
            "market.usp_Parametro_ActualizarAuditoriaHabilitada",
            new { EmpresaId = empresaId, SucursalId = sucursalId, AuditoriaHabilitada = habilitada, ModificadoPorUsuarioId = modificadoPorUsuarioId },
            commandType: CommandType.StoredProcedure);
    }
}
