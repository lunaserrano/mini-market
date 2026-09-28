using System.Net;

namespace MiniMarket.Desktop.Api;

/// <summary>
/// Error devuelto por la Api (cuerpo { "error": "...", "errores": { campo: [..] } } de
/// ExceptionHandlingMiddleware) o fallo de conexión con el servicio local (StatusCode = null).
/// </summary>
public sealed class ApiException : Exception
{
    public HttpStatusCode? StatusCode { get; }
    public IReadOnlyDictionary<string, string[]> Errores { get; }

    public ApiException(HttpStatusCode? statusCode, string mensaje, IReadOnlyDictionary<string, string[]>? errores = null, Exception? inner = null)
        : base(mensaje, inner)
    {
        StatusCode = statusCode;
        Errores = errores ?? new Dictionary<string, string[]>();
    }

    public bool SinConexion => StatusCode is null;

    /// <summary>Mensaje listo para mostrar: incluye los errores de validación por campo, si los hay.</summary>
    public string MensajeCompleto =>
        Errores.Count == 0
            ? Message
            : Message + Environment.NewLine + Environment.NewLine +
              string.Join(Environment.NewLine, Errores.SelectMany(e => e.Value.Select(m => "• " + m)));
}
