using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;
using MiniMarket.Domain.Security;

namespace MiniMarket.Infrastructure.Persistence;

/// <summary>
/// Siembra datos mínimos de referencia (roles, empresa/sucursal demo, usuario admin) para poder
/// levantar el backend y loguearse de inmediato en desarrollo. Es idempotente: no duplica filas si
/// ya existen. Se invoca desde Program.cs solo en entorno Development.
/// Pasa por los mismos repositorios (y por lo tanto los mismos stored procedures de market) que usa
/// el resto de la Api: nunca ejecuta SQL directo. database/schema/market/12_seed_demo.sql documenta
/// el mismo contenido (sin el hash de password) como script independiente.
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(
        IEmpresaRepository empresaRepository,
        ISucursalRepository sucursalRepository,
        IRolRepository rolRepository,
        IUsuarioRepository usuarioRepository,
        ICategoriaRepository categoriaRepository,
        IPasswordHasher passwordHasher)
    {
        if (await empresaRepository.ContarTodasAsync() > 0) return;

        // CodigoMoneda/SimboloMoneda/TasaImpuesto usan su DEFAULT de columna (USD/$/13%) — El Salvador
        // usa dólar estadounidense e IVA del 13%. Cualquier empresa puede cambiarlo luego desde
        // Configuración (GET/PUT /api/empresa) sin tocar código.
        var empresaId = await empresaRepository.CrearAsync(
            "Mini Market Demo", "Mini Market Demo, S.A. de C.V.", "America/El_Salvador", "A");

        var sucursalId = await sucursalRepository.CrearAsync(empresaId, "Sucursal Principal", "San Salvador", "A");

        // Roles de sistema por empresa. El admin no lleva filas en RolPermiso (siempre tiene todos los
        // permisos); supervisor y cajero reciben su conjunto por defecto.
        var rolesBase = new[]
        {
            (Codigo: RolCatalogo.CodigoAdmin, Nombre: "Administrador", Descripcion: "Acceso total al sistema. No editable."),
            (Codigo: RolCatalogo.CodigoSupervisor, Nombre: "Supervisor", Descripcion: "Gestión operativa: catálogo, inventario, compras y anulaciones."),
            (Codigo: RolCatalogo.CodigoCajero, Nombre: "Cajero", Descripcion: "Punto de venta y operación de caja.")
        };
        var rolAdminId = 0;
        foreach (var (codigo, nombre, descripcion) in rolesBase)
        {
            var permisos = Permisos.PorDefecto.TryGetValue(codigo, out var porDefecto)
                ? porDefecto
                : Array.Empty<string>();

            var rol = new RolCatalogo
            {
                EmpresaId = empresaId,
                Codigo = codigo,
                Nombre = nombre,
                Descripcion = descripcion,
                EsSistema = true,
                Estado = "A",
                FechaCreacion = DateTime.UtcNow
            };
            var rolId = await rolRepository.CrearAsync(rol, permisos);

            if (codigo == RolCatalogo.CodigoAdmin) rolAdminId = rolId;
        }

        var admin = new Usuario
        {
            EmpresaId = empresaId,
            SucursalId = sucursalId,
            RolId = rolAdminId,
            NombreCompleto = "Administrador General",
            Username = "admin",
            PasswordHash = passwordHasher.Hash("Admin123!"),
            Estado = "A",
            DebeCambiarPassword = false,
            FechaCreacion = DateTime.UtcNow
        };
        await usuarioRepository.CrearAsync(admin);

        var categorias = new[]
        {
            ("Abarrotes", "Productos de abarrotes en general"),
            ("Bebidas", "Bebidas embotelladas y enlatadas"),
            ("Limpieza", "Artículos de limpieza del hogar")
        };
        foreach (var (nombre, descripcion) in categorias)
        {
            await categoriaRepository.CrearAsync(new Categoria
            {
                EmpresaId = empresaId,
                Nombre = nombre,
                Descripcion = descripcion,
                Estado = "A",
                FechaCreacion = DateTime.UtcNow
            });
        }
    }
}
