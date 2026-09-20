using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using MiniMarket.Domain.Entities;
using MiniMarket.Domain.Exceptions;
using MiniMarket.Domain.Security;
using MiniMarket.Infrastructure.Services;
using MiniMarket.Tests.Helpers;

namespace MiniMarket.Tests;

public class JwtTokenGeneratorTests
{
    private readonly ManualTimeProvider _time = new();

    private JwtTokenGenerator Crear() => new(Options.Create(new JwtSettings
    {
        Issuer = "test", Audience = "test", Key = new string('k', 48), AccessTokenMinutes = 15
    }), _time);

    [Fact]
    public void IncluyeUnClaimPorCadaPermisoYVenceEnLosMinutosConfigurados()
    {
        var usuario = Datos.Usuario(20, rolId: 2);
        var permisos = new[] { Permisos.VentasVer, Permisos.ProductosVer };

        var token = Crear().Generar(usuario, Datos.Rol(), permisos);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.Token);

        Assert.Equal(permisos, jwt.Claims.Where(c => c.Type == TenantClaimTypes.Permiso).Select(c => c.Value));
        Assert.Equal("supervisor", jwt.Claims.Single(c => c.Type == TenantClaimTypes.Rol).Value);
        Assert.Equal("20", jwt.Claims.Single(c => c.Type == TenantClaimTypes.UsuarioId).Value);
        Assert.Equal(_time.AhoraUtc.AddMinutes(15), token.ExpiraUtc);
        Assert.Equal(token.ExpiraUtc, jwt.ValidTo, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ElFlagDeCambioForzadoSoloSeEmiteCuandoAplica()
    {
        var usuario = Datos.Usuario();
        var lector = new JwtSecurityTokenHandler();

        var normal = lector.ReadJwtToken(Crear().Generar(usuario, Datos.Rol(), Array.Empty<string>()).Token);
        usuario.DebeCambiarPassword = true;
        var forzado = lector.ReadJwtToken(Crear().Generar(usuario, Datos.Rol(), Array.Empty<string>()).Token);

        Assert.DoesNotContain(normal.Claims, c => c.Type == TenantClaimTypes.DebeCambiarPassword);
        Assert.Contains(forzado.Claims, c => c.Type == TenantClaimTypes.DebeCambiarPassword && c.Value == "true");
    }
}

public class RefreshTokenGeneratorTests
{
    private readonly RefreshTokenGenerator _sut = new();

    [Fact]
    public void CadaTokenEsDistintoYSuHashEsDeterminista()
    {
        var (t1, h1) = _sut.Generar();
        var (t2, h2) = _sut.Generar();

        Assert.NotEqual(t1, t2);
        Assert.NotEqual(h1, h2);
        Assert.Equal(h1, _sut.Hashear(t1));
    }

    [Fact]
    public void ElHashNoContieneElTokenYCabeEnLaColumna()
    {
        var (token, hash) = _sut.Generar();

        Assert.DoesNotContain(token, hash);
        Assert.Equal(64, hash.Length); // NVARCHAR(64) en RefreshToken.TokenHash
        Assert.True(token.Length >= 80);
        Assert.Matches("^[A-Za-z0-9_-]+$", token); // seguro en JSON y URL
    }
}

public class PasswordHasherTests
{
    private readonly PasswordHasher _sut = new();

    [Fact]
    public void HasheaYVerifica()
    {
        var hash = _sut.Hash("Clave123!");

        Assert.NotEqual("Clave123!", hash);
        Assert.True(_sut.Verify("Clave123!", hash));
        Assert.False(_sut.Verify("otra", hash));
    }

    [Fact]
    public void SimularVerificacion_NoLanzaConCualquierEntrada()
    {
        _sut.SimularVerificacion("lo-que-sea");
        _sut.SimularVerificacion("");
    }
}

public class TenantContextTests
{
    private static TenantContext ConClaims(params (string Tipo, string Valor)[] claims)
    {
        var http = new DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                claims.Select(c => new System.Security.Claims.Claim(c.Tipo, c.Valor)), "test"))
        };
        return new TenantContext(new HttpContextAccessor { HttpContext = http });
    }

    [Fact]
    public void LeeEmpresaUsuarioRolYPermisosDelToken()
    {
        var tenant = ConClaims(
            (TenantClaimTypes.EmpresaId, "3"), (TenantClaimTypes.UsuarioId, "42"), (TenantClaimTypes.Rol, "cajero"),
            (TenantClaimTypes.Permiso, Permisos.VentasVer), (TenantClaimTypes.Permiso, Permisos.CajaOperar));

        Assert.Equal(3, tenant.EmpresaId);
        Assert.Equal(42, tenant.UsuarioId);
        Assert.Equal("cajero", tenant.Rol);
        Assert.Null(tenant.SucursalId);
        Assert.Equal(2, tenant.Permisos.Count);
        Assert.True(tenant.TienePermiso(Permisos.VentasVer));
        Assert.False(tenant.TienePermiso(Permisos.VentasAnular));
    }

    [Fact]
    public void SinClaimsRequeridos_LanzaNoAutenticadoYNoUnErrorGenerico()
    {
        var tenant = ConClaims();

        Assert.Throws<NoAutenticadoException>(() => tenant.EmpresaId);
        Assert.Throws<NoAutenticadoException>(() => tenant.UsuarioId);
        Assert.Throws<NoAutenticadoException>(() => tenant.Rol);
        Assert.Empty(tenant.Permisos);
        Assert.False(tenant.TienePermiso(Permisos.VentasVer));
    }
}

public class EntidadesTests
{
    [Fact]
    public void UsuarioEstaBloqueadoSoloMientrasNoVenzaElBloqueo()
    {
        var ahora = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        var usuario = new Usuario();

        Assert.False(usuario.EstaBloqueado(ahora));
        usuario.BloqueadoHasta = ahora.AddSeconds(1);
        Assert.True(usuario.EstaBloqueado(ahora));
        usuario.BloqueadoHasta = ahora;
        Assert.False(usuario.EstaBloqueado(ahora));
    }

    [Fact]
    public void RefreshTokenExpiraExactamenteEnSuFecha()
    {
        var ahora = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        var token = new RefreshToken { ExpiraUtc = ahora };

        Assert.True(token.HaExpirado(ahora));
        Assert.False(token.HaExpirado(ahora.AddSeconds(-1)));
    }

    [Fact]
    public void SoloElRolDeSistemaConCodigoAdminEsAdministrador()
    {
        Assert.True(new RolCatalogo { Codigo = "admin", EsSistema = true }.EsAdministrador);
        // Un rol personalizado no puede "hacerse pasar" por admin aunque su código coincida.
        Assert.False(new RolCatalogo { Codigo = "admin", EsSistema = false }.EsAdministrador);
        Assert.False(new RolCatalogo { Codigo = "supervisor", EsSistema = true }.EsAdministrador);
    }
}
