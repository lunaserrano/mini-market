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
        return await connection.QuerySingleOrDefaultAsync<Empresa>("SELECT * FROM Empresa WHERE Id = @id", new { id });
    }

    public async Task ActualizarAsync(Empresa empresa)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        const string sql = """
            UPDATE Empresa SET Nombre = @Nombre, RazonSocial = @RazonSocial, IdentificacionFiscal = @IdentificacionFiscal,
                ZonaHoraria = @ZonaHoraria, CodigoMoneda = @CodigoMoneda, SimboloMoneda = @SimboloMoneda, TasaImpuesto = @TasaImpuesto
            WHERE Id = @Id
            """;
        await connection.ExecuteAsync(sql, empresa);
    }
}
