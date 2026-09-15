using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Services;

namespace MiniMarket.Api.Controllers;

/// <summary>Configuración/mantenimiento de la empresa (nombre, zona horaria, moneda). La moneda es
/// configurable por país en vez de estar fija en el código — ver EmpresaService.</summary>
[ApiController]
[Route("api/empresa")]
[Authorize]
public class EmpresaController : ControllerBase
{
    private readonly EmpresaService _service;
    public EmpresaController(EmpresaService service) => _service = service;

    [HttpGet("actual")]
    public async Task<ActionResult<EmpresaDto>> ObtenerActual() => Ok(await _service.ObtenerActualAsync());

    [HttpPut]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<EmpresaDto>> Actualizar(EmpresaUpdateDto dto) => Ok(await _service.ActualizarAsync(dto));
}
