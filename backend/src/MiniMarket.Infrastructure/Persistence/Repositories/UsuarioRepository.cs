using Dapper;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Infrastructure.Persistence.Repositories;

public class UsuarioRepository : IUsuarioRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UsuarioRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Usuario?> ObtenerPorUsernameAsync(int empresaId, string username)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<Usuario>(
            "SELECT * FROM Usuario WHERE EmpresaId = @empresaId AND Username = @username",
            new { empresaId, username });
    }

    public async Task<Usuario?> ObtenerPorIdAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<Usuario>(
            "SELECT * FROM Usuario WHERE EmpresaId = @empresaId AND Id = @id",
            new { empresaId, id });
    }

    public async Task<string> ObtenerCodigoRolAsync(int rolId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleAsync<string>(
            "SELECT Codigo FROM RolCatalogo WHERE Id = @rolId", new { rolId });
    }

    public async Task<IReadOnlyList<Usuario>> ListarAsync(int empresaId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        var usuarios = await connection.QueryAsync<Usuario>(
            "SELECT * FROM Usuario WHERE EmpresaId = @empresaId ORDER BY NombreCompleto", new { empresaId });
        return usuarios.AsList();
    }

    public async Task<int> CrearAsync(Usuario usuario)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        const string sql = """
            INSERT INTO Usuario (EmpresaId, SucursalId, RolId, NombreCompleto, Username, PasswordHash, Estado, CreadoPorUsuarioId, FechaCreacion)
            OUTPUT INSERTED.Id
            VALUES (@EmpresaId, @SucursalId, @RolId, @NombreCompleto, @Username, @PasswordHash, @Estado, @CreadoPorUsuarioId, @FechaCreacion)
            """;
        return await connection.QuerySingleAsync<int>(sql, usuario);
    }

    public async Task ActualizarAsync(Usuario usuario)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        const string sql = """
            UPDATE Usuario SET SucursalId = @SucursalId, RolId = @RolId, NombreCompleto = @NombreCompleto,
                ModificadoPorUsuarioId = @ModificadoPorUsuarioId, FechaModificacion = @FechaModificacion
            WHERE Id = @Id AND EmpresaId = @EmpresaId
            """;
        await connection.ExecuteAsync(sql, usuario);
    }

    public async Task CambiarEstadoAsync(int empresaId, int id, string estado)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync(
            "UPDATE Usuario SET Estado = @estado WHERE Id = @id AND EmpresaId = @empresaId",
            new { empresaId, id, estado });
    }

    public async Task ActualizarPasswordAsync(int empresaId, int id, string passwordHash)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync(
            "UPDATE Usuario SET PasswordHash = @passwordHash WHERE Id = @id AND EmpresaId = @empresaId",
            new { empresaId, id, passwordHash });
    }
}

public class RolRepository : IRolRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RolRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<RolCatalogo?> ObtenerPorCodigoAsync(string codigo)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<RolCatalogo>(
            "SELECT * FROM RolCatalogo WHERE Codigo = @codigo", new { codigo });
    }

    public async Task<IReadOnlyList<RolCatalogo>> ListarAsync()
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        var roles = await connection.QueryAsync<RolCatalogo>("SELECT * FROM RolCatalogo");
        return roles.AsList();
    }
}
