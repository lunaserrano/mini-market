namespace MiniMarket.Application.Interfaces;

/// <summary>
/// Expone los claims del usuario autenticado (empresa/sucursal/usuario/rol/permisos) leídos del JWT.
/// Es la ÚNICA fuente de verdad para EmpresaId/SucursalId en cada Service — nunca se toman
/// del DTO enviado por el cliente, para evitar que un usuario fuerce el id de otra empresa.
/// Como no usamos EF Core (no hay global query filters), cada repositorio Dapper debe recibir
/// explícitamente estos valores y aplicarlos en el WHERE/INSERT de su SQL.
/// </summary>
public interface ITenantContext
{
    int EmpresaId { get; }
    /// <summary>Sucursal activa de la sesión. Null si el usuario no tiene sucursal asignada (ej. admin global).</summary>
    int? SucursalId { get; }
    int UsuarioId { get; }
    /// <summary>Código del rol del usuario (informativo; para decidir accesos usar <see cref="TienePermiso"/>).</summary>
    string Rol { get; }
    /// <summary>Permisos del rol al momento de emitir el token.</summary>
    IReadOnlyCollection<string> Permisos { get; }
    bool TienePermiso(string permiso);
    bool EstaAutenticado { get; }
}
