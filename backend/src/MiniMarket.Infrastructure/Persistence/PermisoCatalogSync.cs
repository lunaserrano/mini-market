using Dapper;
using MiniMarket.Domain.Security;

namespace MiniMarket.Infrastructure.Persistence;

/// <summary>
/// Sincroniza la tabla Permiso con el catálogo definido en código (Domain/Security/Permisos.cs) en cada
/// arranque, en cualquier entorno: agrega los permisos nuevos y corrige nombre/módulo de los existentes.
/// Así agregar un permiso no exige otra migración SQL, y el rol admin lo recibe solo (siempre tiene todos).
/// No borra filas: un permiso retirado del código queda huérfano en la tabla y PermisoService lo ignora.
/// </summary>
public static class PermisoCatalogSync
{
    public static async Task SyncAsync(IDbConnectionFactory connectionFactory)
    {
        using var connection = connectionFactory.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var permiso in Permisos.Catalogo)
        {
            await connection.ExecuteAsync("""
                MERGE Permiso AS destino
                USING (SELECT @Codigo AS Codigo, @Modulo AS Modulo, @Nombre AS Nombre) AS origen
                    ON destino.Codigo = origen.Codigo
                WHEN MATCHED AND (destino.Modulo <> origen.Modulo OR destino.Nombre <> origen.Nombre) THEN
                    UPDATE SET Modulo = origen.Modulo, Nombre = origen.Nombre
                WHEN NOT MATCHED THEN
                    INSERT (Codigo, Modulo, Nombre) VALUES (origen.Codigo, origen.Modulo, origen.Nombre);
                """, permiso, transaction);
        }

        transaction.Commit();
    }
}
