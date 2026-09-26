using Microsoft.Extensions.Caching.Memory;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Application.Services;

namespace MiniMarket.Tests;

public class AuditoriaEstadoProviderTests
{
    private readonly IParametroRepository _parametros = Substitute.For<IParametroRepository>();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    private AuditoriaEstadoProvider Crear(bool globalHabilitada = true) =>
        new(_parametros, _cache, new AuditoriaOptions { Habilitada = globalHabilitada });

    [Fact]
    public async Task ElInterruptorGlobalGanaSinConsultarLaBd()
    {
        var habilitada = await Crear(globalHabilitada: false).EstaHabilitadaAsync(empresaId: 9, sucursalId: null);

        Assert.False(habilitada);
        await _parametros.DidNotReceive().ObtenerAuditoriaHabilitadaAsync(Arg.Any<int>(), Arg.Any<int?>());
    }

    [Fact]
    public async Task SinEmpresaResueltaSiempreSeRegistra()
    {
        var habilitada = await Crear().EstaHabilitadaAsync(empresaId: null, sucursalId: null);

        Assert.True(habilitada);
        await _parametros.DidNotReceive().ObtenerAuditoriaHabilitadaAsync(Arg.Any<int>(), Arg.Any<int?>());
    }

    [Fact]
    public async Task SinFilaEnBdAsumeHabilitada()
    {
        _parametros.ObtenerAuditoriaHabilitadaAsync(9, 2).Returns((bool?)null);

        Assert.True(await Crear().EstaHabilitadaAsync(9, 2));
    }

    [Fact]
    public async Task RespetaLaBanderaDesactivadaDeLaEmpresa()
    {
        _parametros.ObtenerAuditoriaHabilitadaAsync(9, 2).Returns(false);

        Assert.False(await Crear().EstaHabilitadaAsync(9, 2));
    }

    [Fact]
    public async Task CacheaElResultadoPorEmpresaYSucursalSinRepetirLaConsulta()
    {
        _parametros.ObtenerAuditoriaHabilitadaAsync(9, 2).Returns(false);
        var sut = Crear();

        await sut.EstaHabilitadaAsync(9, 2);
        await sut.EstaHabilitadaAsync(9, 2);

        await _parametros.Received(1).ObtenerAuditoriaHabilitadaAsync(9, 2);
    }
}
