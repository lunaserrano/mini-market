using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniMarket.Api.Authorization;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Services;
using MiniMarket.Domain.Security;

namespace MiniMarket.Api.Controllers;

[ApiController]
[Route("api/ventas")]
public class VentasController : ControllerBase
{
    private readonly VentaService _service;
    public VentasController(VentaService service) => _service = service;

    [HttpPost]
    [HasPermission(Permisos.VentasCrear)]
    public async Task<ActionResult<VentaDto>> Crear(VentaCreateDto request)
    {
        var venta = await _service.CrearAsync(request);
        return CreatedAtAction(nameof(Obtener), new { id = venta.Id }, venta);
    }

    [HttpGet]
    [HasPermission(Permisos.VentasVer)]
    public async Task<IActionResult> Listar(
        [FromQuery] int? sucursalId, [FromQuery] int? cajaId, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta) =>
        Ok(await _service.ListarAsync(sucursalId, cajaId, desde, hasta));

    [HttpGet("{id:int}")]
    [HasPermission(Permisos.VentasVer)]
    public async Task<IActionResult> Obtener(int id) => Ok(await _service.ObtenerAsync(id));

    [HttpPost("{id:int}/anular")]
    [HasPermission(Permisos.VentasAnular)]
    public async Task<IActionResult> Anular(int id, AnularVentaRequest request)
    {
        await _service.AnularAsync(id, request);
        return NoContent();
    }
}
