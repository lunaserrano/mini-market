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
        return await connection.QuerySingleAsync<int>("""
            INSERT INTO RefreshToken (UsuarioId, TokenHash, FamiliaId, CreadoUtc, ExpiraUtc, Ip)
            OUTPUT INSERTED.Id
            VALUES (@UsuarioId, @TokenHash, @FamiliaId, @CreadoUtc, @ExpiraUtc, @Ip)
            """, token);
    }

    public async Task<RefreshToken?> ObtenerPorHashAsync(string tokenHash)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<RefreshToken>(
            "SELECT * FROM RefreshToken WHERE TokenHash = @tokenHash", new { tokenHash });
    }

    public async Task<bool> RevocarAsync(int id, DateTime ahoraUtc)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        // "AND RevocadoUtc IS NULL" hace la rotación atómica: solo una petición concurrente gana.
        var filas = await connection.ExecuteAsync(
            "UPDATE RefreshToken SET RevocadoUtc = @ahoraUtc WHERE Id = @id AND RevocadoUtc IS NULL",
            new { id, ahoraUtc });
        return filas > 0;
    }

    public async Task MarcarReemplazoAsync(int id, int reemplazadoPorId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync(
            "UPDATE RefreshToken SET ReemplazadoPorId = @reemplazadoPorId WHERE Id = @id",
            new { id, reemplazadoPorId });
    }

    public async Task RevocarFamiliaAsync(Guid familiaId, DateTime ahoraUtc)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync(
            "UPDATE RefreshToken SET RevocadoUtc = @ahoraUtc WHERE FamiliaId = @familiaId AND RevocadoUtc IS NULL",
            new { familiaId, ahoraUtc });
    }

    public async Task RevocarTodosDeUsuarioAsync(int usuarioId, DateTime ahoraUtc)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync(
            "UPDATE RefreshToken SET RevocadoUtc = @ahoraUtc WHERE UsuarioId = @usuarioId AND RevocadoUtc IS NULL",
            new { usuarioId, ahoraUtc });
    }

    public async Task PurgarAntiguosAsync(int usuarioId, DateTime antesDeUtc)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync("""
            DELETE FROM RefreshToken
            WHERE UsuarioId = @usuarioId AND (ExpiraUtc < @antesDeUtc OR RevocadoUtc < @antesDeUtc)
            """, new { usuarioId, antesDeUtc });
    }
}
