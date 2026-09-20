using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniMarket.Api.Authorization;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Services;
using MiniMarket.Domain.Security;

namespace MiniMarket.Api.Controllers;

[ApiController]
[Route("api/categorias")]
public class CategoriasController : ControllerBase
{
    private readonly CategoriaService _service;
    public CategoriasController(CategoriaService service) => _service = service;

    [HttpGet]
    [HasPermission(Permisos.CategoriasVer)]
    public async Task<IActionResult> Listar() => Ok(await _service.ListarAsync());

    [HttpGet("{id:int}")]
    [HasPermission(Permisos.CategoriasVer)]
    public async Task<IActionResult> Obtener(int id) => Ok(await _service.ObtenerAsync(id));

    [HttpPost]
    [HasPermission(Permisos.CategoriasGestionar)]
    public async Task<IActionResult> Crear(CategoriaCreateDto dto)
    {
        var id = await _service.CrearAsync(dto);
        return CreatedAtAction(nameof(Obtener), new { id }, id);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permisos.CategoriasGestionar)]
    public async Task<IActionResult> Actualizar(int id, CategoriaUpdateDto dto)
    {
        await _service.ActualizarAsync(id, dto);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permisos.CategoriasEliminar)]
    public async Task<IActionResult> Desactivar(int id)
    {
        await _service.DesactivarAsync(id);
        return NoContent();
    }
}
