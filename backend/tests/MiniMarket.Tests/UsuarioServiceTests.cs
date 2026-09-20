using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Application.Services;
using MiniMarket.Domain.Entities;
using MiniMarket.Domain.Exceptions;
using MiniMarket.Domain.Security;
using MiniMarket.Tests.Helpers;

namespace MiniMarket.Tests;

public class UsuarioServiceTests
{
    private const int YoId = Datos.ActorId; // quien opera
    private const int OtroId = 20;

    private readonly IUsuarioRepository _usuarios = Substitute.For<IUsuarioRepository>();
    private readonly IRolRepository _roles = Substitute.For<IRolRepository>();
    private readonly IRefreshTokenRepository _refresh = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ISeguridadAuditor _auditor = Substitute.For<ISeguridadAuditor>();
    private readonly ManualTimeProvider _time = new();
    private readonly UsuarioService _sut;

    public UsuarioServiceTests()
    {
        _hasher.Hash(Arg.Any<string>()).Returns(ci => "hash:" + ci.Arg<string>());
        _roles.ObtenerPorIdAsync(Datos.EmpresaId, 1).Returns(Datos.RolAdmin());
        _roles.ObtenerPorIdAsync(Datos.EmpresaId, 2).Returns(Datos.Rol());
        _usuarios.ExisteSucursalAsync(Datos.EmpresaId, Arg.Any<int>()).Returns(true);
        _usuarios.CrearAsync(Arg.Any<Usuario>()).Returns(77);

        _sut = new UsuarioService(_usuarios, _roles, _refresh, _hasher, Datos.Tenant(YoId), _auditor, _time);
    }

    private Usuario Existente(int id, int rolId, string estado = "A")
    {
        var u = Datos.Usuario(id, rolId, estado);
        _usuarios.ObtenerPorIdAsync(Datos.EmpresaId, id).Returns(u);
        return u;
    }

    // ---------- Crear ----------

    [Fact]
    public async Task Crear_HasheaLaContrasenaGuardaElFlagYAudita()
    {
        var id = await _sut.CrearAsync(new UsuarioCreateDto(null, " Ana Gómez ", " ana ", "Clave123!", 2, true));

        Assert.Equal(77, id);
        await _usuarios.Received(1).CrearAsync(Arg.Is<Usuario>(u =>
            u.Username == "ana" && u.NombreCompleto == "Ana Gómez" && u.PasswordHash == "hash:Clave123!" &&
            u.RolId == 2 && u.EmpresaId == Datos.EmpresaId && u.DebeCambiarPassword && u.CreadoPorUsuarioId == YoId));
        await _auditor.Received(1).RegistrarAsync(TipoEventoSeguridad.UsuarioCreado, Datos.EmpresaId, YoId, 77, Arg.Any<string>());
    }

    [Fact]
    public async Task Crear_ConRolDeOtraEmpresa_SeTrataComoInexistente()
    {
        // El repositorio filtra por empresa: un RolId ajeno no se encuentra.
        _roles.ObtenerPorIdAsync(Datos.EmpresaId, 999).Returns((RolCatalogo?)null);

        var ex = await Assert.ThrowsAsync<ReglaDeNegocioException>(() => _sut.CrearAsync(new UsuarioCreateDto(null, "Ana", "ana", "Clave123!", 999)));

        Assert.Contains("rol", ex.Message);
        await _usuarios.DidNotReceiveWithAnyArgs().CrearAsync(default!);
    }

