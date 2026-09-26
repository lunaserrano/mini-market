using System.Data;
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
            "market.usp_Usuario_ObtenerPorUsername", new { empresaId, username }, commandType: CommandType.StoredProcedure);
    }

    public async Task<Usuario?> ObtenerPorIdAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<Usuario>(
            "market.usp_Usuario_ObtenerPorId", new { empresaId, id }, commandType: CommandType.StoredProcedure);
    }

    public async Task<Usuario?> ObtenerParaSesionAsync(int usuarioId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<Usuario>(
            "market.usp_Usuario_ObtenerParaSesion", new { usuarioId }, commandType: CommandType.StoredProcedure);
    }

    public async Task<IReadOnlyList<Usuario>> ListarAsync(int empresaId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        var usuarios = await connection.QueryAsync<Usuario>(
            "market.usp_Usuario_Listar", new { empresaId }, commandType: CommandType.StoredProcedure);
        return usuarios.AsList();
    }

    public async Task<int> CrearAsync(Usuario usuario)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleAsync<int>("market.usp_Usuario_Crear", new
        {
            usuario.EmpresaId, usuario.SucursalId, usuario.RolId, usuario.NombreCompleto, usuario.Username,
            usuario.PasswordHash, usuario.Estado, usuario.DebeCambiarPassword, usuario.CreadoPorUsuarioId, usuario.FechaCreacion
        }, commandType: CommandType.StoredProcedure);
    }

    public async Task ActualizarAsync(Usuario usuario)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync("market.usp_Usuario_Actualizar", new
        {
            usuario.Id, usuario.EmpresaId, usuario.SucursalId, usuario.RolId, usuario.NombreCompleto,
            usuario.ModificadoPorUsuarioId, usuario.FechaModificacion
        }, commandType: CommandType.StoredProcedure);
    }

    public async Task CambiarEstadoAsync(int empresaId, int id, string estado, int modificadoPorUsuarioId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync("market.usp_Usuario_CambiarEstado",
            new { empresaId, id, estado, modificadoPorUsuarioId }, commandType: CommandType.StoredProcedure);
    }

    public async Task ActualizarPasswordAsync(int empresaId, int id, string passwordHash, bool debeCambiarPassword, int? modificadoPorUsuarioId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync("market.usp_Usuario_ActualizarPassword",
            new { empresaId, id, passwordHash, debeCambiarPassword, modificadoPorUsuarioId }, commandType: CommandType.StoredProcedure);
    }

    public async Task<DateTime?> RegistrarIntentoFallidoAsync(int empresaId, int id, int maxIntentos, DateTime bloqueoHastaUtc)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<DateTime?>("market.usp_Usuario_RegistrarIntentoFallido",
            new { empresaId, id, maxIntentos, bloqueoHastaUtc }, commandType: CommandType.StoredProcedure);
    }

    public async Task RegistrarLoginOkAsync(int empresaId, int id, DateTime ahoraUtc)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync("market.usp_Usuario_RegistrarLoginOk",
            new { empresaId, id, ahoraUtc }, commandType: CommandType.StoredProcedure);
    }

    public async Task DesbloquearAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync(
            "market.usp_Usuario_Desbloquear", new { empresaId, id }, commandType: CommandType.StoredProcedure);
    }

    public async Task<int> ContarAdministradoresActivosAsync(int empresaId, int? excluirUsuarioId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleAsync<int>("market.usp_Usuario_ContarAdministradoresActivos",
            new { empresaId, excluirUsuarioId }, commandType: CommandType.StoredProcedure);
    }

    public async Task<bool> ExisteSucursalAsync(int empresaId, int sucursalId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleAsync<int>(
            "market.usp_Sucursal_Existe", new { empresaId, sucursalId }, commandType: CommandType.StoredProcedure) > 0;
    }
}
