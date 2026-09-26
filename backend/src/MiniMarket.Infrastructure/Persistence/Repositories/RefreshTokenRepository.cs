using System.Data;
using Dapper;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RefreshTokenRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> CrearAsync(RefreshToken token)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleAsync<int>("market.usp_RefreshToken_Crear", new
        {
            token.UsuarioId, token.TokenHash, token.FamiliaId, token.CreadoUtc, token.ExpiraUtc, token.Ip
        }, commandType: CommandType.StoredProcedure);
    }

    public async Task<RefreshToken?> ObtenerPorHashAsync(string tokenHash)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<RefreshToken>(
            "market.usp_RefreshToken_ObtenerPorHash", new { tokenHash }, commandType: CommandType.StoredProcedure);
    }

    public async Task<bool> RevocarAsync(int id, DateTime ahoraUtc)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        var filas = await connection.QuerySingleAsync<int>(
            "market.usp_RefreshToken_Revocar", new { id, ahoraUtc }, commandType: CommandType.StoredProcedure);
        return filas > 0;
    }

    public async Task MarcarReemplazoAsync(int id, int reemplazadoPorId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync(
            "market.usp_RefreshToken_MarcarReemplazo", new { id, reemplazadoPorId }, commandType: CommandType.StoredProcedure);
    }

    public async Task RevocarFamiliaAsync(Guid familiaId, DateTime ahoraUtc)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync(
            "market.usp_RefreshToken_RevocarFamilia", new { familiaId, ahoraUtc }, commandType: CommandType.StoredProcedure);
    }

    public async Task RevocarTodosDeUsuarioAsync(int usuarioId, DateTime ahoraUtc)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync(
            "market.usp_RefreshToken_RevocarTodosDeUsuario", new { usuarioId, ahoraUtc }, commandType: CommandType.StoredProcedure);
    }

    public async Task PurgarAntiguosAsync(int usuarioId, DateTime antesDeUtc)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync(
            "market.usp_RefreshToken_PurgarAntiguos", new { usuarioId, antesDeUtc }, commandType: CommandType.StoredProcedure);
    }
}
