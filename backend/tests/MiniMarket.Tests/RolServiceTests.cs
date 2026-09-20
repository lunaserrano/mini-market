using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Application.Services;
using MiniMarket.Domain.Entities;
using MiniMarket.Domain.Exceptions;
using MiniMarket.Domain.Security;
using MiniMarket.Tests.Helpers;

namespace MiniMarket.Tests;

public class RolServiceTests
{
    private readonly IRolRepository _roles = Substitute.For<IRolRepository>();
    private readonly ISeguridadAuditor _auditor = Substitute.For<ISeguridadAuditor>();
    private readonly ManualTimeProvider _time = new();
    private readonly RolService _sut;

    public RolServiceTests()
    {
        _roles.ListarAsync(Datos.EmpresaId).Returns(new[]
        {
            new RolDto(1, "admin", "Administrador", null, true, 1, Permisos.Todos.Count),
            new RolDto(2, "supervisor", "Supervisor", null, true, 3, 5),
            new RolDto(4, "bodeguero", "Bodeguero", null, false, 0, 2)
        });
        _roles.CrearAsync(Arg.Any<RolCatalogo>(), Arg.Any<IReadOnlyCollection<string>>()).Returns(9);

        _sut = new RolService(_roles, new PermisoService(_roles), Datos.Tenant(), _auditor, _time);
    }

    // ---------- Crear ----------

    [Fact]
    public async Task Crear_GeneraElCodigoDesdeElNombreYGuardaLosPermisosSinDuplicados()
    {
        var id = await _sut.CrearAsync(new RolCreateDto(" Jefe de Tienda ", " Encargado del turno ",
            new[] { Permisos.VentasVer, Permisos.VentasVer, Permisos.ProductosVer }));

        Assert.Equal(9, id);
        await _roles.Received(1).CrearAsync(
            Arg.Is<RolCatalogo>(r => r.Codigo == "jefe_de_tienda" && r.Nombre == "Jefe de Tienda" && r.Descripcion == "Encargado del turno" &&
                                     !r.EsSistema && r.EmpresaId == Datos.EmpresaId),
            Arg.Is<IReadOnlyCollection<string>>(p => p.Count == 2));
        await _auditor.Received(1).RegistrarAsync(TipoEventoSeguridad.RolCreado, Datos.EmpresaId, Datos.ActorId, null, Arg.Any<string>());
    }

    [Fact]
    public async Task Crear_ConPermisoDesconocido_SeRechaza()
    {
        var ex = await Assert.ThrowsAsync<ReglaDeNegocioException>(() =>
            _sut.CrearAsync(new RolCreateDto("Nuevo", null, new[] { Permisos.VentasVer, "hackear.todo" })));

        Assert.Contains("hackear.todo", ex.Message);
        await _roles.DidNotReceiveWithAnyArgs().CrearAsync(default!, default!);
    }

