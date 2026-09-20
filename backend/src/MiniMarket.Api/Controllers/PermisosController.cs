using Microsoft.AspNetCore.Mvc;
using MiniMarket.Api.Authorization;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Services;
using MiniMarket.Domain.Security;

namespace MiniMarket.Api.Controllers;

[ApiController]
[Route("api/permisos")]
public class PermisosController : ControllerBase
{
    private readonly PermisoService _service;
    public PermisosController(PermisoService service) => _service = service;

    /// <summary>Catálogo completo de permisos agrupado por módulo.</summary>
    [HttpGet]
    [HasPermission(Permisos.RolesVer, Permisos.RolesGestionar)]
    public ActionResult<IReadOnlyList<PermisoModuloDto>> Listar() => Ok(_service.ListarCatalogo());
}
