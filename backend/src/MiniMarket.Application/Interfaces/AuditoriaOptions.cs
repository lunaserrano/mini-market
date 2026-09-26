namespace MiniMarket.Application.Interfaces;

/// <summary>Sección "Auditoria" de appsettings.</summary>
public class AuditoriaOptions
{
    public const string SectionName = "Auditoria";

    /// <summary>
    /// Interruptor global del registro de auditoría en BD (peticiones API, eventos de UI y eventos de seguridad).
    /// Se apaga cuando la tabla crece demasiado; no afecta el resto del sistema, solo deja de escribir auditoría.
    /// </summary>
    public bool Habilitada { get; set; } = true;
}
