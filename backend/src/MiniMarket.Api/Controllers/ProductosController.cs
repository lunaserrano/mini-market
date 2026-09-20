using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniMarket.Api.Authorization;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Services;
using MiniMarket.Domain.Security;

namespace MiniMarket.Api.Controllers;

[ApiController]
[Route("api/productos")]
[Authorize]
public class ProductosController : ControllerBase
{
    private readonly ProductoService _service;

    public ProductosController(ProductoService service) => _service = service;

    [HttpGet]
    [HasPermission(Permisos.ProductosVer)]
    public async Task<ActionResult<IReadOnlyList<ProductoDto>>> Listar() => Ok(await _service.ListarAsync());

    [HttpGet("buscar")]
    [HasPermission(Permisos.ProductosVer)]
    public async Task<ActionResult<IReadOnlyList<ProductoPosDto>>> Buscar([FromQuery] string termino) =>
        Ok(await _service.BuscarParaPosAsync(termino));

    [HttpGet("{id:int}")]
    [HasPermission(Permisos.ProductosVer)]
    public async Task<ActionResult<ProductoDto>> Obtener(int id) => Ok(await _service.ObtenerAsync(id));

    [HttpPost]
    [HasPermission(Permisos.ProductosGestionar)]
    public async Task<ActionResult<int>> Crear(ProductoCreateDto dto)
    {
        var id = await _service.CrearAsync(dto);
        return CreatedAtAction(nameof(Obtener), new { id }, id);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permisos.ProductosGestionar)]
    public async Task<IActionResult> Actualizar(int id, ProductoUpdateDto dto)
    {
        await _service.ActualizarAsync(id, dto);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permisos.ProductosEliminar)]
    public async Task<IActionResult> Desactivar(int id)
    {
        await _service.DesactivarAsync(id);
        return NoContent();
    }

    [HttpPost("{productoId:int}/tipos-precio")]
    [HasPermission(Permisos.ProductosGestionar)]
    public async Task<ActionResult<int>> AgregarTipoPrecio(int productoId, TipoPrecioCreateDto dto) =>
        Ok(await _service.AgregarTipoPrecioAsync(productoId, dto));

    [HttpPut("{productoId:int}/tipos-precio/{tipoPrecioId:int}")]
    [HasPermission(Permisos.ProductosGestionar)]
    public async Task<IActionResult> ActualizarTipoPrecio(int productoId, int tipoPrecioId, TipoPrecioUpdateDto dto)
    {
        await _service.ActualizarTipoPrecioAsync(productoId, tipoPrecioId, dto);
        return NoContent();
    }

    [HttpDelete("{productoId:int}/tipos-precio/{tipoPrecioId:int}")]
    [HasPermission(Permisos.ProductosGestionar)]
    public async Task<IActionResult> EliminarTipoPrecio(int productoId, int tipoPrecioId)
    {
        await _service.EliminarTipoPrecioAsync(productoId, tipoPrecioId);
        return NoContent();
    }
}
