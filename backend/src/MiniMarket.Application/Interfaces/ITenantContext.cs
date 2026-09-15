namespace MiniMarket.Application.Interfaces;

/// <summary>
/// Expone los claims del usuario autenticado (empresa/sucursal/usuario/rol) leídos del JWT.
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
    /// <summary>Código de rol: "admin" | "supervisor" | "cajero".</summary>
    string Rol { get; }
    bool EstaAutenticado { get; }
}
