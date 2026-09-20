using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Application.Services;
using MiniMarket.Domain.Entities;
using MiniMarket.Domain.Exceptions;
using MiniMarket.Domain.Security;
using MiniMarket.Tests.Helpers;

namespace MiniMarket.Tests;

/// <summary>Fábrica de dobles compartida por las pruebas de crédito (venta y cobranza).</summary>
internal static class CreditoDobles
{
    public static IUnitOfWorkFactory UnitOfWork(out IUnitOfWork uow)
    {
        uow = Substitute.For<IUnitOfWork>();
        uow.Transaction.Returns(Substitute.For<System.Data.IDbTransaction>());
        var factory = Substitute.For<IUnitOfWorkFactory>();
        factory.Create().Returns(uow);
        return factory;
    }
}

public class CreditoServiceTests
{
    private const int CreditoId = 5;

    private readonly ICreditoRepository _creditos = Substitute.For<ICreditoRepository>();
    private readonly ICajaRepository _cajas = Substitute.For<ICajaRepository>();
    private readonly IUnitOfWork _uow;
    private readonly CreditoService _sut;

    public CreditoServiceTests()
    {
        var factory = CreditoDobles.UnitOfWork(out _uow);
        _sut = new CreditoService(_creditos, _cajas, factory, Datos.Tenant(permisos: Permisos.CreditosAbonar));

        _cajas.ObtenerAbiertaPorUsuarioAsync(Datos.EmpresaId, Datos.ActorId).Returns(new Caja { Id = 3, Estado = "ABIERTA" });
        _creditos.ObtenerDetalleAsync(Datos.EmpresaId, CreditoId).Returns(Detalle());
    }

    private static CreditoDto Detalle() => new(CreditoId, 1, 100, 7, "Ana", DateTime.UtcNow, null, null, 50m, 50m, 50m, "PENDIENTE", false, []);

    private void ConSaldo(decimal saldo, string estado = Credito.Pendiente) =>
        _creditos.ObtenerParaActualizarAsync(Datos.EmpresaId, CreditoId, Arg.Any<System.Data.IDbTransaction>())
            .Returns(new Credito { Id = CreditoId, EmpresaId = Datos.EmpresaId, MontoOriginal = 50m, SaldoPendiente = saldo, Estado = estado });

    [Fact]
    public async Task AbonoParcial_ReduceElSaldoYDejaElCreditoPendiente()
    {
        ConSaldo(50m);

        await _sut.AbonarAsync(CreditoId, new AbonoCreditoCreateDto("tarjeta", 20m, " V-1 "));

        await _creditos.Received(1).RegistrarAbonoAsync(
            Arg.Is<AbonoCredito>(a => a.Monto == 20m && a.Metodo == "TARJETA" && a.Referencia == "V-1" && a.CajaId == null && a.UsuarioId == Datos.ActorId),
            Arg.Any<System.Data.IDbTransaction>());
        await _creditos.Received(1).ActualizarSaldoAsync(CreditoId, 30m, Credito.Pendiente, null, Arg.Any<System.Data.IDbTransaction>());
        _uow.Received(1).Commit();
    }

    [Fact]
    public async Task AbonoQueSaldaLaDeuda_MarcaElCreditoComoPagado()
    {
        ConSaldo(20m);

        await _sut.AbonarAsync(CreditoId, new AbonoCreditoCreateDto("TRANSFERENCIA", 20m, null));

        await _creditos.Received(1).ActualizarSaldoAsync(CreditoId, 0m, Credito.Pagado, Arg.Is<DateTime?>(d => d.HasValue), Arg.Any<System.Data.IDbTransaction>());
    }

    [Fact]
    public async Task AbonoEnEfectivo_LigaLaCajaYGeneraUnIngresoDeCaja()
    {
        ConSaldo(50m);

        await _sut.AbonarAsync(CreditoId, new AbonoCreditoCreateDto("EFECTIVO", 15m, null));

        await _creditos.Received(1).RegistrarAbonoAsync(Arg.Is<AbonoCredito>(a => a.CajaId == 3), Arg.Any<System.Data.IDbTransaction>());
        await _cajas.Received(1).RegistrarMovimientoAsync(
            Arg.Is<MovimientoCaja>(m => m.CajaId == 3 && m.Tipo == "INGRESO" && m.Monto == 15m && m.DocumentoReferenciaId == CreditoId),
            Arg.Any<System.Data.IDbTransaction?>());
    }

