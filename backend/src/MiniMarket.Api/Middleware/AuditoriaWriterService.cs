using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Application.Services;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Api.Middleware;

/// <summary>
/// Vacía la <see cref="AuditoriaCola"/> y guarda los eventos por lotes en una sola transacción.
/// Al apagar la aplicación cierra la cola y termina de escribir lo pendiente antes de salir.
/// </summary>
public class AuditoriaWriterService : BackgroundService
{
    private const int TamanoLote = 200;
    private const int Reintentos = 3;

    private readonly AuditoriaCola _cola;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AuditoriaWriterService> _logger;

    public AuditoriaWriterService(AuditoriaCola cola, IServiceScopeFactory scopes, ILogger<AuditoriaWriterService> logger)
    {
        _cola = cola;
        _scopes = scopes;
        _logger = logger;
    }

    // No se usa el stoppingToken para leer: al apagar, StopAsync completa la cola y este bucle termina
    // solo cuando ya escribió todo lo que había pendiente.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lector = _cola.Lector;
        var lote = new List<EventoSeguridad>(TamanoLote);

        while (await lector.WaitToReadAsync(CancellationToken.None))
        {
            lote.Clear();
            while (lote.Count < TamanoLote && lector.TryRead(out var evento))
                lote.Add(evento);

            await GuardarAsync(lote);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _cola.Completar();
        await base.StopAsync(cancellationToken);
    }

    private async Task GuardarAsync(List<EventoSeguridad> lote)
    {
        for (var intento = 1; intento <= Reintentos; intento++)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IEventoSeguridadRepository>().RegistrarLoteAsync(lote);
                return;
            }
            catch (Exception ex)
            {
                if (intento == Reintentos)
                {
                    _logger.LogError(ex, "No se pudieron guardar {Cantidad} eventos de auditoría tras {Intentos} intentos", lote.Count, Reintentos);
                    return;
                }
                await Task.Delay(TimeSpan.FromSeconds(intento), CancellationToken.None);
            }
        }
    }
}
