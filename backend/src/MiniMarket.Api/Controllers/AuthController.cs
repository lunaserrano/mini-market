using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Services;

namespace MiniMarket.Api.Controllers;

/// <summary>
/// Sesión: login, renovación, cierre, cambio de contraseña y datos del usuario actual. Estos endpoints
/// solo exigen autenticación (no permisos), para que también funcionen mientras el usuario deba cambiar su clave.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    public const string RateLimitPolicy = "auth";

    private readonly AuthService _authService;

    public AuthController(AuthService authService) => _authService = authService;

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request) =>
        Ok(await _authService.LoginAsync(request));

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<ActionResult<LoginResponse>> Refresh(RefreshRequest request) =>
        Ok(await _authService.RefrescarAsync(request));

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(LogoutRequest request)
    {
        await _authService.LogoutAsync(request);
        return NoContent();
    }

    [HttpPost("cambiar-password")]
    [Authorize]
    public async Task<ActionResult<LoginResponse>> CambiarPassword(CambiarPasswordRequest request) =>
        Ok(await _authService.CambiarPasswordAsync(request));

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UsuarioActualDto>> Me() =>
        Ok(await _authService.ObtenerActualAsync());
}
