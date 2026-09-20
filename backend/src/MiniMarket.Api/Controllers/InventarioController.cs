using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniMarket.Api.Authorization;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Services;
using MiniMarket.Domain.Security;

namespace MiniMarket.Api.Controllers;

[ApiController]
[Route("api/inventario")]
[Authorize]
public class InventarioController : ControllerBase
{
    private readonly InventarioService _service;
    public InventarioController(InventarioService service) => _service = service;

    [HttpGet]
    [HasPermission(Permisos.InventarioVer)]
    public async Task<IActionResult> Listar([FromQuery] int? sucursalId, [FromQuery] int? productoId) =>
        Ok(await _service.ListarAsync(sucursalId, productoId));

    [HttpGet("{productoId:int}/sucursal/{sucursalId:int}")]
    [HasPermission(Permisos.InventarioConsultar)]
    public async Task<IActionResult> ObtenerPuntual(int productoId, int sucursalId) =>
        Ok(await _service.ObtenerAsync(productoId, sucursalId));

    [HttpPost("ajuste")]
    [HasPermission(Permisos.InventarioAjustar)]
    public async Task<IActionResult> Ajustar(AjusteInventarioRequest request)
    {
        await _service.AjustarAsync(request);
        return NoContent();
    }

    [HttpGet("movimientos")]
    [HasPermission(Permisos.InventarioVer)]
    public async Task<IActionResult> ListarMovimientos([FromQuery] int? productoId, [FromQuery] int? sucursalId,
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta) =>
        Ok(await _service.ListarMovimientosAsync(new MovimientoInventarioFiltro(productoId, sucursalId, desde, hasta)));
}
