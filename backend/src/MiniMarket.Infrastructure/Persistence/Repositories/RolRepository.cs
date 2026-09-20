using System.Data;
using Dapper;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;
using MiniMarket.Domain.Security;

namespace MiniMarket.Infrastructure.Persistence.Repositories;

public class RolRepository : IRolRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RolRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<RolDto>> ListarAsync(int empresaId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        // El admin no tiene filas en RolPermiso: su total es el del catálogo completo.
        const string sql = """
            SELECT r.Id, r.Codigo, r.Nombre, r.Descripcion, r.EsSistema,
                   (SELECT COUNT(*) FROM Usuario u WHERE u.RolId = r.Id) AS TotalUsuarios,
                   CASE WHEN r.EsSistema = 1 AND r.Codigo = 'admin' THEN @totalCatalogo
                        ELSE (SELECT COUNT(*) FROM RolPermiso rp WHERE rp.RolId = r.Id) END AS TotalPermisos
            FROM RolCatalogo r
            WHERE r.EmpresaId = @empresaId AND r.Estado = 'A'
            ORDER BY r.EsSistema DESC, r.Nombre
            """;
        var roles = await connection.QueryAsync<RolDto>(sql, new { empresaId, totalCatalogo = Permisos.Todos.Count });
        return roles.AsList();
    }

    public async Task<RolCatalogo?> ObtenerPorIdAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<RolCatalogo>(
            "SELECT * FROM RolCatalogo WHERE EmpresaId = @empresaId AND Id = @id AND Estado = 'A'",
            new { empresaId, id });
    }

    public async Task<IReadOnlyList<string>> ObtenerCodigosPermisosAsync(int rolId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        var codigos = await connection.QueryAsync<string>("""
            SELECT p.Codigo FROM RolPermiso rp JOIN Permiso p ON p.Id = rp.PermisoId
            WHERE rp.RolId = @rolId ORDER BY p.Codigo
            """, new { rolId });
        return codigos.AsList();
    }

    public async Task<int> CrearAsync(RolCatalogo rol, IReadOnlyCollection<string> permisos)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();

        var id = await connection.QuerySingleAsync<int>("""
            INSERT INTO RolCatalogo (EmpresaId, Codigo, Nombre, Descripcion, EsSistema, Estado, CreadoPorUsuarioId, FechaCreacion)
            OUTPUT INSERTED.Id
            VALUES (@EmpresaId, @Codigo, @Nombre, @Descripcion, @EsSistema, @Estado, @CreadoPorUsuarioId, @FechaCreacion)
            """, rol, transaction);

        await InsertarPermisosAsync(connection, transaction, id, permisos);
        transaction.Commit();
        return id;
    }

    public async Task ActualizarAsync(RolCatalogo rol, IReadOnlyCollection<string>? permisos)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync("""
            UPDATE RolCatalogo SET Nombre = @Nombre, Descripcion = @Descripcion,
                ModificadoPorUsuarioId = @ModificadoPorUsuarioId, FechaModificacion = @FechaModificacion
            WHERE Id = @Id AND EmpresaId = @EmpresaId
            """, rol, transaction);

        if (permisos is not null)
        {
            await connection.ExecuteAsync("DELETE FROM RolPermiso WHERE RolId = @Id", new { rol.Id }, transaction);
            await InsertarPermisosAsync(connection, transaction, rol.Id, permisos);
        }

        transaction.Commit();
    }

    public async Task EliminarAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();

        // El filtro por empresa y EsSistema = 0 protege también a nivel de SQL: nunca se borra un rol de sistema ni ajeno.
        await connection.ExecuteAsync("""
            DELETE rp FROM RolPermiso rp JOIN RolCatalogo r ON r.Id = rp.RolId
            WHERE r.Id = @id AND r.EmpresaId = @empresaId AND r.EsSistema = 0
            """, new { empresaId, id }, transaction);
        await connection.ExecuteAsync(
            "DELETE FROM RolCatalogo WHERE Id = @id AND EmpresaId = @empresaId AND EsSistema = 0",
            new { empresaId, id }, transaction);

        transaction.Commit();
    }

    public async Task<int> ContarUsuariosAsync(int empresaId, int rolId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM Usuario WHERE EmpresaId = @empresaId AND RolId = @rolId", new { empresaId, rolId });
    }

    private static Task InsertarPermisosAsync(IDbConnection connection, IDbTransaction transaction, int rolId, IReadOnlyCollection<string> permisos)
    {
        if (permisos.Count == 0) return Task.CompletedTask;
        return connection.ExecuteAsync(
            "INSERT INTO RolPermiso (RolId, PermisoId) SELECT @rolId, Id FROM Permiso WHERE Codigo IN @permisos",
            new { rolId, permisos }, transaction);
    }
}
