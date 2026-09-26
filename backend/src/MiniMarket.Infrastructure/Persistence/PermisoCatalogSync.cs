using System.Data;
using Dapper;
using MiniMarket.Domain.Security;

namespace MiniMarket.Infrastructure.Persistence;

/// <summary>
/// Sincroniza la tabla market.Permiso con el catálogo definido en código (Domain/Security/Permisos.cs) en
/// cada arranque, en cualquier entorno: agrega los permisos nuevos y corrige nombre/módulo de los
/// existentes. Así agregar un permiso no exige otra migración SQL, y el rol admin lo recibe solo
/// (siempre tiene todos). No borra filas: un permiso retirado del código queda huérfano en la tabla y
/// PermisoService lo ignora. Todo el trabajo lo hace market.usp_Permiso_SincronizarLote en un único
/// MERGE por lote (nunca SQL de aplicación suelto contra las tablas).
/// </summary>
public static class PermisoCatalogSync
{
    public static async Task SyncAsync(IDbConnectionFactory connectionFactory)
    {
        var tabla = new DataTable();
        tabla.Columns.Add("Codigo", typeof(string));
        tabla.Columns.Add("Modulo", typeof(string));
        tabla.Columns.Add("Nombre", typeof(string));
        foreach (var permiso in Permisos.Catalogo)
            tabla.Rows.Add(permiso.Codigo, permiso.Modulo, permiso.Nombre);

        var parametros = new DynamicParameters();
        parametros.Add("@Permisos", tabla.AsTableValuedParameter("market.PermisoListType"));

        using var connection = connectionFactory.CreateOpenConnection();
        await connection.ExecuteAsync("market.usp_Permiso_SincronizarLote", parametros, commandType: CommandType.StoredProcedure);
    }
}
