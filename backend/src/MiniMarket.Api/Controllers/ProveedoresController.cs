using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniMarket.Api.Authorization;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Services;
using MiniMarket.Domain.Security;

namespace MiniMarket.Api.Controllers;

[ApiController]
[Route("api/proveedores")]
public class ProveedoresController : ControllerBase
{
    private readonly ProveedorService _service;
    public ProveedoresController(ProveedorService service) => _service = service;

    [HttpGet]
    [HasPermission(Permisos.ProveedoresVer)]
    public async Task<IActionResult> Listar() => Ok(await _service.ListarAsync());

    [HttpGet("{id:int}")]
    [HasPermission(Permisos.ProveedoresVer)]
    public async Task<IActionResult> Obtener(int id) => Ok(await _service.ObtenerAsync(id));

    [HttpPost]
    [HasPermission(Permisos.ProveedoresGestionar)]
    public async Task<IActionResult> Crear(ProveedorCreateDto dto)
    {
        var id = await _service.CrearAsync(dto);
        return CreatedAtAction(nameof(Obtener), new { id }, id);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permisos.ProveedoresGestionar)]
    public async Task<IActionResult> Actualizar(int id, ProveedorUpdateDto dto)
    {
        await _service.ActualizarAsync(id, dto);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permisos.ProveedoresEliminar)]
    public async Task<IActionResult> Desactivar(int id)
    {
        await _service.DesactivarAsync(id);
        return NoContent();
    }
}
