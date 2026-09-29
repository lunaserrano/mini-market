using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Logging;
using MiniMarket.Api;
using MiniMarket.Infrastructure.Security;

namespace MiniMarket.Desktop.Services;

/// <summary>
/// MiniMarket.Api hospedada DENTRO de este proceso: Kestrel escucha solo en 127.0.0.1 con un puerto
/// dinámico, arranca con la ventana y se detiene al salir. No hay servicio Windows: lo único externo
/// que necesita la app es SQL Server. Son las mismas reglas de negocio, permisos, validaciones y
/// auditoría que la versión web, en el entorno "Desktop".
///
/// Configuración: appsettings.json (junto al .exe) + secretos cifrados con DPAPI en
/// %ProgramData%\MiniMarket\appsettings.Secrets.json (ver <see cref="SecretosLocales"/>).
/// </summary>
public sealed class ApiLocal
{
    public const string Entorno = "Desktop";
    public const string OrigenEventLog = "MiniMarket";

    // Mientras la Api no está iniciada, las peticiones van a un puerto cerrado: fallan con
    // HttpRequestException y la UI lo muestra como "sin conexión" en vez de romperse.
    private static readonly Uri SinIniciar = new("http://127.0.0.1:1/api/");

    private WebApplication? _app;

    public Uri BaseAddress { get; private set; } = SinIniciar;

    public bool Iniciada => _app is not null;

    /// <summary>
    /// True si ya hay una connection string guardada en este equipo (o, en desarrollo, en la variable
    /// de entorno ConnectionStrings__DefaultConnection, que la configuración de la Api también lee).
    /// </summary>
    public static bool Configurada
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")))
                return true;
            try
            {
                return SecretosLocales.LeerConnectionString(SecretosLocales.RutaPredeterminada) is not null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException
                                           or System.Security.Cryptography.CryptographicException)
            {
                // Archivo ilegible o copiado de otra PC: hay que volver a configurar la conexión.
                return false;
            }
        }
    }

    /// <summary>
    /// Arranca la Api: aplica migraciones, sincroniza permisos, hace el seed inicial y abre Kestrel.
    /// Lanza si falta la configuración o si SQL Server no responde.
    /// </summary>
    public async Task IniciarAsync(CancellationToken ct = default)
    {
        if (_app is not null) return;

        var builder = ApiHost.CrearBuilder(
            new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory, EnvironmentName = Entorno },
            SecretosLocales.RutaPredeterminada);

        // Puerto 0 = el sistema asigna uno libre: no hay conflictos con otros programas ni nada que
        // configurar. Solo loopback: la Api no es accesible desde la red.
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        ConfigurarLogging(builder.Logging);

        var app = builder.Build();
        try
        {
            await ApiHost.InicializarBaseDatosAsync(app, ct);
            ApiHost.ConfigurarPipeline(app);
            await app.StartAsync(ct);

            var direccion = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault()
                ?? throw new InvalidOperationException("Kestrel no informó la dirección en la que escucha.");
            BaseAddress = new Uri(direccion.TrimEnd('/') + "/api/");
            _app = app;
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    /// <summary>Detiene Kestrel y los servicios en segundo plano (vacía la cola de auditoría).</summary>
    public async Task DetenerAsync()
    {
        if (_app is null) return;
        var app = _app;
        _app = null;
        BaseAddress = SinIniciar;
        try
        {
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await app.StopAsync(limite.Token);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    private static void ConfigurarLogging(ILoggingBuilder logging)
    {
        // Sin consola en una app WinForms. El Visor de eventos solo si el instalador creó el origen:
        // crearlo requiere permisos de administrador y el cajero no los tiene.
        logging.ClearProviders();
        logging.AddDebug();
        if (ExisteOrigenEventLog())
            logging.AddEventLog(o => o.SourceName = OrigenEventLog);
    }

    private static bool ExisteOrigenEventLog()
    {
        try
        {
            return EventLog.SourceExists(OrigenEventLog);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or InvalidOperationException)
        {
            return false;
        }
    }
}
