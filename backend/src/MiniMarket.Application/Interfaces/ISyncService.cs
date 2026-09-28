namespace MiniMarket.Application.Interfaces;

/// <summary>Sección "Sync" de appsettings: sincronización futura de una instalación local con la nube (Azure).</summary>
public class SyncOptions
{
    public const string SectionName = "Sync";

    /// <summary>Apagado por defecto: el sistema local funciona 100% offline sin él.</summary>
    public bool Enabled { get; set; }

    /// <summary>URL base de la API central en Azure (ej. https://minimarket-central.azurewebsites.net/api).</summary>
    public string? AzureApiUrl { get; set; }

    public int IntervaloMinutos { get; set; } = 5;

    /// <summary>Máximo de filas de market.SyncOutbox por lote enviado.</summary>
    public int TamanoLote { get; set; } = 200;
}

/// <summary>Resultado de un ciclo de sincronización.</summary>
public record SyncResultado(bool Ejecutado, int Enviados, int Recibidos, string? Error);

/// <summary>
/// Punto de extensión para sincronizar la base local con la nube. Diseño previsto (ver
/// docs/desktop-instalacion.md, "Hoja de ruta de sincronización"):
///  1. PUSH: leer market.SyncOutbox (EnviadoUtc IS NULL) en lotes y enviarlos a la API central;
///     marcar EnviadoUtc o incrementar Intentos/Error.
///  2. PULL: pedir a la API central los cambios de catálogos con SyncVersion &gt; el último recibido
///     (market.NodoSync) y aplicarlos por SyncId (GUID estable entre nodos, no el Id IDENTITY local).
/// La implementación actual (NoOpSyncService) no hace nada: el sistema es offline-first.
/// </summary>
public interface ISyncService
{
    Task<SyncResultado> SincronizarAsync(CancellationToken cancellationToken);
}
