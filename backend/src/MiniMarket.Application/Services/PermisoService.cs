using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;
using MiniMarket.Domain.Security;

namespace MiniMarket.Application.Services;

public class PermisoService
{
    private readonly IRolRepository _rolRepository;

    public PermisoService(IRolRepository rolRepository)
    {
        _rolRepository = rolRepository;
    }

    /// <summary>Catálogo completo agrupado por módulo (para pintar la matriz de permisos en la UI).</summary>
    public IReadOnlyList<PermisoModuloDto> ListarCatalogo() =>
        Permisos.Catalogo
            .GroupBy(p => p.Modulo)
            .Select(g => new PermisoModuloDto(g.Key, g.Select(p => new PermisoDto(p.Codigo, p.Modulo, p.Nombre)).ToList()))
            .ToList();

    /// <summary>
    /// Permisos efectivos de un rol: el Administrador siempre tiene todo el catálogo (no depende de filas
    /// en RolPermiso, así no puede quedarse sin acceso); el resto, lo asignado, descartando códigos que
    /// ya no existan en el catálogo.
    /// </summary>
    public async Task<IReadOnlyList<string>> ResolverAsync(RolCatalogo rol)
    {
        if (rol.EsAdministrador) return Permisos.Todos;

        var asignados = await _rolRepository.ObtenerCodigosPermisosAsync(rol.Id);
        return asignados.Where(Permisos.Existe).ToList();
    }
}
