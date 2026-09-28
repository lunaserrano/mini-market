using MiniMarket.Application.Interfaces;

namespace MiniMarket.Infrastructure.Sync;

/// <summary>
/// Implementación vacía de <see cref="ISyncService"/>: el sistema local es offline-first y no depende
/// de la nube. Reemplazarla por un AzureSyncService (HttpClient + market.SyncOutbox) cuando se
/// habilite la sincronización; el resto del sistema no necesita cambios.
/// </summary>
public sealed class NoOpSyncService : ISyncService
{
    public Task<SyncResultado> SincronizarAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new SyncResultado(Ejecutado: false, Enviados: 0, Recibidos: 0, Error: null));
}
