using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Services;

namespace MiniMarket.Api.Controllers;

[ApiController]
[Route("api/inventario")]
[Authorize]
public class InventarioController : ControllerBase
{
    private readonly InventarioService _service;
    public InventarioController(InventarioService service) => _service = service;

    [HttpGet]
    [Authorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> Listar([FromQuery] int? sucursalId, [FromQuery] int? productoId) =>
        Ok(await _service.ListarAsync(sucursalId, productoId));

    [HttpGet("{productoId:int}/sucursal/{sucursalId:int}")]
    [Authorize(Roles = "admin,supervisor,cajero")]
    public async Task<IActionResult> ObtenerPuntual(int productoId, int sucursalId) =>
        Ok(await _service.ObtenerAsync(productoId, sucursalId));

    [HttpPost("ajuste")]
    [Authorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> Ajustar(AjusteInventarioRequest request)
    {
        await _service.AjustarAsync(request);
        return NoContent();
    }

    [HttpGet("movimientos")]
    [Authorize(Roles = "admin,supervisor")]
    public async Task<IActionResult> ListarMovimientos([FromQuery] int? productoId, [FromQuery] int? sucursalId,
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta) =>
        Ok(await _service.ListarMovimientosAsync(new MovimientoInventarioFiltro(productoId, sucursalId, desde, hasta)));
}