    [Fact]
    public async Task AbonoEnEfectivoSinCajaAbierta_SeRechaza()
    {
        _cajas.ObtenerAbiertaPorUsuarioAsync(Datos.EmpresaId, Datos.ActorId).Returns((Caja?)null);
        ConSaldo(50m);

        await Assert.ThrowsAsync<CajaCerradaException>(() => _sut.AbonarAsync(CreditoId, new AbonoCreditoCreateDto("EFECTIVO", 10m, null)));

        await _creditos.DidNotReceiveWithAnyArgs().RegistrarAbonoAsync(default!, default!);
    }

    [Fact]
    public async Task AbonoQueSuperaElSaldo_SeRechazaSinRegistrarNada()
    {
        ConSaldo(20m);

        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => _sut.AbonarAsync(CreditoId, new AbonoCreditoCreateDto("TARJETA", 20.01m, null)));

        await _creditos.DidNotReceiveWithAnyArgs().RegistrarAbonoAsync(default!, default!);
        _uow.DidNotReceive().Commit();
    }

    [Theory]
    [InlineData(Credito.Pagado)]
    [InlineData(Credito.Anulado)]
    public async Task NoSeAbonaAUnCreditoPagadoOAnulado(string estado)
    {
        ConSaldo(0m, estado);

        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => _sut.AbonarAsync(CreditoId, new AbonoCreditoCreateDto("TARJETA", 1m, null)));
    }

    [Theory]
    [InlineData("TARJETA", 0)]
    [InlineData("TARJETA", -5)]
    [InlineData("CHEQUE", 10)]
    public async Task AbonoConMontoOMetodoInvalido_SeRechaza(string metodo, decimal monto)
    {
        ConSaldo(50m);

        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => _sut.AbonarAsync(CreditoId, new AbonoCreditoCreateDto(metodo, monto, null)));
    }

    [Fact]
    public async Task AbonarACreditoDeOtraEmpresa_SeTrataComoInexistente()
    {
        // El repositorio filtra por empresa: un id ajeno no se encuentra.
        _creditos.ObtenerParaActualizarAsync(Datos.EmpresaId, 99, Arg.Any<System.Data.IDbTransaction>()).Returns((Credito?)null);

        await Assert.ThrowsAsync<EntidadNoEncontradaException>(() => _sut.AbonarAsync(99, new AbonoCreditoCreateDto("TARJETA", 1m, null)));
    }

    [Fact]
    public async Task Listar_ConEstadoInvalido_SeRechaza()
    {
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => _sut.ListarAsync(null, "VENCIDO"));
    }

    [Fact]
    public async Task Listar_NormalizaElEstado()
    {
        await _sut.ListarAsync(7, " pendiente ");

        await _creditos.Received(1).ListarAsync(Datos.EmpresaId, 7, "PENDIENTE");
    }
}

public class VentaACreditoTests
{
    private const int ClienteId = 7;

    private readonly IVentaRepository _ventas = Substitute.For<IVentaRepository>();
    private readonly IInventarioRepository _inventario = Substitute.For<IInventarioRepository>();
    private readonly IProductoRepository _productos = Substitute.For<IProductoRepository>();
    private readonly ICajaRepository _cajas = Substitute.For<ICajaRepository>();
    private readonly IEmpresaRepository _empresas = Substitute.For<IEmpresaRepository>();
    private readonly IClienteRepository _clientes = Substitute.For<IClienteRepository>();
    private readonly ICreditoRepository _creditos = Substitute.For<ICreditoRepository>();
    private readonly IUnitOfWork _uow;
    private readonly IUnitOfWorkFactory _factory;

