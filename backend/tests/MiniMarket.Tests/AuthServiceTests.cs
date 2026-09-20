using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Application.Services;
using MiniMarket.Domain.Entities;
using MiniMarket.Domain.Exceptions;
using MiniMarket.Domain.Security;
using MiniMarket.Tests.Helpers;

namespace MiniMarket.Tests;

public class AuthServiceTests
{
    private readonly IUsuarioRepository _usuarios = Substitute.For<IUsuarioRepository>();
    private readonly IRolRepository _roles = Substitute.For<IRolRepository>();
    private readonly IRefreshTokenRepository _refresh = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenGenerator _jwt = Substitute.For<IJwtTokenGenerator>();
    private readonly IRefreshTokenGenerator _refreshGen = Substitute.For<IRefreshTokenGenerator>();
    private readonly ISeguridadAuditor _auditor = Substitute.For<ISeguridadAuditor>();
    private readonly IRequestInfo _request = Substitute.For<IRequestInfo>();
    private readonly ITenantContext _tenant = Datos.Tenant(usuarioId: 20);
    private readonly SeguridadOptions _options = new() { MaxIntentosFallidos = 5, MinutosBloqueo = 15, RefreshTokenDays = 7 };
    private readonly ManualTimeProvider _time = new();
    private readonly Usuario _usuario = Datos.Usuario(id: 20);
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _hasher.Verify("correcta", "hash-guardado").Returns(true);
        _roles.ObtenerPorIdAsync(Datos.EmpresaId, 2).Returns(Datos.Rol());
        _roles.ObtenerCodigosPermisosAsync(2).Returns(new[] { Permisos.VentasVer, Permisos.VentasCrear, "permiso.retirado" });
        _usuarios.ObtenerPorUsernameAsync(Datos.EmpresaId, "maria").Returns(_usuario);
        _usuarios.ObtenerPorIdAsync(Datos.EmpresaId, 20).Returns(_usuario);
        _usuarios.ObtenerParaSesionAsync(20).Returns(_usuario);

        _jwt.Generar(Arg.Any<Usuario>(), Arg.Any<RolCatalogo>(), Arg.Any<IReadOnlyCollection<string>>())
            .Returns(_ => new AccessToken("jwt", _time.AhoraUtc.AddMinutes(15)));
        _refreshGen.Generar().Returns(("refresh-nuevo", "hash-nuevo"));
        _refreshGen.Hashear(Arg.Any<string>()).Returns(ci => "h:" + ci.Arg<string>());
        _refresh.CrearAsync(Arg.Any<RefreshToken>()).Returns(100);

