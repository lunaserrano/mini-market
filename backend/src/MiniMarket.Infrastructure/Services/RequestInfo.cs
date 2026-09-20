using Microsoft.AspNetCore.Http;
using MiniMarket.Application.Interfaces;

namespace MiniMarket.Infrastructure.Services;

/// <summary>IP y User-Agent de la petición actual, para la auditoría. Null fuera de un contexto HTTP (seeders, tests).</summary>
public class RequestInfo : IRequestInfo
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RequestInfo(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? Ip => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString() is { Length: > 0 } ua ? ua : null;
}