    public VentaACreditoTests()
    {
        _factory = CreditoDobles.UnitOfWork(out _uow);

        _cajas.ObtenerAbiertaPorUsuarioAsync(Datos.EmpresaId, Datos.ActorId).Returns(new Caja { Id = 3, SucursalId = 1, Estado = "ABIERTA" });
        _empresas.ObtenerPorIdAsync(Datos.EmpresaId).Returns(new Empresa { Id = Datos.EmpresaId, TasaImpuesto = 13m });
        _productos.ObtenerEntidadAsync(Datos.EmpresaId, 1).Returns(new Producto { Id = 1, Nombre = "Arroz" });
        _productos.ObtenerTipoPrecioAsync(1, 1).Returns(new TipoPrecio { Id = 1, ProductoId = 1, Nombre = "Unidad", CantidadBase = 1, PrecioVenta = 50m });
        _inventario.ObtenerParaActualizarAsync(1, Arg.Any<int>(), Arg.Any<System.Data.IDbTransaction>()).Returns(new Inventario { StockActual = 100 });
        _ventas.CrearAsync(Arg.Any<Venta>(), Arg.Any<System.Data.IDbTransaction>()).Returns(900);
        _clientes.ObtenerPorIdAsync(Datos.EmpresaId, ClienteId).Returns(new Cliente { Id = ClienteId, Nombre = "Ana", Estado = "A" });
    }

    private VentaService Sut(params string[] permisos) =>
        new(_ventas, _inventario, _productos, _cajas, _empresas, _clientes, _creditos, _factory, Datos.Tenant(permisos: permisos));

    private static VentaCreateDto Venta(int? clienteId, bool alCredito, decimal pagado, DateTime? vence = null) =>
        new(clienteId, [new DetalleVentaCreateDto(1, 1, 1m, 0m)],
            pagado > 0 ? [new PagoVentaCreateDto("EFECTIVO", pagado, null)] : [],
            alCredito, vence);

    [Fact]
    public async Task VentaTotalmenteACredito_CreaElCreditoPorElTotalSinPagos()
    {
        await Sut(Permisos.CreditosOtorgar).CrearAsync(Venta(ClienteId, true, 0m, DateTime.UtcNow.AddDays(15)));

        await _creditos.Received(1).CrearAsync(
            Arg.Is<Credito>(c => c.VentaId == 900 && c.ClienteId == ClienteId && c.MontoOriginal == 50m && c.SaldoPendiente == 50m
                                 && c.Estado == Credito.Pendiente && c.FechaVencimiento.HasValue && c.CreadoPorUsuarioId == Datos.ActorId),
            Arg.Any<System.Data.IDbTransaction>());
        await _ventas.DidNotReceiveWithAnyArgs().CrearPagoAsync(default!, default!);
        _uow.Received(1).Commit();
    }

    [Fact]
    public async Task VentaConPagoInicial_DejaACreditoSoloLoQueFalta()
    {
        await Sut(Permisos.CreditosOtorgar).CrearAsync(Venta(ClienteId, true, 20m));

        await _creditos.Received(1).CrearAsync(Arg.Is<Credito>(c => c.MontoOriginal == 30m && c.SaldoPendiente == 30m), Arg.Any<System.Data.IDbTransaction>());
        await _ventas.Received(1).CrearPagoAsync(Arg.Is<PagoVenta>(p => p.Monto == 20m), Arg.Any<System.Data.IDbTransaction>());
    }

    [Fact]
    public async Task VentaAlContado_NoCreaCredito()
    {
        await Sut().CrearAsync(Venta(ClienteId, false, 50m));

        await _creditos.DidNotReceiveWithAnyArgs().CrearAsync(default!, default!);
    }

    [Fact]
    public async Task SinElPermisoDeOtorgar_NoSePuedeVenderACredito()
    {
        await Assert.ThrowsAsync<PermisoDenegadoException>(() => Sut().CrearAsync(Venta(ClienteId, true, 0m)));

        _uow.DidNotReceive().Commit();
    }

