using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Options;
using MiniMarket.Api.Authorization;
using MiniMarket.Api.Controllers;
using MiniMarket.Domain.Security;
using MiniMarket.Infrastructure.Services;

namespace MiniMarket.Tests;

public class PermissionHandlerTests
{
    private static async Task<bool> Evaluar(ClaimsPrincipal usuario, params string[] requeridos)
    {
        var requirement = new PermissionRequirement(requeridos);
        var context = new AuthorizationHandlerContext(new[] { requirement }, usuario, null);
        await new PermissionHandler().HandleAsync(context);
        return context.HasSucceeded;
    }

    private static ClaimsPrincipal Usuario(IEnumerable<string> permisos, bool cambioForzado = false)
    {
        var claims = permisos.Select(p => new Claim(TenantClaimTypes.Permiso, p)).ToList();
        if (cambioForzado) claims.Add(new Claim(TenantClaimTypes.DebeCambiarPassword, "true"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public async Task ConElPermisoRequerido_Autoriza() =>
        Assert.True(await Evaluar(Usuario(new[] { Permisos.VentasVer }), Permisos.VentasVer));

    [Fact]
    public async Task SinElPermisoRequerido_NoAutoriza() =>
        Assert.False(await Evaluar(Usuario(new[] { Permisos.VentasVer }), Permisos.VentasAnular));

    [Fact]
    public async Task SinNingunPermiso_NoAutoriza() =>
        Assert.False(await Evaluar(Usuario(Array.Empty<string>()), Permisos.VentasVer));

    [Fact]
    public async Task ConVariosRequeridos_BastaConUnoDeEllos() =>
        Assert.True(await Evaluar(Usuario(new[] { Permisos.UsuariosCrear }), Permisos.RolesVer, Permisos.UsuariosCrear, Permisos.UsuariosEditar));

    [Fact]
    public async Task ElNombreDelPermisoSeCompararExacto_UnPrefijoNoAutoriza() =>
        Assert.False(await Evaluar(Usuario(new[] { "ventas" }), Permisos.VentasVer));

    [Fact]
    public async Task MientrasDebaCambiarLaContrasena_NingunPermisoAutoriza() =>
        Assert.False(await Evaluar(Usuario(Permisos.Todos, cambioForzado: true), Permisos.VentasVer));

    [Fact]
    public async Task ElProveedorDePoliticasConstruyeLaPoliticaDesdeElNombre()
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

        var policy = await provider.GetPolicyAsync("perm:roles.ver|usuarios.crear");

        Assert.NotNull(policy);
        var requirement = Assert.Single(policy!.Requirements.OfType<PermissionRequirement>());
        Assert.Equal(new[] { "roles.ver", "usuarios.crear" }, requirement.Permisos);
    }

    [Fact]
    public async Task ElProveedorDePoliticasDejaPasarLasPoliticasAjenas()
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

        Assert.Null(await provider.GetPolicyAsync("otra-politica"));
    }
}

public class CatalogoDePermisosTests
{
    private static readonly string[] ConstantesDePermisos = typeof(Permisos)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToArray();

    [Fact]
    public void TodaConstanteTieneEntradaEnElCatalogo_YViceversa()
    {
        Assert.Equal(ConstantesDePermisos.OrderBy(x => x), Permisos.Todos.OrderBy(x => x));
    }

    [Fact]
    public void LosCodigosSonUnicosYTienenFormatoModuloAccion()
    {
        Assert.Equal(Permisos.Todos.Count, Permisos.Todos.Distinct().Count());
        Assert.All(Permisos.Todos, codigo => Assert.Matches(new Regex("^[a-z]+\\.[a-z_]+$"), codigo));
    }

    [Fact]
    public void CadaPermisoDelCatalogoTieneModuloYNombreLegible()
    {
        Assert.All(Permisos.Catalogo, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Modulo));
            Assert.False(string.IsNullOrWhiteSpace(p.Nombre));
        });
    }

    [Fact]
    public void LosPermisosPorDefectoExistenEnElCatalogoYNoIncluyenAlAdmin()
    {
        Assert.DoesNotContain("admin", Permisos.PorDefecto.Keys);
        Assert.All(Permisos.PorDefecto.Values.SelectMany(v => v), p => Assert.True(Permisos.Existe(p), $"'{p}' no está en el catálogo"));
    }

    [Fact]
    public void ElCajeroPorDefectoNoPuedeGestionarNiVerLoAjeno()
    {
        var cajero = Permisos.PorDefecto["cajero"];

        Assert.DoesNotContain(Permisos.VentasVerTodas, cajero);
        Assert.DoesNotContain(Permisos.VentasAnular, cajero);
        Assert.DoesNotContain(Permisos.UsuariosVer, cajero);
        Assert.DoesNotContain(Permisos.RolesGestionar, cajero);
    }

    [Fact]
    public void LosTiposDeEventoTienenListaCompletaYSinDuplicados()
    {
        var constantes = typeof(TipoEventoSeguridad).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToArray();

        Assert.Equal(constantes.OrderBy(x => x), TipoEventoSeguridad.Todos.OrderBy(x => x));
        Assert.Equal(constantes.Length, constantes.Distinct().Count());
    }
}

