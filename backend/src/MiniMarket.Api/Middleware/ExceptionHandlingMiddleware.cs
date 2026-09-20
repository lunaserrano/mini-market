using System.Net;
using System.Text.Json;
using MiniMarket.Domain.Exceptions;

namespace MiniMarket.Api.Middleware;

/// <summary>Traduce excepciones de dominio a respuestas HTTP consistentes ({ "error": "..." }).</summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var statusCode = ex switch
            {
                EntidadNoEncontradaException => HttpStatusCode.NotFound,
                StockInsuficienteException => HttpStatusCode.Conflict,
                PagosInsuficientesException => HttpStatusCode.Conflict,
                CajaYaAbiertaException => HttpStatusCode.Conflict,
                CajaCerradaException => HttpStatusCode.Conflict,
                CredencialesInvalidasException => HttpStatusCode.Unauthorized,
                NoAutenticadoException => HttpStatusCode.Unauthorized,
                PermisoDenegadoException => HttpStatusCode.Forbidden,
                CuentaBloqueadaException => HttpStatusCode.Locked,
                ReglaDeNegocioException => HttpStatusCode.BadRequest,
                ValidacionException => HttpStatusCode.BadRequest,
                DomainException => HttpStatusCode.BadRequest,
                _ => HttpStatusCode.InternalServerError
            };

            if (statusCode == HttpStatusCode.InternalServerError)
                _logger.LogError(ex, "Error no controlado procesando {Path}", context.Request.Path);

            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";

            object cuerpo = ex is ValidacionException validacion
                ? new { error = validacion.Message, errores = validacion.Errores }
                : new { error = statusCode == HttpStatusCode.InternalServerError ? "Ocurrió un error inesperado." : ex.Message };
            await context.Response.WriteAsync(JsonSerializer.Serialize(cuerpo));
        }
    }
}
