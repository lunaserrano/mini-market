namespace MiniMarket.Application.DTOs;

public record LoginRequest(string Username, string Password);

public record RefreshRequest(string RefreshToken);

public record LogoutRequest(string? RefreshToken);

public record CambiarPasswordRequest(string PasswordActual, string PasswordNueva);

/// <summary>
/// Token = access token JWT de vida corta; RefreshToken = token opaco rotativo para renovarlo.
/// El refresh token en claro solo existe en esta respuesta (en BD se guarda su hash).
/// </summary>
public record LoginResponse(
    string Token,
    DateTime ExpiraUtc,
    string RefreshToken,
    DateTime RefreshExpiraUtc,
    UsuarioActualDto Usuario
);

public record UsuarioActualDto(
    int Id,
    int EmpresaId,
    int? SucursalId,
    string NombreCompleto,
    string Username,
    string Rol,
    string RolNombre,
    IReadOnlyList<string> Permisos,
    bool DebeCambiarPassword
);
