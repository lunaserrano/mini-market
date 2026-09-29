using MiniMarket.Desktop.Api.Clients;

namespace MiniMarket.Desktop.Services;

public enum EstadoServicio { Desconocido, EnLinea, SinBaseDatos, Detenido }

/// <summary>
/// Consulta GET /api/health (Api embebida, ver <see cref="ApiLocal"/>) cada Api:HealthIntervaloSegundos.
/// MainForm lo muestra en la barra de estado: el sistema es local, así que "sin conexión" significa
/// SQL Server detenido, nunca "sin internet".
/// </summary>
public sealed class ApiHealthMonitor : IDisposable
{
    private readonly IServiceProvider _services;
    private readonly TimeSpan _intervalo;
    private CancellationTokenSource? _cts;

    public ApiHealthMonitor(IServiceProvider services, IConfiguration configuration)
    {
        _services = services;
        _intervalo = TimeSpan.FromSeconds(Math.Max(5, configuration.GetValue("Api:HealthIntervaloSegundos", 15)));
    }

    public EstadoServicio Estado { get; private set; } = EstadoServicio.Desconocido;

    /// <summary>Se dispara en un hilo del pool: los suscriptores de UI deben usar BeginInvoke.</summary>
    public event EventHandler<EstadoServicio>? EstadoCambiado;

    public void Iniciar()
    {
        Detener();
        _cts = new CancellationTokenSource();
        _ = BucleAsync(_cts.Token);
    }

    public void Detener()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public async Task<EstadoServicio> VerificarAsync(CancellationToken ct = default)
    {
        EstadoServicio nuevo;
        try
        {
            var salud = await _services.GetRequiredService<HealthApi>().ObtenerAsync(ct);
            nuevo = salud.Db ? EstadoServicio.EnLinea : EstadoServicio.SinBaseDatos;
        }
        catch (Api.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        {
            nuevo = EstadoServicio.SinBaseDatos;
        }
        catch (Exception ex) when (ex is Api.ApiException or HttpRequestException or TaskCanceledException)
        {
            nuevo = EstadoServicio.Detenido;
        }

        if (nuevo != Estado)
        {
            Estado = nuevo;
            EstadoCambiado?.Invoke(this, nuevo);
        }
        return nuevo;
    }

    private async Task BucleAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_intervalo);
        try
        {
            do
            {
                await VerificarAsync(ct);
            } while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Dispose() => Detener();
}
