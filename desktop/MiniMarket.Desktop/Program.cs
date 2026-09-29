using Microsoft.Extensions.Hosting;
using MiniMarket.Desktop.Forms;

namespace MiniMarket.Desktop;

/// <summary>
/// MiniMarket POS de escritorio (WinForms). Todo corre en este proceso: la Api (MiniMarket.Api, ver
/// <see cref="ApiLocal"/>) se hospeda aquí mismo en 127.0.0.1 y las pantallas la consumen por HTTP con
/// JWT, igual que la versión web. Lo único externo que se necesita es SQL Server.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        System.Windows.Forms.Application.ThreadException += (_, e) => Dialogs.Excepcion(null, e.Exception);

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
            DisableDefaults = true
        });
        builder.Configuration
            .AddJsonFile("appsettings.json", optional: false)
            // Preferencias del usuario sobre los valores instalados.
            .AddJsonFile(AppPaths.ConfigUsuario, optional: true);

        ConfigurarServicios(builder.Services, builder.Configuration);

        using var host = builder.Build();
        var api = host.Services.GetRequiredService<ApiLocal>();
        try
        {
            System.Windows.Forms.Application.Run(new AppFlow(host.Services));
        }
        finally
        {
            // Fuera del hilo de UI: Kestrel y AuditoriaWriterService terminan de forma ordenada.
            Task.Run(api.DetenerAsync).Wait(TimeSpan.FromSeconds(15));
        }
    }

    private static void ConfigurarServicios(IServiceCollection services, IConfiguration configuration)
    {
        var timeout = TimeSpan.FromSeconds(configuration.GetValue("Api:TimeoutSegundos", 30));
        // El nombre del equipo identifica la caja en la auditoría: con la Api embebida la IP siempre es 127.0.0.1.
        var userAgent = $"MiniMarket.Desktop/{System.Windows.Forms.Application.ProductVersion.Split('+')[0]} ({Environment.MachineName})";

        // La dirección se lee al crear cada HttpClient: el puerto lo elige Kestrel al iniciar ApiLocal.
        void Configurar(IServiceProvider sp, HttpClient c)
        {
            c.BaseAddress = sp.GetRequiredService<ApiLocal>().BaseAddress;
            c.Timeout = timeout;
            c.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        }

        services.AddSingleton<ApiLocal>();
        services.AddSingleton<TokenStore>();
        services.AddSingleton<SessionService>();
        services.AddSingleton<ApiHealthMonitor>();
        services.AddSingleton<TicketPrinter>();
        services.AddTransient<NavegacionAuditor>();

        services.AddTransient<AuthDelegatingHandler>();
        services.AddHttpClient(AuthDelegatingHandler.ClienteRefresh, Configurar);
        services.AddHttpClient<ApiHttp>(Configurar).AddHttpMessageHandler<AuthDelegatingHandler>();

        services.AddTransient<HealthApi>();
        services.AddTransient<AuthApi>();
        services.AddTransient<EmpresaApi>();
        services.AddTransient<CategoriasApi>();
        services.AddTransient<ClientesApi>();
        services.AddTransient<ProveedoresApi>();
        services.AddTransient<ProductosApi>();
        services.AddTransient<InventarioApi>();
        services.AddTransient<CajaApi>();
        services.AddTransient<VentasApi>();
        services.AddTransient<ComprasApi>();
        services.AddTransient<CreditosApi>();
        services.AddTransient<UsuariosApi>();
        services.AddTransient<RolesApi>();
        services.AddTransient<AuditoriaApi>();
    }
}
