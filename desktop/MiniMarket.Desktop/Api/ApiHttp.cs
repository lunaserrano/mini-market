using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MiniMarket.Desktop.Api;

/// <summary>
/// Cliente HTTP base (typed client de IHttpClientFactory). El token Bearer y su renovación los agrega
/// <see cref="AuthDelegatingHandler"/>; aquí solo se serializa JSON y se traducen errores a <see cref="ApiException"/>.
/// </summary>
public sealed class ApiHttp
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public ApiHttp(HttpClient http) => _http = http;

    public async Task<T> GetAsync<T>(string url, CancellationToken ct = default) =>
        await LeerAsync<T>(await EnviarAsync(new HttpRequestMessage(HttpMethod.Get, url), ct), ct);

    /// <summary>GET que devuelve null ante un 404 (ej. "no hay caja abierta").</summary>
    public async Task<T?> GetOrDefaultAsync<T>(string url, CancellationToken ct = default) where T : class
    {
        var respuesta = await EnviarAsync(new HttpRequestMessage(HttpMethod.Get, url), ct, permitir404: true);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            respuesta.Dispose();
            return null;
        }
        return await LeerAsync<T>(respuesta, ct);
    }

    public async Task<T> PostAsync<T>(string url, object? cuerpo, CancellationToken ct = default) =>
        await LeerAsync<T>(await EnviarAsync(Crear(HttpMethod.Post, url, cuerpo), ct), ct);

    public async Task PostAsync(string url, object? cuerpo, CancellationToken ct = default) =>
        (await EnviarAsync(Crear(HttpMethod.Post, url, cuerpo), ct)).Dispose();

    public async Task<T> PutAsync<T>(string url, object? cuerpo, CancellationToken ct = default) =>
        await LeerAsync<T>(await EnviarAsync(Crear(HttpMethod.Put, url, cuerpo), ct), ct);

    public async Task PutAsync(string url, object? cuerpo, CancellationToken ct = default) =>
        (await EnviarAsync(Crear(HttpMethod.Put, url, cuerpo), ct)).Dispose();

    public async Task DeleteAsync(string url, CancellationToken ct = default) =>
        (await EnviarAsync(new HttpRequestMessage(HttpMethod.Delete, url), ct)).Dispose();

    /// <summary>
    /// Arma "ruta?clave=valor&amp;..." omitiendo nulos y vacíos. Las fechas viajan en UTC y SIN "Z":
    /// la Api las compara contra columnas datetime2 UTC, y con "Z" el model binding de ASP.NET las
    /// convertiría a hora local del servidor.
    /// </summary>
    public static string Query(string ruta, params (string Clave, object? Valor)[] parametros)
    {
        var partes = parametros
            .Where(p => p.Valor is not null && !(p.Valor is string s && s.Length == 0))
            .Select(p => $"{p.Clave}={Uri.EscapeDataString(Formatear(p.Valor!))}")
            .ToList();
        return partes.Count == 0 ? ruta : $"{ruta}?{string.Join('&', partes)}";

        static string Formatear(object valor) => valor switch
        {
            DateTime d => (d.Kind == DateTimeKind.Utc ? d : d.ToUniversalTime()).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => valor.ToString() ?? ""
        };
    }

    private static HttpRequestMessage Crear(HttpMethod metodo, string url, object? cuerpo) =>
        new(metodo, url) { Content = cuerpo is null ? null : JsonContent.Create(cuerpo, cuerpo.GetType(), options: Json) };

    private async Task<HttpResponseMessage> EnviarAsync(HttpRequestMessage request, CancellationToken ct, bool permitir404 = false)
    {
        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(null,
                "No se pudo conectar con el servicio local de MiniMarket.\n" +
                "Verifique que el servicio 'MiniMarketApi' esté iniciado (services.msc) y la URL en Sistema > Conexión.", inner: ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new ApiException(null, "El servicio local no respondió a tiempo.", inner: ex);
        }

        if (respuesta.IsSuccessStatusCode || (permitir404 && respuesta.StatusCode == HttpStatusCode.NotFound))
            return respuesta;

        using (respuesta)
            throw await CrearExcepcionAsync(respuesta, ct);
    }

    private static async Task<T> LeerAsync<T>(HttpResponseMessage respuesta, CancellationToken ct)
    {
        using (respuesta)
        {
            if (respuesta.StatusCode == HttpStatusCode.NoContent || respuesta.Content.Headers.ContentLength == 0)
                return default!;
            return (await respuesta.Content.ReadFromJsonAsync<T>(Json, ct))!;
        }
    }

    private static async Task<ApiException> CrearExcepcionAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        var texto = await respuesta.Content.ReadAsStringAsync(ct);
        string? mensaje = null;
        Dictionary<string, string[]>? errores = null;
        try
        {
            using var doc = JsonDocument.Parse(texto);
            if (doc.RootElement.TryGetProperty("error", out var e)) mensaje = e.GetString();
            if (doc.RootElement.TryGetProperty("errores", out var errs) && errs.ValueKind == JsonValueKind.Object)
                errores = errs.Deserialize<Dictionary<string, string[]>>(Json);
        }
        catch (JsonException)
        {
            // Cuerpo no JSON (p. ej. 404 de una ruta inexistente): se usa el mensaje genérico por código.
        }

        mensaje ??= respuesta.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "La sesión expiró. Vuelva a iniciar sesión.",
            HttpStatusCode.Forbidden => "No tiene permiso para realizar esta acción.",
            HttpStatusCode.NotFound => "El registro solicitado no existe.",
            HttpStatusCode.TooManyRequests => "Demasiados intentos. Espere un minuto.",
            _ => $"Error del servidor ({(int)respuesta.StatusCode})."
        };
        return new ApiException(respuesta.StatusCode, mensaje, errores);
    }
}