/// <summary>
/// Red de seguridad estructural: impide que un endpoint nuevo quede sin protección por olvido y que
/// se vuelva a usar la autorización por nombres de rol fijos.
/// </summary>
public class ProteccionDeControllersTests
{
    private static IEnumerable<Type> Controllers() =>
        typeof(AuthController).Assembly.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

    private static IEnumerable<(Type Controller, MethodInfo Accion)> Acciones() =>
        Controllers().SelectMany(c => c
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes().OfType<HttpMethodAttribute>().Any())
            .Select(m => (c, m)));

    [Fact]
    public void SeEncuentranControllersYAcciones()
    {
        Assert.NotEmpty(Controllers());
        Assert.True(Acciones().Count() > 30);
    }

    [Fact]
    public void TodaAccionDeclaraExplicitamenteSuAutorizacion()
    {
        var sinProteccion = Acciones()
            .Where(x =>
            {
                var atributos = x.Accion.GetCustomAttributes(true).Concat(x.Controller.GetCustomAttributes(true)).ToArray();
                return !atributos.OfType<IAuthorizeData>().Any() && !atributos.OfType<IAllowAnonymous>().Any();
            })
            .Select(x => $"{x.Controller.Name}.{x.Accion.Name}")
            .ToList();

        Assert.True(sinProteccion.Count == 0, "Acciones sin [HasPermission]/[Authorize]/[AllowAnonymous]: " + string.Join(", ", sinProteccion));
    }

    [Fact]
    public void NingunEndpointUsaAutorizacionPorNombreDeRol()
    {
        var conRoles = Controllers()
            .SelectMany(c => c.GetCustomAttributes<AuthorizeAttribute>().Select(a => (Nombre: c.Name, Atributo: a))
                .Concat(c.GetMethods().SelectMany(m => m.GetCustomAttributes<AuthorizeAttribute>().Select(a => (Nombre: $"{c.Name}.{m.Name}", Atributo: a)))))
            .Where(x => !string.IsNullOrEmpty(x.Atributo.Roles))
            .Select(x => x.Nombre)
            .ToList();

        Assert.True(conRoles.Count == 0, "Usan [Authorize(Roles=...)]: " + string.Join(", ", conRoles));
    }

    [Fact]
    public void TodoPermisoReferenciadoPorUnEndpointExisteEnElCatalogo()
    {
        var referenciados = Controllers()
            .SelectMany(c => c.GetCustomAttributes<HasPermissionAttribute>()
                .Concat(c.GetMethods().SelectMany(m => m.GetCustomAttributes<HasPermissionAttribute>())))
            .SelectMany(a => a.Permisos)
            .Distinct()
            .ToList();

        Assert.NotEmpty(referenciados);
        Assert.All(referenciados, p => Assert.True(Permisos.Existe(p), $"'{p}' no existe en Permisos.Catalogo"));
    }

    [Fact]
    public void TodoPermisoDelCatalogoProtegeAlMenosUnEndpointOSeUsaEnServicios()
    {
        // Permisos que se evalúan dentro de un servicio y no en un atributo.
        var usadosEnServicios = new[] { Permisos.VentasVerTodas, Permisos.CreditosOtorgar };

        var referenciados = Controllers()
            .SelectMany(c => c.GetCustomAttributes<HasPermissionAttribute>()
                .Concat(c.GetMethods().SelectMany(m => m.GetCustomAttributes<HasPermissionAttribute>())))
            .SelectMany(a => a.Permisos)
            .Concat(usadosEnServicios)
            .ToHashSet();

        var huerfanos = Permisos.Todos.Where(p => !referenciados.Contains(p)).ToList();

        Assert.True(huerfanos.Count == 0, "Permisos que no protegen nada: " + string.Join(", ", huerfanos));
    }
}
