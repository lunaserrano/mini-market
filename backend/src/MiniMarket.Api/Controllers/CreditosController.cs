using Microsoft.AspNetCore.Mvc;
using MiniMarket.Api.Authorization;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Services;
using MiniMarket.Domain.Security;

namespace MiniMarket.Api.Controllers;

[ApiController]
[Route("api/creditos")]
public class CreditosController : ControllerBase
{
    private readonly CreditoService _service;
    public CreditosController(CreditoService service) => _service = service;

    [HttpGet]
    [HasPermission(Permisos.CreditosVer)]
    public async Task<IActionResult> Listar([FromQuery] int? clienteId, [FromQuery] string? estado) =>
        Ok(await _service.ListarAsync(clienteId, estado));

    [HttpGet("{id:int}")]
    [HasPermission(Permisos.CreditosVer)]
    public async Task<IActionResult> Obtener(int id) => Ok(await _service.ObtenerAsync(id));

    [HttpPost("{id:int}/abonos")]
    [HasPermission(Permisos.CreditosAbonar)]
    public async Task<ActionResult<CreditoDto>> Abonar(int id, AbonoCreditoCreateDto request) =>
        Ok(await _service.AbonarAsync(id, request));
}