    [Theory]
    [InlineData("bodeguero")]
    [InlineData("BODEGUERO")]
    [InlineData("Administrador")]
    public async Task Crear_ConNombreYaExistente_SeRechazaSinDistinguirMayusculas(string nombre)
    {
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => _sut.CrearAsync(new RolCreateDto(nombre, null, Array.Empty<string>())));
    }

    [Fact]
    public async Task Crear_NombreQueGeneraUnCodigoReservado_SeRechaza()
    {
        // "Admin" → código "admin", que ya usa el rol de sistema.
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => _sut.CrearAsync(new RolCreateDto("Admin", null, Array.Empty<string>())));
    }

    // ---------- Actualizar ----------

    [Fact]
    public async Task Actualizar_ElRolAdministradorNoSePuedeModificar()
    {
        _roles.ObtenerPorIdAsync(Datos.EmpresaId, 1).Returns(Datos.RolAdmin());

        var ex = await Assert.ThrowsAsync<ReglaDeNegocioException>(() => _sut.ActualizarAsync(1, new RolUpdateDto("Admin 2", null, Array.Empty<string>())));

        Assert.Contains("Administrador", ex.Message);
        await _roles.DidNotReceiveWithAnyArgs().ActualizarAsync(default!, default);
    }

    [Fact]
    public async Task Actualizar_UnRolDeSistemaNoAdministradorSiSePuedeEditar_ManteniendoSuCodigo()
    {
        var supervisor = Datos.Rol(2, "supervisor", "Supervisor");
        _roles.ObtenerPorIdAsync(Datos.EmpresaId, 2).Returns(supervisor);
        _roles.ObtenerCodigosPermisosAsync(2).Returns(new[] { Permisos.VentasVer, Permisos.VentasAnular });

        await _sut.ActualizarAsync(2, new RolUpdateDto("Supervisor de Piso", null, new[] { Permisos.VentasVer, Permisos.ProductosVer }));

        await _roles.Received(1).ActualizarAsync(
            Arg.Is<RolCatalogo>(r => r.Codigo == "supervisor" && r.Nombre == "Supervisor de Piso"),
            Arg.Is<IReadOnlyCollection<string>?>(p => p != null && p.Count == 2));
        // +1 (productos.ver) −1 (ventas.anular)
        await _auditor.Received(1).RegistrarAsync(TipoEventoSeguridad.RolEditado, Datos.EmpresaId, Datos.ActorId, null,
            Arg.Is<string>(d => d.Contains("+1") && d.Contains("−1")));
    }

    [Fact]
    public async Task Actualizar_NombreDeOtroRol_SeRechaza()
    {
        _roles.ObtenerPorIdAsync(Datos.EmpresaId, 4).Returns(Datos.Rol(4, "bodeguero", "Bodeguero", esSistema: false));

        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => _sut.ActualizarAsync(4, new RolUpdateDto("Supervisor", null, Array.Empty<string>())));
    }

    [Fact]
    public async Task Actualizar_ConservandoSuPropioNombre_NoLoTomaComoDuplicado()
    {
        _roles.ObtenerPorIdAsync(Datos.EmpresaId, 4).Returns(Datos.Rol(4, "bodeguero", "Bodeguero", esSistema: false));
        _roles.ObtenerCodigosPermisosAsync(4).Returns(Array.Empty<string>());

        await _sut.ActualizarAsync(4, new RolUpdateDto("Bodeguero", "Nueva descripción", new[] { Permisos.InventarioVer }));

        await _roles.Received(1).ActualizarAsync(Arg.Any<RolCatalogo>(), Arg.Any<IReadOnlyCollection<string>?>());
    }

    [Fact]
    public async Task Actualizar_RolInexistente_LanzaNoEncontrado()
    {
        _roles.ObtenerPorIdAsync(Datos.EmpresaId, 404).Returns((RolCatalogo?)null);

        await Assert.ThrowsAsync<EntidadNoEncontradaException>(() => _sut.ActualizarAsync(404, new RolUpdateDto("X", null, Array.Empty<string>())));
    }

    // ---------- Eliminar ----------

    [Fact]
    public async Task Eliminar_UnRolDeSistema_SeRechaza()
    {
        _roles.ObtenerPorIdAsync(Datos.EmpresaId, 2).Returns(Datos.Rol());

        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => _sut.EliminarAsync(2));

        await _roles.DidNotReceiveWithAnyArgs().EliminarAsync(default, default);
    }

    [Fact]
    public async Task Eliminar_UnRolConUsuariosAsignados_SeRechaza()
    {
        _roles.ObtenerPorIdAsync(Datos.EmpresaId, 4).Returns(Datos.Rol(4, "bodeguero", "Bodeguero", esSistema: false));
        _roles.ContarUsuariosAsync(Datos.EmpresaId, 4).Returns(2);

        var ex = await Assert.ThrowsAsync<ReglaDeNegocioException>(() => _sut.EliminarAsync(4));

        Assert.Contains("2 usuario", ex.Message);
        await _roles.DidNotReceiveWithAnyArgs().EliminarAsync(default, default);
    }

    [Fact]
    public async Task Eliminar_UnRolPersonalizadoSinUsuarios_LoElimina()
    {
        _roles.ObtenerPorIdAsync(Datos.EmpresaId, 4).Returns(Datos.Rol(4, "bodeguero", "Bodeguero", esSistema: false));
        _roles.ContarUsuariosAsync(Datos.EmpresaId, 4).Returns(0);

        await _sut.EliminarAsync(4);

        await _roles.Received(1).EliminarAsync(Datos.EmpresaId, 4);
        await _auditor.Received(1).RegistrarAsync(TipoEventoSeguridad.RolEliminado, Datos.EmpresaId, Datos.ActorId, null, Arg.Any<string>());
    }

    // ---------- Detalle ----------

    [Fact]
    public async Task Obtener_ElAdministradorMuestraTodosLosPermisosDelCatalogo()
    {
        _roles.ObtenerPorIdAsync(Datos.EmpresaId, 1).Returns(Datos.RolAdmin());

        var detalle = await _sut.ObtenerAsync(1);

        Assert.Equal(Permisos.Todos, detalle.Permisos);
    }

    // ---------- Código (slug) ----------

    [Theory]
    [InlineData("Jefe de Tienda", "jefe_de_tienda")]
    [InlineData("  Ñandú   Gerente!! ", "nandu_gerente")]
    [InlineData("Caja-2 (turno noche)", "caja_2_turno_noche")]
    [InlineData("Ventas", "ventas")]
    public void GenerarCodigo_NormalizaAcentosEspaciosYSimbolos(string nombre, string esperado)
    {
        Assert.Equal(esperado, RolService.GenerarCodigo(nombre));
    }

    [Fact]
    public void GenerarCodigo_NoSuperaCincuentaCaracteres()
    {
        Assert.True(RolService.GenerarCodigo(new string('a', 120)).Length <= 50);
    }

    [Theory]
    [InlineData("###")]
    [InlineData("   ")]
    public void GenerarCodigo_SinLetrasNiNumeros_SeRechaza(string nombre)
    {
        Assert.Throws<ReglaDeNegocioException>(() => RolService.GenerarCodigo(nombre));
    }
}
