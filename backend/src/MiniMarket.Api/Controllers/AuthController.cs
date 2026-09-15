using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Services;

namespace MiniMarket.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly ITenantContext _tenant;

    public AuthController(AuthService authService, ITenantContext tenant)
    {
        _authService = authService;
        _tenant = tenant;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request) =>
        Ok(await _authService.LoginAsync(request));

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UsuarioActualDto>> Me() =>
        Ok(await _authService.ObtenerActualAsync(_tenant));
}
