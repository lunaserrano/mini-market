namespace MiniMarket.Infrastructure.Services;

/// <summary>Nombres de claim compartidos entre JwtTokenGenerator (los emite) y TenantContext (los lee).</summary>
public static class TenantClaimTypes
{
    public const string EmpresaId = "empresa_id";
    public const string SucursalId = "sucursal_id";
    public const string UsuarioId = "usuario_id";
    public const string Rol = "rol";
}