        _sut = new AuthService(_usuarios, _roles, _refresh, new PermisoService(_roles), _hasher, _jwt, _refreshGen,
            _auditor, _request, _tenant, _options, _time);
    }

    // ---------- Login ----------

    [Fact]
    public async Task Login_Correcto_EmiteTokensYPermisosDelRol()
    {
        var respuesta = await _sut.LoginAsync(new LoginRequest("  maria ", "correcta"));

        Assert.Equal("jwt", respuesta.Token);
        Assert.Equal("refresh-nuevo", respuesta.RefreshToken);
        Assert.Equal(_time.AhoraUtc.AddDays(7), respuesta.RefreshExpiraUtc);
        // Los permisos que ya no existen en el catálogo se descartan.
        Assert.Equal(new[] { Permisos.VentasVer, Permisos.VentasCrear }, respuesta.Usuario.Permisos);
        await _usuarios.Received(1).RegistrarLoginOkAsync(Datos.EmpresaId, 20, _time.AhoraUtc);
        await _auditor.Received(1).RegistrarAsync(TipoEventoSeguridad.LoginOk, Datos.EmpresaId, 20, 20, null);
    }

    [Fact]
    public async Task Login_RolAdministrador_RecibeTodoElCatalogoSinConsultarRolPermiso()
    {
        _usuario.RolId = 1;
        _roles.ObtenerPorIdAsync(Datos.EmpresaId, 1).Returns(Datos.RolAdmin());

        var respuesta = await _sut.LoginAsync(new LoginRequest("maria", "correcta"));

        Assert.Equal(Permisos.Todos, respuesta.Usuario.Permisos);
        await _roles.DidNotReceive().ObtenerCodigosPermisosAsync(Arg.Any<int>());
    }

    [Fact]
    public async Task Login_UsuarioInexistente_DaErrorGenericoYSimulaLaVerificacion()
    {
        _usuarios.ObtenerPorUsernameAsync(Datos.EmpresaId, "fantasma").Returns((Usuario?)null);

        await Assert.ThrowsAsync<CredencialesInvalidasException>(() => _sut.LoginAsync(new LoginRequest("fantasma", "x")));

        _hasher.Received(1).SimularVerificacion("x");
        await _usuarios.DidNotReceiveWithAnyArgs().RegistrarIntentoFallidoAsync(default, default, default, default);
    }

    [Fact]
    public async Task Login_PasswordIncorrecta_SumaUnIntentoConLosParametrosConfigurados()
    {
        _usuarios.RegistrarIntentoFallidoAsync(Datos.EmpresaId, 20, 5, _time.AhoraUtc.AddMinutes(15)).Returns((DateTime?)null);

        await Assert.ThrowsAsync<CredencialesInvalidasException>(() => _sut.LoginAsync(new LoginRequest("maria", "mala")));

        await _usuarios.Received(1).RegistrarIntentoFallidoAsync(Datos.EmpresaId, 20, 5, _time.AhoraUtc.AddMinutes(15));
        await _auditor.Received(1).RegistrarAsync(TipoEventoSeguridad.LoginFallido, Datos.EmpresaId, 20, 20, "Contraseña incorrecta");
        await _refresh.DidNotReceiveWithAnyArgs().CrearAsync(default!);
    }

    [Fact]
    public async Task Login_AlAlcanzarElMaximoDeIntentos_BloqueaLaCuenta()
    {
        var hasta = _time.AhoraUtc.AddMinutes(15);
        _usuarios.RegistrarIntentoFallidoAsync(Datos.EmpresaId, 20, 5, hasta).Returns(hasta);

        await Assert.ThrowsAsync<CuentaBloqueadaException>(() => _sut.LoginAsync(new LoginRequest("maria", "mala")));

        await _auditor.Received(1).RegistrarAsync(TipoEventoSeguridad.CuentaBloqueada, Datos.EmpresaId, 20, 20, Arg.Any<string>());
    }

    [Fact]
    public async Task Login_CuentaBloqueada_RechazaAunConLaContrasenaCorrecta()
    {
        _usuario.BloqueadoHasta = _time.AhoraUtc.AddMinutes(10);

        await Assert.ThrowsAsync<CuentaBloqueadaException>(() => _sut.LoginAsync(new LoginRequest("maria", "correcta")));

        _hasher.DidNotReceiveWithAnyArgs().Verify(default!, default!);
        await _refresh.DidNotReceiveWithAnyArgs().CrearAsync(default!);
    }

    [Fact]
    public async Task Login_BloqueoVencido_PermiteEntrar()
    {
        _usuario.BloqueadoHasta = _time.AhoraUtc.AddMinutes(15);
        _time.Avanzar(TimeSpan.FromMinutes(16));

        var respuesta = await _sut.LoginAsync(new LoginRequest("maria", "correcta"));

        Assert.Equal("jwt", respuesta.Token);
    }

    [Fact]
    public async Task Login_UsuarioInactivo_SeRechazaSoloConLaContrasenaCorrecta()
    {
        _usuario.Estado = "I";

        var ex = await Assert.ThrowsAsync<CredencialesInvalidasException>(() => _sut.LoginAsync(new LoginRequest("maria", "correcta")));

        Assert.Contains("inactivo", ex.Message);
    }

    // ---------- Refresh ----------

    private RefreshToken TokenActivo(int id = 50, Guid? familia = null) => new()
    {
        Id = id,
        UsuarioId = 20,
        TokenHash = "h:viejo",
        FamiliaId = familia ?? Guid.NewGuid(),
        CreadoUtc = _time.AhoraUtc.AddHours(-1),
        ExpiraUtc = _time.AhoraUtc.AddDays(6)
    };

    [Fact]
    public async Task Refrescar_RotaElToken_RevocaElViejoYEnlazaElNuevoEnLaMismaFamilia()
    {
        var actual = TokenActivo();
        _refresh.ObtenerPorHashAsync("h:viejo").Returns(actual);
        _refresh.RevocarAsync(50, _time.AhoraUtc).Returns(true);

        var respuesta = await _sut.RefrescarAsync(new RefreshRequest("viejo"));

        Assert.Equal("refresh-nuevo", respuesta.RefreshToken);
        await _refresh.Received(1).RevocarAsync(50, _time.AhoraUtc);
        await _refresh.Received(1).CrearAsync(Arg.Is<RefreshToken>(t => t.FamiliaId == actual.FamiliaId && t.TokenHash == "hash-nuevo" && t.UsuarioId == 20));
        await _refresh.Received(1).MarcarReemplazoAsync(50, 100);
    }

    [Fact]
    public async Task Refrescar_RecalculaLosPermisosConElEstadoActualDelRol()
    {
        _refresh.ObtenerPorHashAsync("h:viejo").Returns(TokenActivo());
        _refresh.RevocarAsync(50, _time.AhoraUtc).Returns(true);
        _roles.ObtenerCodigosPermisosAsync(2).Returns(new[] { Permisos.ProductosVer }); // el admin le cambió los permisos al rol

        var respuesta = await _sut.RefrescarAsync(new RefreshRequest("viejo"));

        Assert.Equal(new[] { Permisos.ProductosVer }, respuesta.Usuario.Permisos);
        _jwt.Received(1).Generar(_usuario, Arg.Any<RolCatalogo>(), Arg.Is<IReadOnlyCollection<string>>(p => p.SequenceEqual(new[] { Permisos.ProductosVer })));
    }

    [Fact]
    public async Task Refrescar_TokenYaRotado_RevocaTodaLaFamiliaYAudita()
    {
        var familia = Guid.NewGuid();
        var rotado = TokenActivo(familia: familia);
        rotado.RevocadoUtc = _time.AhoraUtc.AddMinutes(-5);
        rotado.ReemplazadoPorId = 51;
        _refresh.ObtenerPorHashAsync("h:viejo").Returns(rotado);

        await Assert.ThrowsAsync<CredencialesInvalidasException>(() => _sut.RefrescarAsync(new RefreshRequest("viejo")));

        await _refresh.Received(1).RevocarFamiliaAsync(familia, _time.AhoraUtc);
        await _auditor.Received(1).RegistrarAsync(TipoEventoSeguridad.RefreshReuso, Datos.EmpresaId, 20, 20, Arg.Any<string>());
        await _refresh.DidNotReceiveWithAnyArgs().CrearAsync(default!);
    }

    [Fact]
    public async Task Refrescar_TokenRevocadoPorLogout_SeRechazaSinTumbarLaFamilia()
    {
        var revocado = TokenActivo();
        revocado.RevocadoUtc = _time.AhoraUtc.AddMinutes(-5); // sin ReemplazadoPorId: lo revocó un logout, no una rotación
        _refresh.ObtenerPorHashAsync("h:viejo").Returns(revocado);

        await Assert.ThrowsAsync<CredencialesInvalidasException>(() => _sut.RefrescarAsync(new RefreshRequest("viejo")));

        await _refresh.DidNotReceiveWithAnyArgs().RevocarFamiliaAsync(default, default);
    }

    [Fact]
    public async Task Refrescar_TokenExpirado_SeRechaza()
    {
        var expirado = TokenActivo();
        expirado.ExpiraUtc = _time.AhoraUtc.AddSeconds(-1);
        _refresh.ObtenerPorHashAsync("h:viejo").Returns(expirado);

        await Assert.ThrowsAsync<CredencialesInvalidasException>(() => _sut.RefrescarAsync(new RefreshRequest("viejo")));
    }

    [Fact]
    public async Task Refrescar_TokenDesconocido_SeRechaza()
    {
        _refresh.ObtenerPorHashAsync(Arg.Any<string>()).Returns((RefreshToken?)null);

        await Assert.ThrowsAsync<CredencialesInvalidasException>(() => _sut.RefrescarAsync(new RefreshRequest("inventado")));
    }

    [Fact]
    public async Task Refrescar_UsuarioDesactivado_RevocaLaFamiliaYRechaza()
    {
        var actual = TokenActivo();
        _refresh.ObtenerPorHashAsync("h:viejo").Returns(actual);
        _usuario.Estado = "I";

        await Assert.ThrowsAsync<CredencialesInvalidasException>(() => _sut.RefrescarAsync(new RefreshRequest("viejo")));

        await _refresh.Received(1).RevocarFamiliaAsync(actual.FamiliaId, _time.AhoraUtc);
        await _refresh.DidNotReceiveWithAnyArgs().CrearAsync(default!);
    }

    [Fact]
    public async Task Refrescar_SiOtraPeticionGanaLaCarrera_NoEmiteTokenNuevo()
    {
        _refresh.ObtenerPorHashAsync("h:viejo").Returns(TokenActivo());
        _refresh.RevocarAsync(50, _time.AhoraUtc).Returns(false);

        await Assert.ThrowsAsync<CredencialesInvalidasException>(() => _sut.RefrescarAsync(new RefreshRequest("viejo")));

        await _refresh.DidNotReceiveWithAnyArgs().CrearAsync(default!);
    }

    // ---------- Logout ----------

    [Fact]
    public async Task Logout_RevocaElRefreshTokenPropio()
    {
        _refresh.ObtenerPorHashAsync("h:mio").Returns(new RefreshToken { Id = 7, UsuarioId = 20 });

        await _sut.LogoutAsync(new LogoutRequest("mio"));

        await _refresh.Received(1).RevocarAsync(7, _time.AhoraUtc);
        await _auditor.Received(1).RegistrarAsync(TipoEventoSeguridad.Logout, Datos.EmpresaId, 20, 20, null);
    }

    [Fact]
    public async Task Logout_NoPuedeRevocarLaSesionDeOtroUsuario()
    {
        _refresh.ObtenerPorHashAsync("h:ajeno").Returns(new RefreshToken { Id = 8, UsuarioId = 999 });

        await _sut.LogoutAsync(new LogoutRequest("ajeno"));

        await _refresh.DidNotReceiveWithAnyArgs().RevocarAsync(default, default);
    }

    // ---------- Cambiar contraseña ----------

    [Fact]
    public async Task CambiarPassword_Correcto_GuardaHashRevocaTodasLasSesionesYEmiteSesionNueva()
    {
        _usuario.DebeCambiarPassword = true;
        _hasher.Hash("NuevaClave1!").Returns("hash-nuevo");

        var respuesta = await _sut.CambiarPasswordAsync(new CambiarPasswordRequest("correcta", "NuevaClave1!"));

        await _usuarios.Received(1).ActualizarPasswordAsync(Datos.EmpresaId, 20, "hash-nuevo", false, 20);
        await _refresh.Received(1).RevocarTodosDeUsuarioAsync(20, _time.AhoraUtc);
        Assert.Equal("refresh-nuevo", respuesta.RefreshToken);
        // La sesión nueva ya no debe llevar el flag de cambio forzado.
        _jwt.Received(1).Generar(Arg.Is<Usuario>(u => !u.DebeCambiarPassword), Arg.Any<RolCatalogo>(), Arg.Any<IReadOnlyCollection<string>>());
    }

    [Fact]
    public async Task CambiarPassword_ActualIncorrecta_CuentaComoIntentoFallido()
    {
        var ex = await Assert.ThrowsAsync<ReglaDeNegocioException>(() => _sut.CambiarPasswordAsync(new CambiarPasswordRequest("mala", "NuevaClave1!")));

        Assert.Contains("actual", ex.Message);
        await _usuarios.Received(1).RegistrarIntentoFallidoAsync(Datos.EmpresaId, 20, 5, Arg.Any<DateTime>());
        await _usuarios.DidNotReceiveWithAnyArgs().ActualizarPasswordAsync(default, default, default!, default, default);
    }

    [Fact]
    public async Task CambiarPassword_SiElIntentoFallidoBloquea_RevocaLasSesiones()
    {
        var hasta = _time.AhoraUtc.AddMinutes(15);
        _usuarios.RegistrarIntentoFallidoAsync(Datos.EmpresaId, 20, 5, hasta).Returns(hasta);

        await Assert.ThrowsAsync<CuentaBloqueadaException>(() => _sut.CambiarPasswordAsync(new CambiarPasswordRequest("mala", "NuevaClave1!")));

        await _refresh.Received(1).RevocarTodosDeUsuarioAsync(20, _time.AhoraUtc);
    }

    [Fact]
    public async Task CambiarPassword_NuevaIgualALaActual_SeRechaza()
    {
        _hasher.Verify("correcta", "hash-guardado").Returns(true);

        var ex = await Assert.ThrowsAsync<ReglaDeNegocioException>(() => _sut.CambiarPasswordAsync(new CambiarPasswordRequest("correcta", "correcta")));

        Assert.Contains("distinta", ex.Message);
    }

    // ---------- Me ----------

    [Fact]
    public async Task ObtenerActual_LeePermisosDeLaBaseDeDatosYNoDelToken()
    {
        var dto = await _sut.ObtenerActualAsync();

        Assert.Equal("supervisor", dto.Rol);
        Assert.Equal(new[] { Permisos.VentasVer, Permisos.VentasCrear }, dto.Permisos);
    }
}
