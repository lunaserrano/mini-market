using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MiniMarket.Api.Middleware;
using MiniMarket.Infrastructure.Services;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Application.Services;
using MiniMarket.Domain.Entities;
using MiniMarket.Domain.Exceptions;
using MiniMarket.Domain.Security;
using MiniMarket.Tests.Helpers;

namespace MiniMarket.Tests;

public class AuditoriaDatosTests
{
    [Fact]
    public void OcultaSecretosEnCualquierNivelDelJson()
    {
        const string json = """
            {"username":"maria","password":"1234","anidado":{"refreshToken":"abc","x":1},"lista":[{"PasswordNueva":"zzz","ok":true}]}
            """;

        var resultado = JsonDocument.Parse(AuditoriaDatos.SanitizarJson(json)!).RootElement;

        Assert.Equal("maria", resultado.GetProperty("username").GetString());
        Assert.Equal(AuditoriaDatos.Oculto, resultado.GetProperty("password").GetString());
        Assert.Equal(AuditoriaDatos.Oculto, resultado.GetProperty("anidado").GetProperty("refreshToken").GetString());
        Assert.Equal(1, resultado.GetProperty("anidado").GetProperty("x").GetInt32());
        Assert.Equal(AuditoriaDatos.Oculto, resultado.GetProperty("lista")[0].GetProperty("PasswordNueva").GetString());
        Assert.DoesNotContain("1234", resultado.GetRawText());
        Assert.DoesNotContain("zzz", resultado.GetRawText());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SinCuerpoNoGuardaNada(string? json) => Assert.Null(AuditoriaDatos.SanitizarJson(json));

    [Fact]
    public void UnCuerpoQueNoEsJsonNoSeGuardaPorqueNoSePuedeGarantizarQueNoTengaSecretos() =>
        Assert.Null(AuditoriaDatos.SanitizarJson("password=1234&user=maria"));

    [Fact]
    public void AcotaElTamanoDeCuerposEnormes()
    {
        var json = JsonSerializer.Serialize(new { nota = new string('x', AuditoriaDatos.MaxCaracteres * 2) });

        var resultado = AuditoriaDatos.SanitizarJson(json)!;

        Assert.EndsWith("[truncado]", resultado);
        Assert.True(resultado.Length < AuditoriaDatos.MaxCaracteres + 20);
    }

    [Fact]
    public void ElDescriptorDeUnClicAcotaValoresYOcultaClavesSecretas()
    {
        var datos = new Dictionary<string, string?>
        {
            ["etiqueta"] = new string('a', 1000),
            ["token"] = "no-debe-verse",
            ["tag"] = "BUTTON"
        };

        var resultado = JsonDocument.Parse(AuditoriaDatos.SerializarDescriptor(datos)!).RootElement;

        Assert.Equal(300, resultado.GetProperty("etiqueta").GetString()!.Length);
        Assert.Equal(AuditoriaDatos.Oculto, resultado.GetProperty("token").GetString());
        Assert.Equal("BUTTON", resultado.GetProperty("tag").GetString());
    }

    [Fact]
    public void SinDescriptorDevuelveNull()
    {
        Assert.Null(AuditoriaDatos.SerializarDescriptor(null));
        Assert.Null(AuditoriaDatos.SerializarDescriptor(new Dictionary<string, string?>()));
    }
}

public class AuditoriaServiceTests
{
    private readonly IEventoSeguridadRepository _repo = Substitute.For<IEventoSeguridadRepository>();
    private readonly IAuditoriaCola _cola = Substitute.For<IAuditoriaCola>();
    private readonly IRequestInfo _request = Substitute.For<IRequestInfo>();
    private readonly ManualTimeProvider _time = new();
    private readonly List<EventoSeguridad> _encolados = new();

    private AuditoriaService Crear()
    {
        _request.Ip.Returns("10.0.0.5");
        _request.UserAgent.Returns("Mozilla/5.0");
        _cola.Encolar(Arg.Do<EventoSeguridad>(_encolados.Add)).Returns(true);
        return new AuditoriaService(_repo, Datos.Tenant(usuarioId: 77, empresaId: 9), _cola, _request, _time);
    }

    private static EventoClienteDto Clic(string tipo = TipoEventoSeguridad.ClicUi, DateTime? fecha = null) =>
        new(tipo, fecha, "/ventas", "Clic en Cobrar", new Dictionary<string, string?> { ["tag"] = "BUTTON", ["texto"] = "Cobrar" });

