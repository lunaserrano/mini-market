using Microsoft.AspNetCore.Authorization;

namespace MiniMarket.Api.Authorization;

/// <summary>
/// Exige que el usuario tenga al menos uno de los permisos indicados (ver Domain/Security/Permisos.cs).
/// Reemplaza a [Authorize(Roles = "...")]: el acceso depende de los permisos del rol, que el admin
/// puede editar, y no de nombres de rol fijos. Se coloca por acción (no a nivel de clase) para que
/// cada endpoint declare exactamente lo que necesita: varios atributos se combinan con AND.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "perm:";

    public HasPermissionAttribute(params string[] permisos)
    {
        if (permisos.Length == 0) throw new ArgumentException("Indique al menos un permiso.", nameof(permisos));
        Permisos = permisos;
        Policy = PolicyPrefix + string.Join('|', permisos);
    }

    public string[] Permisos { get; }
}