    [Fact]
    public async Task Crear_ConSucursalAjena_SeRechaza()
    {
        _usuarios.ExisteSucursalAsync(Datos.EmpresaId, 55).Returns(false);

        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => _sut.CrearAsync(new UsuarioCreateDto(55, "Ana", "ana", "Clave123!", 2)));
    }

    [Fact]
    public async Task Crear_UsernameRepetido_SeRechaza()
    {
        _usuarios.ObtenerPorUsernameAsync(Datos.EmpresaId, "ana").Returns(Datos.Usuario(username: "ana"));

        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => _sut.CrearAsync(new UsuarioCreateDto(null, "Ana", "ana", "Clave123!", 2)));
    }

    // ---------- Estado ----------

    [Fact]
    public async Task CambiarEstado_Desactivar_RevocaSusSesiones()
    {
        Existente(OtroId, rolId: 2);

        await _sut.CambiarEstadoAsync(OtroId, activo: false);

        await _usuarios.Received(1).CambiarEstadoAsync(Datos.EmpresaId, OtroId, "I", YoId);
        await _refresh.Received(1).RevocarTodosDeUsuarioAsync(OtroId, _time.AhoraUtc);
        await _auditor.Received(1).RegistrarAsync(TipoEventoSeguridad.UsuarioEstado, Datos.EmpresaId, YoId, OtroId, "Usuario desactivado");
    }

    [Fact]
    public async Task CambiarEstado_NoPuedeDesactivarseASiMismo()
    {
        Existente(YoId, rolId: 1);

        var ex = await Assert.ThrowsAsync<ReglaDeNegocioException>(() => _sut.CambiarEstadoAsync(YoId, activo: false));

        Assert.Contains("propia", ex.Message);
    }

    [Fact]
    public async Task CambiarEstado_NoPuedeDesactivarAlUltimoAdministradorActivo()
    {
        Existente(OtroId, rolId: 1);
        _usuarios.ContarAdministradoresActivosAsync(Datos.EmpresaId, OtroId).Returns(0);

        var ex = await Assert.ThrowsAsync<ReglaDeNegocioException>(() => _sut.CambiarEstadoAsync(OtroId, activo: false));

        Assert.Contains("administrador", ex.Message);
        await _usuarios.DidNotReceiveWithAnyArgs().CambiarEstadoAsync(default, default, default!, default);
    }

    [Fact]
    public async Task CambiarEstado_PuedeDesactivarAUnAdministradorSiHayOtroActivo()
    {
        Existente(OtroId, rolId: 1);
        _usuarios.ContarAdministradoresActivosAsync(Datos.EmpresaId, OtroId).Returns(1);

        await _sut.CambiarEstadoAsync(OtroId, activo: false);

        await _usuarios.Received(1).CambiarEstadoAsync(Datos.EmpresaId, OtroId, "I", YoId);
    }

    [Fact]
    public async Task CambiarEstado_UsuarioInexistente_LanzaNoEncontrado()
    {
        _usuarios.ObtenerPorIdAsync(Datos.EmpresaId, 404).Returns((Usuario?)null);

        await Assert.ThrowsAsync<EntidadNoEncontradaException>(() => _sut.CambiarEstadoAsync(404, activo: false));
    }

    [Fact]
    public async Task CambiarEstado_Activar_NoRevocaSesiones()
    {
        Existente(OtroId, rolId: 2, estado: "I");

        await _sut.CambiarEstadoAsync(OtroId, activo: true);

        await _refresh.DidNotReceiveWithAnyArgs().RevocarTodosDeUsuarioAsync(default, default);
    }

    // ---------- Actualizar / cambio de rol ----------

    [Fact]
    public async Task Actualizar_NoPuedeCambiarSuPropioRol()
    {
        Existente(YoId, rolId: 1);

        var ex = await Assert.ThrowsAsync<ReglaDeNegocioException>(() => _sut.ActualizarAsync(YoId, new UsuarioUpdateDto(null, "Yo", 2)));

        Assert.Contains("propio rol", ex.Message);
    }

    [Fact]
    public async Task Actualizar_QuitarElRolAdminAlUltimoAdministrador_SeRechaza()
    {
        Existente(OtroId, rolId: 1);
        _usuarios.ContarAdministradoresActivosAsync(Datos.EmpresaId, OtroId).Returns(0);

        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => _sut.ActualizarAsync(OtroId, new UsuarioUpdateDto(null, "Otro", 2)));

        await _usuarios.DidNotReceiveWithAnyArgs().ActualizarAsync(default!);
    }

    [Fact]
    public async Task Actualizar_CambioDeRol_RevocaSesionesParaQueAplique()
    {
        Existente(OtroId, rolId: 2);
        _roles.ObtenerPorIdAsync(Datos.EmpresaId, 3).Returns(Datos.Rol(3, "cajero", "Cajero"));

        await _sut.ActualizarAsync(OtroId, new UsuarioUpdateDto(null, "Otro", 3));

        await _usuarios.Received(1).ActualizarAsync(Arg.Is<Usuario>(u => u.RolId == 3));
        await _refresh.Received(1).RevocarTodosDeUsuarioAsync(OtroId, _time.AhoraUtc);
    }

    [Fact]
    public async Task Actualizar_SinCambiarDeRol_NoRevocaSesiones()
    {
        Existente(OtroId, rolId: 2);

        await _sut.ActualizarAsync(OtroId, new UsuarioUpdateDto(null, "Nuevo Nombre", 2));

        await _refresh.DidNotReceiveWithAnyArgs().RevocarTodosDeUsuarioAsync(default, default);
    }

    // ---------- Contraseña, desbloqueo y sesiones ----------

    [Fact]
    public async Task ResetPassword_GuardaHashRevocaSesionesYAudita()
    {
        Existente(OtroId, rolId: 2);

        await _sut.ResetPasswordAsync(OtroId, new ResetPasswordRequest("Temporal1!", DebeCambiarPassword: true));

        await _usuarios.Received(1).ActualizarPasswordAsync(Datos.EmpresaId, OtroId, "hash:Temporal1!", true, YoId);
        await _refresh.Received(1).RevocarTodosDeUsuarioAsync(OtroId, _time.AhoraUtc);
        await _auditor.Received(1).RegistrarAsync(TipoEventoSeguridad.PasswordReset, Datos.EmpresaId, YoId, OtroId, Arg.Any<string>());
    }

    [Fact]
    public async Task Desbloquear_LimpiaElBloqueoYAudita()
    {
        Existente(OtroId, rolId: 2);

        await _sut.DesbloquearAsync(OtroId);

        await _usuarios.Received(1).DesbloquearAsync(Datos.EmpresaId, OtroId);
        await _auditor.Received(1).RegistrarAsync(TipoEventoSeguridad.Desbloqueo, Datos.EmpresaId, YoId, OtroId, null);
    }

    [Fact]
    public async Task RevocarSesiones_RevocaTodosLosRefreshTokensDelUsuario()
    {
        Existente(OtroId, rolId: 2);

        await _sut.RevocarSesionesAsync(OtroId);

        await _refresh.Received(1).RevocarTodosDeUsuarioAsync(OtroId, _time.AhoraUtc);
    }

    // ---------- Listado ----------

    [Fact]
    public async Task Listar_MarcaComoBloqueadoSoloSiElBloqueoSigueVigente()
    {
        var vigente = Datos.Usuario(30, 2, username: "a"); vigente.BloqueadoHasta = _time.AhoraUtc.AddMinutes(5);
        var vencido = Datos.Usuario(31, 2, username: "b"); vencido.BloqueadoHasta = _time.AhoraUtc.AddMinutes(-5);
        _usuarios.ListarAsync(Datos.EmpresaId).Returns(new[] { vigente, vencido });
        _roles.ListarAsync(Datos.EmpresaId).Returns(new[] { new RolDto(2, "supervisor", "Supervisor", null, true, 2, 5) });

        var lista = await _sut.ListarAsync();

        Assert.True(lista[0].Bloqueado);
        Assert.Equal(DateTimeKind.Utc, lista[0].BloqueadoHasta!.Value.Kind);
        Assert.False(lista[1].Bloqueado);
        Assert.Null(lista[1].BloqueadoHasta);
        Assert.Equal("Supervisor", lista[0].RolNombre);
    }
}
