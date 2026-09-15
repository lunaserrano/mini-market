namespace MiniMarket.Application.DTOs;

public record LoginRequest(string Username, string Password);

public record LoginResponse(string Token, DateTime ExpiraUtc, UsuarioActualDto Usuario);

public record UsuarioActualDto(
    int Id,
    int EmpresaId,
    int? SucursalId,
    string NombreCompleto,
    string Username,
    string Rol
);
