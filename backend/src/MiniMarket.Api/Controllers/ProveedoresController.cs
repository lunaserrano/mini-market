using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Services;

namespace MiniMarket.Api.Controllers;

[ApiController]
[Route("api/proveedores")]
[Authorize(Roles = "admin,supervisor")]
public class ProveedoresController : ControllerBase
{
    private readonly ProveedorService _service;
    public ProveedoresController(ProveedorService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Listar() => Ok(await _service.ListarAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id) => Ok(await _service.ObtenerAsync(id));

    [HttpPost]
    public async Task<IActionResult> Crear(ProveedorCreateDto dto)
    {
        var id = await _service.CrearAsync(dto);
        return CreatedAtAction(nameof(Obtener), new { id }, id);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, ProveedorUpdateDto dto)
    {
        await _service.ActualizarAsync(id, dto);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Desactivar(int id)
    {
        await _service.DesactivarAsync(id);
        return NoContent();
    }
}
