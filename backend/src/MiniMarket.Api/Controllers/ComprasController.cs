using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniMarket.Api.Authorization;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Services;
using MiniMarket.Domain.Security;

namespace MiniMarket.Api.Controllers;

[ApiController]
[Route("api/compras")]
public class ComprasController : ControllerBase
{
    private readonly CompraService _service;
    public ComprasController(CompraService service) => _service = service;

    [HttpGet]
    [HasPermission(Permisos.ComprasVer)]
    public async Task<IActionResult> Listar([FromQuery] int? sucursalId) => Ok(await _service.ListarAsync(sucursalId));

    [HttpGet("{id:int}")]
    [HasPermission(Permisos.ComprasVer)]
    public async Task<IActionResult> Obtener(int id) => Ok(await _service.ObtenerAsync(id));

    [HttpPost]
    [HasPermission(Permisos.ComprasCrear)]
    public async Task<IActionResult> Crear(CompraCreateDto request)
    {
        var id = await _service.CrearAsync(request);
        return CreatedAtAction(nameof(Obtener), new { id }, id);
    }

    [HttpPost("{id:int}/anular")]
    [HasPermission(Permisos.ComprasAnular)]
    public async Task<IActionResult> Anular(int id)
    {
        await _service.AnularAsync(id);
        return NoContent();
    }
}
