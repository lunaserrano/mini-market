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

    private static DataTable CodigosATabla(IEnumerable<string> codigos)
    {
        var tabla = new DataTable();
        tabla.Columns.Add("Codigo", typeof(string));
        foreach (var codigo in codigos.Distinct())
            tabla.Rows.Add(codigo);
        return tabla;
    }

    public async Task<IReadOnlyList<RolDto>> ListarAsync(int empresaId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        // El admin no tiene filas en RolPermiso: su total es el del catálogo completo.
        var roles = await connection.QueryAsync<RolDto>(
            "market.usp_Rol_Listar", new { empresaId, totalCatalogo = Permisos.Todos.Count }, commandType: CommandType.StoredProcedure);
        return roles.AsList();
    }

    public async Task<RolCatalogo?> ObtenerPorIdAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<RolCatalogo>(
            "market.usp_Rol_ObtenerPorId", new { empresaId, id }, commandType: CommandType.StoredProcedure);
    }

    public async Task<IReadOnlyList<string>> ObtenerCodigosPermisosAsync(int rolId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        var codigos = await connection.QueryAsync<string>(
            "market.usp_Rol_ObtenerCodigosPermisos", new { rolId }, commandType: CommandType.StoredProcedure);
        return codigos.AsList();
    }

    public async Task<int> CrearAsync(RolCatalogo rol, IReadOnlyCollection<string> permisos)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        var parametros = new DynamicParameters();
        parametros.Add("@EmpresaId", rol.EmpresaId);
        parametros.Add("@Codigo", rol.Codigo);
        parametros.Add("@Nombre", rol.Nombre);
        parametros.Add("@Descripcion", rol.Descripcion);
        parametros.Add("@EsSistema", rol.EsSistema);
        parametros.Add("@Estado", rol.Estado);
        parametros.Add("@CreadoPorUsuarioId", rol.CreadoPorUsuarioId);
        parametros.Add("@FechaCreacion", rol.FechaCreacion);
        parametros.Add("@Permisos", CodigosATabla(permisos).AsTableValuedParameter("market.CodigoListType"));

        return await connection.QuerySingleAsync<int>("market.usp_Rol_Crear", parametros, commandType: CommandType.StoredProcedure);
    }

    public async Task ActualizarAsync(RolCatalogo rol, IReadOnlyCollection<string>? permisos)
    {
        using var connection = _connectionFactory.CreateOpenConnection();

        var parametros = new DynamicParameters();
        parametros.Add("@Id", rol.Id);
        parametros.Add("@EmpresaId", rol.EmpresaId);
        parametros.Add("@Nombre", rol.Nombre);
        parametros.Add("@Descripcion", rol.Descripcion);
        parametros.Add("@ModificadoPorUsuarioId", rol.ModificadoPorUsuarioId);
        parametros.Add("@FechaModificacion", rol.FechaModificacion);
        parametros.Add("@ActualizarPermisos", permisos is not null);
        parametros.Add("@Permisos", CodigosATabla(permisos ?? Array.Empty<string>()).AsTableValuedParameter("market.CodigoListType"));

        await connection.ExecuteAsync("market.usp_Rol_Actualizar", parametros, commandType: CommandType.StoredProcedure);
    }

    public async Task EliminarAsync(int empresaId, int id)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync("market.usp_Rol_Eliminar", new { empresaId, id }, commandType: CommandType.StoredProcedure);
    }

    public async Task<int> ContarUsuariosAsync(int empresaId, int rolId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        return await connection.QuerySingleAsync<int>(
            "market.usp_Rol_ContarUsuarios", new { empresaId, rolId }, commandType: CommandType.StoredProcedure);
    }
}