    [Fact]
    public async Task VenderACreditoSinCliente_SeRechaza()
    {
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => Sut(Permisos.CreditosOtorgar).CrearAsync(Venta(null, true, 0m)));
    }

    [Fact]
    public async Task VenderACreditoAUnClienteInactivo_SeRechaza()
    {
        _clientes.ObtenerPorIdAsync(Datos.EmpresaId, ClienteId).Returns(new Cliente { Id = ClienteId, Nombre = "Ana", Estado = "I" });

        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => Sut(Permisos.CreditosOtorgar).CrearAsync(Venta(ClienteId, true, 0m)));
    }

    [Fact]
    public async Task ClienteDeOtraEmpresa_SeTrataComoInexistente()
    {
        _clientes.ObtenerPorIdAsync(Datos.EmpresaId, 999).Returns((Cliente?)null);

        await Assert.ThrowsAsync<EntidadNoEncontradaException>(() => Sut(Permisos.CreditosOtorgar).CrearAsync(Venta(999, true, 0m)));
    }

    [Fact]
    public async Task SiLosPagosYaCubrenElTotal_NoQuedaSaldoParaElCredito()
    {
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => Sut(Permisos.CreditosOtorgar).CrearAsync(Venta(ClienteId, true, 50m)));
    }

    [Fact]
    public async Task VencimientoEnElPasado_SeRechaza()
    {
        await Assert.ThrowsAsync<ReglaDeNegocioException>(
            () => Sut(Permisos.CreditosOtorgar).CrearAsync(Venta(ClienteId, true, 0m, DateTime.UtcNow.AddDays(-2))));
    }

    [Fact]
    public async Task SinMarcarCredito_LosPagosInsuficientesSiguenSiendoError()
    {
        await Assert.ThrowsAsync<PagosInsuficientesException>(() => Sut(Permisos.CreditosOtorgar).CrearAsync(Venta(ClienteId, false, 20m)));
    }

    // ---------- Anulación ----------

    private void VentaAnulable() =>
        _ventas.ObtenerEntidadAsync(Datos.EmpresaId, 900).Returns(new Venta { Id = 900, EmpresaId = Datos.EmpresaId, Estado = "COMPLETADA" });

    [Fact]
    public async Task AnularVentaACreditoSinAbonos_AnulaTambienElCredito()
    {
        VentaAnulable();
        _creditos.ObtenerPorVentaAsync(900, Arg.Any<System.Data.IDbTransaction>()).Returns(new Credito { Id = 5, VentaId = 900 });
        _creditos.ContarAbonosAsync(5, Arg.Any<System.Data.IDbTransaction>()).Returns(0);

        await Sut().AnularAsync(900, new AnularVentaRequest("error", false));

        await _creditos.Received(1).AnularAsync(5, Arg.Any<System.Data.IDbTransaction>());
        await _ventas.Received(1).AnularAsync(900, Datos.ActorId, "error", Arg.Any<System.Data.IDbTransaction>());
    }

    [Fact]
    public async Task AnularVentaACreditoConAbonos_SeRechazaSinTocarNada()
    {
        VentaAnulable();
        _creditos.ObtenerPorVentaAsync(900, Arg.Any<System.Data.IDbTransaction>()).Returns(new Credito { Id = 5, VentaId = 900 });
        _creditos.ContarAbonosAsync(5, Arg.Any<System.Data.IDbTransaction>()).Returns(2);

        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => Sut().AnularAsync(900, new AnularVentaRequest("error", true)));

        await _ventas.DidNotReceiveWithAnyArgs().AnularAsync(default, default, default!, default!);
        _uow.DidNotReceive().Commit();
    }

    [Fact]
    public async Task AnularVentaAlContado_NoTocaCreditos()
    {
        VentaAnulable();
        _creditos.ObtenerPorVentaAsync(900, Arg.Any<System.Data.IDbTransaction>()).Returns((Credito?)null);

        await Sut().AnularAsync(900, new AnularVentaRequest("error", false));

        await _creditos.DidNotReceiveWithAnyArgs().AnularAsync(default, default!);
    }
}

public class CreditoValidatorTests
{
    private static readonly MiniMarket.Application.Validators.AbonoCreditoCreateDtoValidator Validador = new();

    [Theory]
    [InlineData("EFECTIVO", 10, true)]
    [InlineData("tarjeta", 0.01, true)]
    [InlineData("CHEQUE", 10, false)]
    [InlineData("EFECTIVO", 0, false)]
    [InlineData("", 10, false)]
    public void ValidaMetodoYMonto(string metodo, double monto, bool valido) =>
        Assert.Equal(valido, Validador.Validate(new AbonoCreditoCreateDto(metodo, (decimal)monto, null)).IsValid);

    [Fact]
    public void LaReferenciaNoPuedeSuperarLaColumna() =>
        Assert.False(Validador.Validate(new AbonoCreditoCreateDto("TARJETA", 5m, new string('x', 101))).IsValid);
}
