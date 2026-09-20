using Dapper;
using MiniMarket.Application.Interfaces;
using MiniMarket.Domain.Entities;
using MiniMarket.Domain.Security;

namespace MiniMarket.Infrastructure.Persistence;

/// <summary>
/// Siembra datos mínimos de referencia (roles, empresa/sucursal demo, usuario admin) para poder
/// levantar el backend y loguearse de inmediato en desarrollo. Es idempotente: no duplica filas si
/// ya existen. Se invoca desde Program.cs solo en entorno Development.
/// database/seed.sql documenta el mismo contenido (sin el hash de password) como referencia.
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(IDbConnectionFactory connectionFactory, IPasswordHasher passwordHasher)
    {
        using var connection = connectionFactory.CreateOpenConnection();

        var yaExiste = await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM Empresa");
        if (yaExiste > 0) return;

        // CodigoMoneda/SimboloMoneda usan su DEFAULT de columna ('USD'/'$') — El Salvador usa dólar
        // estadounidense como moneda oficial. Cualquier empresa puede cambiarlo luego desde
        // Configuración (GET/PUT /api/empresa) sin tocar código.
        var empresaId = await connection.QuerySingleAsync<int>("""
            INSERT INTO Empresa (Nombre, RazonSocial, ZonaHoraria, Estado)
            OUTPUT INSERTED.Id
            VALUES ('Mini Market Demo', 'Mini Market Demo, S.A. de C.V.', 'America/El_Salvador', 'A')
            """);

        var sucursalId = await connection.QuerySingleAsync<int>("""
            INSERT INTO Sucursal (EmpresaId, Nombre, Direccion, Estado)
            OUTPUT INSERTED.Id
            VALUES (@empresaId, 'Sucursal Principal', 'San Salvador', 'A')
            """, new { empresaId });

        // Roles de sistema por empresa. El admin no lleva filas en RolPermiso (siempre tiene todos los permisos);
        // supervisor y cajero reciben su conjunto por defecto (requiere que PermisoCatalogSync ya haya corrido).
        var rolesBase = new[]
        {
            (Codigo: RolCatalogo.CodigoAdmin, Nombre: "Administrador", Descripcion: "Acceso total al sistema. No editable."),
            (Codigo: RolCatalogo.CodigoSupervisor, Nombre: "Supervisor", Descripcion: "Gestión operativa: catálogo, inventario, compras y anulaciones."),
            (Codigo: RolCatalogo.CodigoCajero, Nombre: "Cajero", Descripcion: "Punto de venta y operación de caja.")
        };
        var rolAdminId = 0;
        foreach (var (codigo, nombre, descripcion) in rolesBase)
        {
            var rolId = await connection.QuerySingleAsync<int>("""
                INSERT INTO RolCatalogo (EmpresaId, Codigo, Nombre, Descripcion, EsSistema)
                OUTPUT INSERTED.Id
                VALUES (@empresaId, @codigo, @nombre, @descripcion, 1)
                """, new { empresaId, codigo, nombre, descripcion });

            if (codigo == RolCatalogo.CodigoAdmin) rolAdminId = rolId;

            if (Permisos.PorDefecto.TryGetValue(codigo, out var permisos))
                await connection.ExecuteAsync(
                    "INSERT INTO RolPermiso (RolId, PermisoId) SELECT @rolId, Id FROM Permiso WHERE Codigo IN @permisos",
                    new { rolId, permisos });
        }

        var adminHash = passwordHasher.Hash("Admin123!");
        await connection.ExecuteAsync("""
            INSERT INTO Usuario (EmpresaId, SucursalId, RolId, NombreCompleto, Username, PasswordHash, Estado)
            VALUES (@empresaId, @sucursalId, @rolAdminId, 'Administrador General', 'admin', @adminHash, 'A')
            """, new { empresaId, sucursalId, rolAdminId, adminHash });

        await connection.ExecuteAsync("""
            INSERT INTO Categoria (EmpresaId, Nombre, Descripcion, Estado) VALUES
            (@empresaId, 'Abarrotes', 'Productos de abarrotes en general', 'A'),
            (@empresaId, 'Bebidas', 'Bebidas embotelladas y enlatadas', 'A'),
            (@empresaId, 'Limpieza', 'Artículos de limpieza del hogar', 'A')
            """, new { empresaId });
    }
}
