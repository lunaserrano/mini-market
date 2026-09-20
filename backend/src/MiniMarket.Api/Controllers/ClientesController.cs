using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniMarket.Api.Authorization;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Services;
using MiniMarket.Domain.Security;

namespace MiniMarket.Api.Controllers;

[ApiController]
[Route("api/clientes")]
public class ClientesController : ControllerBase
{
    private readonly ClienteService _service;
    public ClientesController(ClienteService service) => _service = service;

    // Quien vende a crédito debe poder elegir al cliente en el POS aunque su rol no administre clientes.
    [HttpGet]
    [HasPermission(Permisos.ClientesVer, Permisos.CreditosOtorgar)]
    public async Task<IActionResult> Listar() => Ok(await _service.ListarAsync());

    [HttpGet("{id:int}")]
    [HasPermission(Permisos.ClientesVer)]
    public async Task<IActionResult> Obtener(int id) => Ok(await _service.ObtenerAsync(id));

    [HttpPost]
    [HasPermission(Permisos.ClientesGestionar)]
    public async Task<IActionResult> Crear(ClienteCreateDto dto)
    {
        var id = await _service.CrearAsync(dto);
        return CreatedAtAction(nameof(Obtener), new { id }, id);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permisos.ClientesGestionar)]
    public async Task<IActionResult> Actualizar(int id, ClienteUpdateDto dto)
    {
        await _service.ActualizarAsync(id, dto);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permisos.ClientesEliminar)]
    public async Task<IActionResult> Desactivar(int id)
    {
        await _service.DesactivarAsync(id);
        return NoContent();
    }
}