    [Fact]
    public void EmpresaYUsuarioSalenDelTokenYNoDelCliente()
    {
        Crear().RegistrarEventosCliente(new[] { Clic() });

        var evento = Assert.Single(_encolados);
        Assert.Equal(9, evento.EmpresaId);
        Assert.Equal(77, evento.ActorUsuarioId);
        Assert.Equal(OrigenEvento.Ui, evento.Origen);
        Assert.Equal("10.0.0.5", evento.Ip);
        Assert.Equal("/ventas", evento.Ruta);
        Assert.Contains("Cobrar", evento.Datos);
    }

    [Theory]
    [InlineData(TipoEventoSeguridad.LoginOk)]
    [InlineData(TipoEventoSeguridad.AccionApi)]
    [InlineData("LO_QUE_SEA")]
    public void ElClienteNoPuedeFabricarEventosDeSeguridadNiDeApi(string tipo)
    {
        var sut = Crear();

        Assert.Throws<ReglaDeNegocioException>(() => sut.RegistrarEventosCliente(new[] { Clic(), Clic(tipo) }));

        Assert.Empty(_encolados); // el lote inválido no se registra a medias
    }

    [Fact]
    public void RechazaLotesDemasiadoGrandes()
    {
        var lote = Enumerable.Range(0, AuditoriaService.MaxEventosPorLote + 1).Select(_ => Clic()).ToList();

        Assert.Throws<ReglaDeNegocioException>(() => Crear().RegistrarEventosCliente(lote));
        Assert.Empty(_encolados);
    }

    [Fact]
    public void UsaLaFechaDelNavegadorSoloSiEsCreible()
    {
        var reciente = _time.AhoraUtc.AddSeconds(-5);
        var muyAntigua = _time.AhoraUtc.AddDays(-3);
        var futura = _time.AhoraUtc.AddHours(2);

        Crear().RegistrarEventosCliente(new[] { Clic(fecha: reciente), Clic(fecha: muyAntigua), Clic(fecha: futura), Clic() });

        Assert.Equal(reciente, _encolados[0].FechaUtc);
        Assert.Equal(_time.AhoraUtc, _encolados[1].FechaUtc);
        Assert.Equal(_time.AhoraUtc, _encolados[2].FechaUtc);
        Assert.Equal(_time.AhoraUtc, _encolados[3].FechaUtc);
    }

    [Fact]
    public async Task ListarNormalizaElFiltroDeOrigen()
    {
        _repo.ListarAsync(Arg.Any<int>(), Arg.Any<AuditoriaFiltro>())
            .Returns(new PaginaResultado<EventoSeguridadDto>(Array.Empty<EventoSeguridadDto>(), 0, 1, 25));

        await Crear().ListarAsync(new AuditoriaFiltro(null, null, null, " ", 0, 5000, " ui "));

        await _repo.Received().ListarAsync(9, Arg.Is<AuditoriaFiltro>(f =>
            f.Origen == "UI" && f.Tipo == null && f.Pagina == 1 && f.TamanoPagina == 100));
    }
}

public class AuditoriaColaTests
{
    [Fact]
    public async Task EntregaLosEventosEnOrdenYRechazaCuandoEstaLlena()
    {
        var cola = new AuditoriaCola(capacidad: 2);

        Assert.True(cola.Encolar(new EventoSeguridad { Tipo = "A" }));
        Assert.True(cola.Encolar(new EventoSeguridad { Tipo = "B" }));
        Assert.False(cola.Encolar(new EventoSeguridad { Tipo = "C" }));

        cola.Completar();
        var recibidos = new List<string>();
        await foreach (var e in cola.Lector.ReadAllAsync()) recibidos.Add(e.Tipo);

        Assert.Equal(new[] { "A", "B" }, recibidos);
    }
}

public class AuditoriaMiddlewareTests
{
    private readonly List<EventoSeguridad> _encolados = new();
    private readonly IAuditoriaCola _cola = Substitute.For<IAuditoriaCola>();
    private readonly ManualTimeProvider _time = new();

    public AuditoriaMiddlewareTests() =>
        _cola.Encolar(Arg.Do<EventoSeguridad>(_encolados.Add)).Returns(true);

