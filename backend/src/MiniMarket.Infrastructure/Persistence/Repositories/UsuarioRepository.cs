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

    public async Task<Usuario?> ObtenerParaSesionAsync(int usuarioId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<Usuario>(
            "SELECT * FROM Usuario WHERE Id = @usuarioId", new { usuarioId });
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
            INSERT INTO Usuario (EmpresaId, SucursalId, RolId, NombreCompleto, Username, PasswordHash, Estado, DebeCambiarPassword, CreadoPorUsuarioId, FechaCreacion)
            OUTPUT INSERTED.Id
            VALUES (@EmpresaId, @SucursalId, @RolId, @NombreCompleto, @Username, @PasswordHash, @Estado, @DebeCambiarPassword, @CreadoPorUsuarioId, @FechaCreacion)
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

    public async Task CambiarEstadoAsync(int empresaId, int id, string estado, int modificadoPorUsuarioId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync("""
            UPDATE Usuario SET Estado = @estado, ModificadoPorUsuarioId = @modificadoPorUsuarioId, FechaModificacion = SYSUTCDATETIME()
            WHERE Id = @id AND EmpresaId = @empresaId
            """, new { empresaId, id, estado, modificadoPorUsuarioId });
    }

    public async Task ActualizarPasswordAsync(int empresaId, int id, string passwordHash, bool debeCambiarPassword, int? modificadoPorUsuarioId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync("""
            UPDATE Usuario SET PasswordHash = @passwordHash, DebeCambiarPassword = @debeCambiarPassword,
                PasswordCambiadaUtc = SYSUTCDATETIME(), IntentosFallidos = 0, BloqueadoHasta = NULL,
                ModificadoPorUsuarioId = @modificadoPorUsuarioId, FechaModificacion = SYSUTCDATETIME()
            WHERE Id = @id AND EmpresaId = @empresaId
            """, new { empresaId, id, passwordHash, debeCambiarPassword, modificadoPorUsuarioId });
    }

    public async Task<DateTime?> RegistrarIntentoFallidoAsync(int empresaId, int id, int maxIntentos, DateTime bloqueoHastaUtc)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        // Una sola sentencia: el incremento y la decisión de bloquear son atómicos aunque lleguen logins concurrentes.
        // Al bloquear, el contador vuelve a 0 para que al vencer el bloqueo la persona tenga intentos nuevos.
        return await connection.QuerySingleOrDefaultAsync<DateTime?>("""
            UPDATE Usuario SET
                IntentosFallidos = CASE WHEN IntentosFallidos + 1 >= @maxIntentos THEN 0 ELSE IntentosFallidos + 1 END,
                BloqueadoHasta   = CASE WHEN IntentosFallidos + 1 >= @maxIntentos THEN @bloqueoHastaUtc ELSE NULL END
            OUTPUT INSERTED.BloqueadoHasta
            WHERE Id = @id AND EmpresaId = @empresaId
            """, new { empresaId, id, maxIntentos, bloqueoHastaUtc });
    }

    public async Task RegistrarLoginOkAsync(int empresaId, int id, DateTime ahoraUtc)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync("""
            UPDATE Usuario SET IntentosFallidos = 0, BloqueadoHasta = NULL, UltimoLoginUtc = @ahoraUtc
            WHERE Id = @id AND EmpresaId = @empresaId
            """, new { empresaId, id, ahoraUtc });
    }

    public async Task DesbloquearAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync(
            "UPDATE Usuario SET IntentosFallidos = 0, BloqueadoHasta = NULL WHERE Id = @id AND EmpresaId = @empresaId",
            new { empresaId, id });
    }

    public async Task<int> ContarAdministradoresActivosAsync(int empresaId, int? excluirUsuarioId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleAsync<int>("""
            SELECT COUNT(*)
            FROM Usuario u
            JOIN RolCatalogo r ON r.Id = u.RolId
            WHERE u.EmpresaId = @empresaId AND u.Estado = 'A'
              AND r.EsSistema = 1 AND r.Codigo = 'admin'
              AND (@excluirUsuarioId IS NULL OR u.Id <> @excluirUsuarioId)
            """, new { empresaId, excluirUsuarioId });
    }

    public async Task<bool> ExisteSucursalAsync(int empresaId, int sucursalId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM Sucursal WHERE Id = @sucursalId AND EmpresaId = @empresaId",
            new { empresaId, sucursalId }) > 0;
    }
}
