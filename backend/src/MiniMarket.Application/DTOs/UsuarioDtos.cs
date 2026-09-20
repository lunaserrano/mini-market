namespace MiniMarket.Application.DTOs;

public record UsuarioDto(
    int Id,
    int? SucursalId,
    string NombreCompleto,
    string Username,
    int RolId,
    string Rol,
    string RolNombre,
    string Estado,
    bool Bloqueado,
    DateTime? BloqueadoHasta,
    bool DebeCambiarPassword,
    DateTime? UltimoLoginUtc
);

public record UsuarioCreateDto(int? SucursalId, string NombreCompleto, string Username, string Password, int RolId, bool DebeCambiarPassword = true);

public record UsuarioUpdateDto(int? SucursalId, string NombreCompleto, int RolId);

public record ResetPasswordRequest(string NuevaPassword, bool DebeCambiarPassword = true);
