namespace MiniMarket.Infrastructure.Services;

/// <summary>Nombres de claim compartidos entre JwtTokenGenerator (los emite) y TenantContext/PermissionHandler (los leen).</summary>
public static class TenantClaimTypes
{
    public const string EmpresaId = "empresa_id";
    public const string SucursalId = "sucursal_id";
    public const string UsuarioId = "usuario_id";
    public const string Rol = "rol";
    /// <summary>Un claim por cada permiso del rol.</summary>
    public const string Permiso = "permiso";
    /// <summary>"true" mientras el usuario deba cambiar su contraseña: solo puede usar los endpoints de sesión.</summary>
    public const string DebeCambiarPassword = "debe_cambiar_password";
}
