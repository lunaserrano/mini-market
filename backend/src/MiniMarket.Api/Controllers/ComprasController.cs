using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Services;

namespace MiniMarket.Api.Controllers;

[ApiController]
[Route("api/compras")]
[Authorize(Roles = "admin,supervisor")]
public class ComprasController : ControllerBase
{
    private readonly CompraService _service;
    public ComprasController(CompraService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int? sucursalId) => Ok(await _service.ListarAsync(sucursalId));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id) => Ok(await _service.ObtenerAsync(id));

    [HttpPost]
    public async Task<IActionResult> Crear(CompraCreateDto request)
    {
        var id = await _service.CrearAsync(request);
        return CreatedAtAction(nameof(Obtener), new { id }, id);
    }

    [HttpPost("{id:int}/anular")]
    public async Task<IActionResult> Anular(int id)
    {
        await _service.AnularAsync(id);
        return NoContent();
    }
}
