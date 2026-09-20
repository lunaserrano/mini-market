using Microsoft.AspNetCore.Mvc;
using MiniMarket.Api.Authorization;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Services;
using MiniMarket.Domain.Security;

namespace MiniMarket.Api.Controllers;

[ApiController]
[Route("api/roles")]
public class RolesController : ControllerBase
{
    private readonly RolService _service;
    public RolesController(RolService service) => _service = service;

    // También lo pueden consultar quienes crean/editan usuarios: necesitan la lista de roles para el selector.
    [HttpGet]
    [HasPermission(Permisos.RolesVer, Permisos.UsuariosCrear, Permisos.UsuariosEditar)]
    public async Task<ActionResult<IReadOnlyList<RolDto>>> Listar() => Ok(await _service.ListarAsync());

    [HttpGet("{id:int}")]
    [HasPermission(Permisos.RolesVer)]
    public async Task<ActionResult<RolDetalleDto>> Obtener(int id) => Ok(await _service.ObtenerAsync(id));

    [HttpPost]
    [HasPermission(Permisos.RolesGestionar)]
    public async Task<ActionResult<int>> Crear(RolCreateDto dto) => Ok(await _service.CrearAsync(dto));

    [HttpPut("{id:int}")]
    [HasPermission(Permisos.RolesGestionar)]
    public async Task<IActionResult> Actualizar(int id, RolUpdateDto dto)
    {
        await _service.ActualizarAsync(id, dto);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permisos.RolesGestionar)]
    public async Task<IActionResult> Eliminar(int id)
    {
        await _service.EliminarAsync(id);
        return NoContent();
    }
}
