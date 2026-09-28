using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MiniMarket.Desktop.Api.Models;
using MiniMarket.Desktop.Services;

namespace MiniMarket.Desktop.Api;

/// <summary>
/// Agrega "Authorization: Bearer" a cada petición y renueva el access token (15 min) con el refresh
/// token rotativo: de forma preventiva si está por vencer, o reactiva ante un 401. Un SemaphoreSlim
/// garantiza un único refresh a la vez (la Api revoca toda la familia si un refresh token se reusa).
/// Si la renovación falla, la sesión se da por expirada y la app vuelve al login.
/// </summary>
public sealed class AuthDelegatingHandler : DelegatingHandler
{
    /// <summary>Cliente HTTP sin este handler, usado solo para llamar a auth/refresh.</summary>
    public const string ClienteRefresh = "auth-refresh";

    private static readonly SemaphoreSlim Candado = new(1, 1);
    private readonly SessionService _sesion;
    private readonly IHttpClientFactory _httpFactory;

    public AuthDelegatingHandler(SessionService sesion, IHttpClientFactory httpFactory)
    {
        _sesion = sesion;
        _httpFactory = httpFactory;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (EsRutaAnonima(request))
            return await base.SendAsync(request, ct);

        if (_sesion.AccessTokenPorVencer)
            await RenovarAsync(_sesion.AccessToken, ct);

        // Se bufferiza el cuerpo para poder reenviarlo tras un refresh.
        if (request.Content is not null) await request.Content.LoadIntoBufferAsync(ct);

        var tokenUsado = _sesion.AccessToken;
        Autorizar(request, tokenUsado);
        var respuesta = await base.SendAsync(request, ct);
        if (respuesta.StatusCode != HttpStatusCode.Unauthorized || _sesion.RefreshToken is null)
            return respuesta;

        if (!await RenovarAsync(tokenUsado, ct))
        {
            _sesion.Expirar();
            return respuesta;
        }

        respuesta.Dispose();
        var reintento = await ClonarAsync(request);
        Autorizar(reintento, _sesion.AccessToken);
        return await base.SendAsync(reintento, ct);
    }

    /// <summary>Renueva, salvo que otro hilo ya lo haya hecho mientras se esperaba el candado. True = hay token vigente nuevo.</summary>
    private async Task<bool> RenovarAsync(string? tokenVisto, CancellationToken ct)
    {
        await Candado.WaitAsync(ct);
        try
        {
            if (_sesion.AccessToken != tokenVisto && !_sesion.AccessTokenPorVencer) return true;
            if (_sesion.RefreshToken is null) return false;

            var cliente = _httpFactory.CreateClient(ClienteRefresh);
            using var respuesta = await cliente.PostAsJsonAsync("auth/refresh", new RefreshRequest(_sesion.RefreshToken), ApiHttp.Json, ct);
            if (!respuesta.IsSuccessStatusCode) return false;

            var login = await respuesta.Content.ReadFromJsonAsync<LoginResponse>(ApiHttp.Json, ct);
            if (login is null) return false;
            _sesion.ActualizarTokens(login);
            return true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        finally
        {
            Candado.Release();
        }
    }

    private static bool EsRutaAnonima(HttpRequestMessage request)
    {
        var ruta = request.RequestUri?.OriginalString ?? "";
        return ruta.EndsWith("auth/login", StringComparison.OrdinalIgnoreCase)
            || ruta.EndsWith("auth/refresh", StringComparison.OrdinalIgnoreCase)
            || ruta.EndsWith("health", StringComparison.OrdinalIgnoreCase);
    }

    private static void Autorizar(HttpRequestMessage request, string? token) =>
        request.Headers.Authorization = token is null ? null : new AuthenticationHeaderValue("Bearer", token);

    private static async Task<HttpRequestMessage> ClonarAsync(HttpRequestMessage original)
    {
        var copia = new HttpRequestMessage(original.Method, original.RequestUri) { Version = original.Version };
        foreach (var h in original.Headers) copia.Headers.TryAddWithoutValidation(h.Key, h.Value);
        if (original.Content is not null)
        {
            copia.Content = new ByteArrayContent(await original.Content.ReadAsByteArrayAsync());
            foreach (var h in original.Content.Headers) copia.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
        }
        return copia;
    }
}
