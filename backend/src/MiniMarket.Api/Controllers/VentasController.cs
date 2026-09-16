using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Services;

namespace MiniMarket.Api.Controllers;

[ApiController]
[Route("api/ventas")]
[Authorize(Roles = "admin,supervisor,cajero")]
public class VentasController : ControllerBase
{
    private readonly VentaService _service;
    public VentasController(VentaService service) => _service = service;

    [HttpPost]
    public async Task<ActionResult<VentaDto>> Crear(VentaCreateDto request)
    {
        var venta = await _service.CrearAsync(request);
        return CreatedAtAction(nameof(Obtener), new { id = venta.Id }, venta);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int? sucursalId, [FromQuery] int? cajaId, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta) =>
        Ok(await _service.ListarAsync(sucursalId, cajaId, desde, hasta));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id) => Ok(await _service.ObtenerAsync(id));

    [HttpPost("{id:int}/anular")]
    [Authorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> Anular(int id, AnularVentaRequest request)
    {
        await _service.AnularAsync(id, request);
        return NoContent();
    }
}
