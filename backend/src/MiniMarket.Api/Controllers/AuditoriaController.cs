using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniMarket.Api.Authorization;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Services;
using MiniMarket.Domain.Security;

namespace MiniMarket.Api.Controllers;

[ApiController]
[Route("api/auditoria")]
public class AuditoriaController : ControllerBase
{
    private readonly AuditoriaService _service;
    public AuditoriaController(AuditoriaService service) => _service = service;

    [HttpGet]
    [HasPermission(Permisos.AuditoriaVer)]
    public async Task<ActionResult<PaginaResultado<EventoSeguridadDto>>> Listar([FromQuery] AuditoriaFiltro filtro) =>
        Ok(await _service.ListarAsync(filtro));

    /// <summary>
    /// El navegador reporta por lotes los clics y cambios de pantalla del usuario. Basta con estar autenticado:
    /// cualquier usuario genera su propia actividad, no solo quien puede consultar la auditoría.
    /// </summary>
    [HttpPost("cliente")]
    [Authorize]
    [RequestSizeLimit(256 * 1024)]
    public IActionResult RegistrarCliente([FromBody] List<EventoClienteDto> eventos)
    {
        _service.RegistrarEventosCliente(eventos);
        return Accepted();
    }

    /// <summary>Tipos de evento disponibles para el filtro de la UI.</summary>
    [HttpGet("tipos")]
    [HasPermission(Permisos.AuditoriaVer)]
    public ActionResult<IReadOnlyList<string>> Tipos() => Ok(TipoEventoSeguridad.Todos);
}
