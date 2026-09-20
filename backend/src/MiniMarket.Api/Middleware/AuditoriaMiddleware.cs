using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Mvc.Controllers;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Services;
using MiniMarket.Domain.Entities;
using MiniMarket.Domain.Security;
using MiniMarket.Infrastructure.Services;

namespace MiniMarket.Api.Middleware;

/// <summary>
/// Registra en la auditoría CADA petición HTTP que llega al backend (cualquier método, endpoint y resultado,
/// incluidos 401/403/404/429/500): quién, desde dónde, qué endpoint, con qué datos, qué respondió y cuánto tardó.
///
/// Va antes de ExceptionHandlingMiddleware para ver el código de estado definitivo, y después de que
/// UseAuthentication pueble HttpContext.User (es el mismo objeto), así que al terminar ya conoce al usuario.
/// El registro se encola y lo persiste <see cref="AuditoriaWriterService"/>: no añade una escritura a BD a la respuesta.
///
/// Se omiten a propósito: preflights CORS (OPTIONS, los emite el navegador, no el usuario), Swagger y el endpoint donde
/// el navegador reporta sus clics (registrarlo generaría un evento por cada lote de eventos).
/// </summary>
public class AuditoriaMiddleware
{
    private const int MaxBytesCuerpo = 64 * 1024;
    private static readonly HashSet<string> MetodosConCuerpo = new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

    private readonly RequestDelegate _next;
    private readonly ILogger<AuditoriaMiddleware> _logger;

    public AuditoriaMiddleware(RequestDelegate next, ILogger<AuditoriaMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuditoriaCola cola, TimeProvider time)
    {
        if (Omitir(context.Request))
        {
            await _next(context);
            return;
        }

        var inicio = time.GetUtcNow().UtcDateTime;
        var cronometro = Stopwatch.StartNew();
        var cuerpo = await LeerCuerpoAsync(context.Request);

        try
        {
            await _next(context);
        }
        finally
        {
            cronometro.Stop();
            try
            {
                if (!cola.Encolar(Construir(context, inicio, cronometro.ElapsedMilliseconds, cuerpo)))
                    _logger.LogWarning("Cola de auditoría llena: se perdió el registro de {Metodo} {Ruta}", context.Request.Method, context.Request.Path);
            }
            catch (Exception ex)
            {
                // La auditoría nunca debe romper la respuesta al usuario.
                _logger.LogError(ex, "No se pudo registrar la petición {Metodo} {Ruta}", context.Request.Method, context.Request.Path);
            }
        }
    }

    private static bool Omitir(HttpRequest request) =>
        HttpMethods.IsOptions(request.Method)
        || request.Path.StartsWithSegments("/swagger")
        || (HttpMethods.IsPost(request.Method) && request.Path.Equals("/api/auditoria/cliente", StringComparison.OrdinalIgnoreCase));

    /// <summary>Lee el cuerpo JSON (acotado) y deja el stream listo para que MVC lo lea de nuevo.</summary>
    private static async Task<string?> LeerCuerpoAsync(HttpRequest request)
    {
        if (!MetodosConCuerpo.Contains(request.Method)) return null;
        if (request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) != true) return null;
        if (request.ContentLength is null or 0 or > MaxBytesCuerpo) return null;

        request.EnableBuffering();
        using var lector = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var texto = await lector.ReadToEndAsync();
        request.Body.Position = 0;
        return texto;
    }

    private static EventoSeguridad Construir(HttpContext context, DateTime inicioUtc, long duracionMs, string? cuerpo)
    {
        var request = context.Request;
        var accion = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        var ruta = request.Path + request.QueryString;

        return new EventoSeguridad
        {
            EmpresaId = LeerEntero(context, TenantClaimTypes.EmpresaId),
            ActorUsuarioId = LeerEntero(context, TenantClaimTypes.UsuarioId),
            Tipo = TipoEventoSeguridad.AccionApi,
            Origen = OrigenEvento.Api,
            Metodo = request.Method,
            Ruta = AuditoriaDatos.Truncar(ruta, 300),
            StatusCode = (short)context.Response.StatusCode,
            DuracionMs = (int)Math.Min(duracionMs, int.MaxValue),
            Detalle = accion is null ? null : $"{accion.ControllerName}.{accion.ActionName}",
            Datos = AuditoriaDatos.SanitizarJson(cuerpo),
            Ip = AuditoriaDatos.Truncar(context.Connection.RemoteIpAddress?.ToString(), 45),
            UserAgent = AuditoriaDatos.Truncar(request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null, 250),
            FechaUtc = inicioUtc
        };
    }

    private static int? LeerEntero(HttpContext context, string claim) =>
        int.TryParse(context.User.FindFirst(claim)?.Value, out var valor) ? valor : null;
}
