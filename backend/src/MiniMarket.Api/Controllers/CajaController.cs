using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniMarket.Api.Authorization;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Services;
using MiniMarket.Domain.Security;

namespace MiniMarket.Api.Controllers;

[ApiController]
[Route("api/caja")]
public class CajaController : ControllerBase
{
    private readonly CajaService _service;
    public CajaController(CajaService service) => _service = service;

    [HttpGet("actual")]
    [HasPermission(Permisos.CajaOperar)]
    public async Task<IActionResult> ObtenerActual()
    {
        var caja = await _service.ObtenerActualAsync();
        return caja is null ? NotFound() : Ok(caja);
    }

    [HttpGet]
    [HasPermission(Permisos.CajaVerTodas)]
    public async Task<IActionResult> Listar([FromQuery] int? sucursalId) => Ok(await _service.ListarAsync(sucursalId));

    [HttpPost("apertura")]
    [HasPermission(Permisos.CajaOperar)]
    public async Task<IActionResult> Abrir(AperturaCajaRequest request) => Ok(await _service.AbrirAsync(request));

    [HttpPost("{id:int}/cierre")]
    [HasPermission(Permisos.CajaOperar)]
    public async Task<IActionResult> Cerrar(int id, CierreCajaRequest request) => Ok(await _service.CerrarAsync(id, request));

    [HttpGet("{id:int}/movimientos")]
    [HasPermission(Permisos.CajaOperar)]
    public async Task<IActionResult> ListarMovimientos(int id) => Ok(await _service.ListarMovimientosAsync(id));

    [HttpPost("{id:int}/movimientos")]
    [HasPermission(Permisos.CajaOperar)]
    public async Task<IActionResult> RegistrarMovimiento(int id, MovimientoCajaCreateDto dto) =>
        Ok(await _service.RegistrarMovimientoAsync(id, dto));
}
