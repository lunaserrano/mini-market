using MiniMarket.Application.Interfaces;

namespace MiniMarket.Api.Middleware;

/// <summary>
/// Dispara <see cref="ISyncService"/> cada Sync:IntervaloMinutos. Con Sync:Enabled=false (por defecto)
/// termina de inmediato: no hay ningún tráfico hacia internet. Un fallo de sync nunca detiene la Api
/// (sin conexión es el estado normal de una instalación offline): se registra y se reintenta en el
/// siguiente ciclo.
/// </summary>
public sealed class SyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SyncOptions _opciones;
    private readonly ILogger<SyncBackgroundService> _logger;

    public SyncBackgroundService(IServiceScopeFactory scopeFactory, SyncOptions opciones, ILogger<SyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _opciones = opciones;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opciones.Enabled)
        {
            _logger.LogInformation("Sincronización con la nube deshabilitada (modo offline).");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, _opciones.IntervaloMinutos)));
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var resultado = await scope.ServiceProvider.GetRequiredService<ISyncService>().SincronizarAsync(stoppingToken);
                if (resultado.Error is not null)
                    _logger.LogWarning("Sincronización incompleta: {Error}", resultado.Error);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Falló el ciclo de sincronización; se reintentará.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
