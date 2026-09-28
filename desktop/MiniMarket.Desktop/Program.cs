using Microsoft.Extensions.Hosting;
using MiniMarket.Desktop.Forms;

namespace MiniMarket.Desktop;

/// <summary>
/// Cliente de escritorio MiniMarket (WinForms). NO accede a la base de datos: toda la lógica de negocio
/// vive en MiniMarket.Api (servicio Windows local, http://127.0.0.1:5080). Este proceso solo presenta
/// pantallas y consume la Api por HTTP con JWT.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Dialogs.Excepcion(null, e.Exception);

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
            DisableDefaults = true
        });
        builder.Configuration
            .AddJsonFile("appsettings.json", optional: false)
            // Preferencias del usuario (Sistema > Conexión) sobre los valores instalados.
            .AddJsonFile(AppPaths.ConfigUsuario, optional: true);

        ConfigurarServicios(builder.Services, builder.Configuration);

        using var host = builder.Build();
        Application.Run(new AppFlow(host.Services));
    }

    private static void ConfigurarServicios(IServiceCollection services, IConfiguration configuration)
    {
        var baseUrl = configuration["Api:BaseUrl"] ?? "http://127.0.0.1:5080/api/";
        if (!baseUrl.EndsWith('/')) baseUrl += "/";
        var timeout = TimeSpan.FromSeconds(configuration.GetValue("Api:TimeoutSegundos", 30));

        void Configurar(HttpClient c)
        {
            c.BaseAddress = new Uri(baseUrl);
            c.Timeout = timeout;
            c.DefaultRequestHeaders.UserAgent.ParseAdd($"MiniMarket.Desktop/{Application.ProductVersion.Split('+')[0]}");
        }

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
