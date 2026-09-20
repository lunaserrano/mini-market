using Microsoft.AspNetCore.Mvc;
using MiniMarket.Api.Authorization;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Services;
using MiniMarket.Domain.Security;

namespace MiniMarket.Api.Controllers;

[ApiController]
[Route("api/usuarios")]
public class UsuariosController : ControllerBase
{
    private readonly UsuarioService _service;
    public UsuariosController(UsuarioService service) => _service = service;

    [HttpGet]
    [HasPermission(Permisos.UsuariosVer)]
    public async Task<IActionResult> Listar() => Ok(await _service.ListarAsync());

    [HttpPost]
    [HasPermission(Permisos.UsuariosCrear)]
    public async Task<IActionResult> Crear(UsuarioCreateDto dto) => Ok(await _service.CrearAsync(dto));

    [HttpPut("{id:int}")]
    [HasPermission(Permisos.UsuariosEditar)]
    public async Task<IActionResult> Actualizar(int id, UsuarioUpdateDto dto)
    {
        await _service.ActualizarAsync(id, dto);
        return NoContent();
    }

    [HttpPut("{id:int}/estado")]
    [HasPermission(Permisos.UsuariosCambiarEstado)]
    public async Task<IActionResult> CambiarEstado(int id, [FromQuery] bool activo)
    {
        await _service.CambiarEstadoAsync(id, activo);
        return NoContent();
    }

    [HttpPost("{id:int}/reset-password")]
    [HasPermission(Permisos.UsuariosResetPassword)]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordRequest request)
    {
        await _service.ResetPasswordAsync(id, request);
        return NoContent();
    }

    [HttpPut("{id:int}/desbloquear")]
    [HasPermission(Permisos.UsuariosDesbloquear)]
    public async Task<IActionResult> Desbloquear(int id)
    {
        await _service.DesbloquearAsync(id);
        return NoContent();
    }

    [HttpPost("{id:int}/revocar-sesiones")]
    [HasPermission(Permisos.UsuariosEditar)]
    public async Task<IActionResult> RevocarSesiones(int id)
    {
        await _service.RevocarSesionesAsync(id);
        return NoContent();
    }
}