    private static DefaultHttpContext Peticion(string metodo, string ruta, string? cuerpo = null, params (string tipo, string valor)[] claims)
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.Method = metodo;
        contexto.Request.Path = ruta;
        contexto.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.20");
        contexto.Request.Headers.UserAgent = "TestAgent/1.0";
        if (cuerpo is not null)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(cuerpo);
            contexto.Request.ContentType = "application/json";
            contexto.Request.ContentLength = bytes.Length;
            contexto.Request.Body = new MemoryStream(bytes);
        }
        contexto.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            claims.Select(c => new System.Security.Claims.Claim(c.tipo, c.valor)), authenticationType: claims.Length > 0 ? "test" : null));
        return contexto;
    }

    private Task Ejecutar(HttpContext contexto, RequestDelegate siguiente) =>
        new AuditoriaMiddleware(siguiente, Substitute.For<ILogger<AuditoriaMiddleware>>()).InvokeAsync(contexto, _cola, _time);

    [Fact]
    public async Task RegistraLaPeticionConUsuarioEstadoYCuerpoSinSecretos()
    {
        var contexto = Peticion("POST", "/api/usuarios", """{"username":"maria","password":"1234"}""",
            (TenantClaimTypes.EmpresaId, "3"), (TenantClaimTypes.UsuarioId, "42"));
        string? cuerpoQueVeMvc = null;

        await Ejecutar(contexto, async ctx =>
        {
            cuerpoQueVeMvc = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
            ctx.Response.StatusCode = 201;
        });

        var evento = Assert.Single(_encolados);
        Assert.Equal(TipoEventoSeguridad.AccionApi, evento.Tipo);
        Assert.Equal(OrigenEvento.Api, evento.Origen);
        Assert.Equal("POST", evento.Metodo);
        Assert.Equal("/api/usuarios", evento.Ruta);
        Assert.Equal((short)201, evento.StatusCode);
        Assert.Equal(3, evento.EmpresaId);
        Assert.Equal(42, evento.ActorUsuarioId);
        Assert.Equal("192.168.1.20", evento.Ip);
        Assert.Equal("TestAgent/1.0", evento.UserAgent);
        Assert.Contains("maria", evento.Datos);
        Assert.DoesNotContain("1234", evento.Datos);
        // El middleware leyó el cuerpo para auditarlo, pero MVC aún debe poder leerlo completo (con la contraseña real).
        Assert.Equal("""{"username":"maria","password":"1234"}""", cuerpoQueVeMvc);
    }

    [Fact]
    public async Task RegistraTambienLasPeticionesRechazadasYAnonimas()
    {
        var contexto = Peticion("GET", "/api/ventas");

        await Ejecutar(contexto, ctx =>
        {
            ctx.Response.StatusCode = 401;
            return Task.CompletedTask;
        });

        var evento = Assert.Single(_encolados);
        Assert.Equal((short)401, evento.StatusCode);
        Assert.Null(evento.ActorUsuarioId);
        Assert.Null(evento.EmpresaId);
    }

    [Fact]
    public async Task IncluyeLaQueryStringEnLaRuta()
    {
        var contexto = Peticion("GET", "/api/productos");
        contexto.Request.QueryString = new QueryString("?buscar=leche&pagina=2");

        await Ejecutar(contexto, _ => Task.CompletedTask);

        Assert.Equal("/api/productos?buscar=leche&pagina=2", Assert.Single(_encolados).Ruta);
    }

    [Theory]
    [InlineData("OPTIONS", "/api/ventas")]
    [InlineData("GET", "/swagger/index.html")]
    [InlineData("POST", "/api/auditoria/cliente")]
    public async Task NoRegistraPreflightsSwaggerNiElReporteDeClics(string metodo, string ruta)
    {
        var siguienteEjecutado = false;

        await Ejecutar(Peticion(metodo, ruta), _ =>
        {
            siguienteEjecutado = true;
            return Task.CompletedTask;
        });

        Assert.True(siguienteEjecutado);
        Assert.Empty(_encolados);
    }

    [Fact]
    public async Task UnCuerpoQueNoEsJsonNoSeGuardaPeroLaPeticionSiSeRegistra()
    {
        var contexto = Peticion("POST", "/api/ventas");
        contexto.Request.ContentType = "text/plain";
        contexto.Request.ContentLength = 10;
        contexto.Request.Body = new MemoryStream("password=1"u8.ToArray());

        await Ejecutar(contexto, _ => Task.CompletedTask);

        Assert.Null(Assert.Single(_encolados).Datos);
    }

    [Fact]
    public async Task UnFalloAlEncolarNoRompeLaRespuestaAlUsuario()
    {
        _cola.Encolar(Arg.Any<EventoSeguridad>()).Returns(_ => throw new InvalidOperationException("cola rota"));
        var contexto = Peticion("GET", "/api/ventas");

        var ex = await Record.ExceptionAsync(() => Ejecutar(contexto, ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        }));

        Assert.Null(ex);
        Assert.Equal(200, contexto.Response.StatusCode);
    }
}
