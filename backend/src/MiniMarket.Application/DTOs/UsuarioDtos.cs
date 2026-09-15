namespace MiniMarket.Application.DTOs;

public record UsuarioDto(int Id, int? SucursalId, string NombreCompleto, string Username, string Rol, string Estado);

public record UsuarioCreateDto(int? SucursalId, string NombreCompleto, string Username, string Password, string Rol);

public record UsuarioUpdateDto(int? SucursalId, string NombreCompleto, string Rol);

public record ResetPasswordRequest(string NuevaPassword);
