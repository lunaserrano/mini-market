using System.Text.Json;
using System.Text.Json.Nodes;

namespace MiniMarket.Desktop.Forms.Configuracion;

/// <summary>
/// URL del servicio MiniMarketApi. Por defecto es el mismo equipo (127.0.0.1); en una red local con
/// varias cajas, apunta a la PC servidor (ej. http://192.168.1.10:5080/api/). Se guarda por usuario en
/// %LOCALAPPDATA%\MiniMarket\desktop.settings.json y requiere reiniciar la app.
/// </summary>
public sealed class ConexionForm : EditDialog
{
    public ConexionForm(IConfiguration configuration) : base("Conexión con el servicio local", 560)
    {
        var url = AgregarTexto("URL de la Api", configuration["Api:BaseUrl"] ?? "http://127.0.0.1:5080/api/");
        var resultado = AgregarAncho(new Label { AutoSize = true, ForeColor = Theme.TextoSuave, Text = "Pruebe la conexión antes de guardar." });
        var probar = Theme.Boton("Probar conexión");
        AgregarAncho(probar).Anchor = AnchorStyles.Left;

        probar.Click += async (_, _) =>
        {
            resultado.Text = "Probando...";
            (resultado.Text, resultado.ForeColor) = await ProbarAsync(Normalizar(url.Text));
        };

        Validar(() => Uri.TryCreate(Normalizar(url.Text), UriKind.Absolute, out var u) && u.Scheme is "http" or "https"
            ? null : "La URL no es válida (ej. http://127.0.0.1:5080/api/).");

        AlGuardar(() =>
        {
            Directory.CreateDirectory(AppPaths.DatosUsuario);
            var json = File.Exists(AppPaths.ConfigUsuario)
                ? JsonNode.Parse(File.ReadAllText(AppPaths.ConfigUsuario)) as JsonObject ?? new JsonObject()
                : new JsonObject();
            json["Api"] ??= new JsonObject();
            json["Api"]!["BaseUrl"] = Normalizar(url.Text);
            File.WriteAllText(AppPaths.ConfigUsuario, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            if (Dialogs.Confirmar(this, "Configuración guardada. Es necesario reiniciar la aplicación.\n¿Reiniciar ahora?"))
                Application.Restart();
            return Task.CompletedTask;
        });
    }

    private static string Normalizar(string url)
    {
        url = url.Trim();
        return url.EndsWith('/') ? url : url + "/";
    }

    private static async Task<(string, Color)> ProbarAsync(string url)
    {
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(5) };
            using var respuesta = await http.GetAsync("health");
            var cuerpo = await respuesta.Content.ReadAsStringAsync();
            var salud = JsonSerializer.Deserialize<HealthDto>(cuerpo, ApiHttp.Json);
            return salud?.Db == true
                ? ($"✔ Conectado. Api {salud.Version} en modo {salud.Modo}, base de datos OK.", Theme.Exito)
                : ("⚠ La Api responde, pero no hay conexión con SQL Server.", Theme.Advertencia);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or UriFormatException)
        {
            return ("✖ No hay respuesta en esa URL: " + ex.Message, Theme.Peligro);
        }
    }
}
