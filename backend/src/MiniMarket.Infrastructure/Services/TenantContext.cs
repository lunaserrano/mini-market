using Microsoft.AspNetCore.Http;
using MiniMarket.Application.Interfaces;

namespace MiniMarket.Infrastructure.Services;

/// <summary>
/// Lee los claims del JWT de la petición actual (vía IHttpContextAccessor) y los expone como
/// EmpresaId/SucursalId/UsuarioId/Rol fuertemente tipados. Se registra Scoped: una instancia por
/// request. Cada repositorio Dapper recibe estos valores explícitamente en sus parámetros —
/// no hay filtrado automático como con los global query filters de EF Core.
/// </summary>
public class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private System.Security.Claims.ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool EstaAutenticado => User?.Identity?.IsAuthenticated ?? false;

    public int EmpresaId => int.TryParse(User?.FindFirst(TenantClaimTypes.EmpresaId)?.Value, out var v) ? v
        : throw new InvalidOperationException("El token no contiene el claim 'empresa_id'.");

    public int? SucursalId => int.TryParse(User?.FindFirst(TenantClaimTypes.SucursalId)?.Value, out var v) ? v : null;

    public int UsuarioId => int.TryParse(User?.FindFirst(TenantClaimTypes.UsuarioId)?.Value, out var v) ? v
        : throw new InvalidOperationException("El token no contiene el claim 'usuario_id'.");

    public string Rol => User?.FindFirst(TenantClaimTypes.Rol)?.Value
        ?? throw new InvalidOperationException("El token no contiene el claim 'rol'.");
}
