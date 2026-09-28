namespace MiniMarket.Desktop.Services;

/// <summary>
/// Reporta a la auditoría de la Api qué pantallas abre el usuario (NAVEGACION_UI, el mismo tipo que
/// registra el frontend web). Es "best effort": nunca bloquea ni muestra errores al usuario.
/// </summary>
public sealed class NavegacionAuditor
{
    private readonly AuditoriaApi _api;

    public NavegacionAuditor(AuditoriaApi api) => _api = api;

    public void Registrar(string ruta, string titulo)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _api.RegistrarClienteAsync(new[]
                {
                    new EventoClienteDto("NAVEGACION_UI", DateTime.UtcNow, "desktop/" + ruta, "Abrió " + titulo,
                        new Dictionary<string, string?> { ["cliente"] = "WinForms", ["equipo"] = Environment.MachineName })
                });
            }
            catch
            {
                // La auditoría de navegación nunca debe afectar la operación.
            }
        });
    }
}
