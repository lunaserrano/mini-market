using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Options;
using MiniMarket.Infrastructure.Services;

namespace MiniMarket.Api.Authorization;

/// <summary>Se cumple si el usuario tiene AL MENOS UNO de los permisos indicados.</summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(IReadOnlyList<string> permisos) => Permisos = permisos;
    public IReadOnlyList<string> Permisos { get; }
}

public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        // Mientras deba cambiar su contraseña, el usuario no puede usar nada con permisos: solo los
        // endpoints que exigen únicamente autenticación (cambiar-password, me, logout, refresh).
        if (context.User.HasClaim(TenantClaimTypes.DebeCambiarPassword, "true"))
            return Task.CompletedTask;

        if (requirement.Permisos.Any(p => context.User.HasClaim(TenantClaimTypes.Permiso, p)))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

/// <summary>Construye dinámicamente la política "perm:a|b" declarada por [HasPermission], sin registrar una por permiso.</summary>
public sealed class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    private readonly ConcurrentDictionary<string, AuthorizationPolicy> _cache = new();

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options) { }

    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(HasPermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
            return base.GetPolicyAsync(policyName);

        var policy = _cache.GetOrAdd(policyName, nombre =>
        {
            var permisos = nombre[HasPermissionAttribute.PolicyPrefix.Length..].Split('|');
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permisos))
                .Build();
        });
        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}

/// <summary>Respuestas 401/403 con el mismo cuerpo { "error": "..." } que el resto de la API (en vez de un cuerpo vacío).</summary>
public sealed class JsonAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            await _default.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var cambioForzado = authorizeResult.Forbidden && context.User.HasClaim(TenantClaimTypes.DebeCambiarPassword, "true");
        context.Response.StatusCode = authorizeResult.Forbidden ? StatusCodes.Status403Forbidden : StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            error = cambioForzado ? "Debe cambiar su contraseña antes de continuar."
                : authorizeResult.Forbidden ? "No tiene permiso para realizar esta operación."
                : "No autenticado o sesión vencida.",
            codigo = cambioForzado ? "cambio_password_requerido" : null
        }));
    }
}
