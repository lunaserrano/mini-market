using System.Globalization;
using System.Text;
using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;
using MiniMarket.Domain.Exceptions;
using MiniMarket.Domain.Security;

namespace MiniMarket.Application.Services;

public class RolService
{
    private readonly IRolRepository _rolRepository;
    private readonly PermisoService _permisoService;
    private readonly ITenantContext _tenant;
    private readonly ISeguridadAuditor _auditor;
    private readonly TimeProvider _time;

    public RolService(IRolRepository rolRepository, PermisoService permisoService, ITenantContext tenant, ISeguridadAuditor auditor, TimeProvider time)
    {
        _rolRepository = rolRepository;
        _permisoService = permisoService;
        _tenant = tenant;
        _auditor = auditor;
        _time = time;
    }

    public Task<IReadOnlyList<RolDto>> ListarAsync() => _rolRepository.ListarAsync(_tenant.EmpresaId);

    public async Task<RolDetalleDto> ObtenerAsync(int id)
    {
        var rol = await ObtenerRolAsync(id);
        var permisos = await _permisoService.ResolverAsync(rol);
        var usuarios = await _rolRepository.ContarUsuariosAsync(_tenant.EmpresaId, id);
        return new RolDetalleDto(rol.Id, rol.Codigo, rol.Nombre, rol.Descripcion, rol.EsSistema, usuarios, permisos);
    }

    public async Task<int> CrearAsync(RolCreateDto dto)
    {
        var permisos = ValidarPermisos(dto.Permisos);
        var nombre = dto.Nombre.Trim();
        var codigo = GenerarCodigo(nombre);
        await AsegurarNombreLibreAsync(nombre, codigo, excluirId: null);

        var ahora = _time.GetUtcNow().UtcDateTime;
        var id = await _rolRepository.CrearAsync(new RolCatalogo
        {
            EmpresaId = _tenant.EmpresaId,
            Codigo = codigo,
            Nombre = nombre,
            Descripcion = LimpiarDescripcion(dto.Descripcion),
            EsSistema = false,
            Estado = "A",
            CreadoPorUsuarioId = _tenant.UsuarioId,
            FechaCreacion = ahora
        }, permisos);

        await _auditor.RegistrarAsync(TipoEventoSeguridad.RolCreado, _tenant.EmpresaId, _tenant.UsuarioId, null,
            $"Rol '{nombre}' creado con {permisos.Count} permiso(s)");
        return id;
    }

    public async Task ActualizarAsync(int id, RolUpdateDto dto)
    {
        var rol = await ObtenerRolAsync(id);
        if (rol.EsAdministrador)
            throw new ReglaDeNegocioException("El rol Administrador tiene siempre todos los permisos y no se puede modificar.");

        var permisos = ValidarPermisos(dto.Permisos);
        var nombre = dto.Nombre.Trim();
        // El código es estable: solo el nombre visible cambia, así los claims/JWT ya emitidos no quedan huérfanos.
        await AsegurarNombreLibreAsync(nombre, rol.Codigo, excluirId: id);

        var anteriores = (await _rolRepository.ObtenerCodigosPermisosAsync(id)).ToHashSet();
        var agregados = permisos.Count(p => !anteriores.Contains(p));
        var quitados = anteriores.Count(p => !permisos.Contains(p));

        rol.Nombre = nombre;
        rol.Descripcion = LimpiarDescripcion(dto.Descripcion);
        rol.ModificadoPorUsuarioId = _tenant.UsuarioId;
        rol.FechaModificacion = _time.GetUtcNow().UtcDateTime;
        await _rolRepository.ActualizarAsync(rol, permisos);

        await _auditor.RegistrarAsync(TipoEventoSeguridad.RolEditado, _tenant.EmpresaId, _tenant.UsuarioId, null,
            $"Rol '{nombre}' editado; permisos: +{agregados} −{quitados} (total {permisos.Count})");
    }

    public async Task EliminarAsync(int id)
    {
        var rol = await ObtenerRolAsync(id);
        if (rol.EsSistema)
            throw new ReglaDeNegocioException("Los roles de sistema no se pueden eliminar.");

        var usuarios = await _rolRepository.ContarUsuariosAsync(_tenant.EmpresaId, id);
        if (usuarios > 0)
            throw new ReglaDeNegocioException($"No se puede eliminar el rol: tiene {usuarios} usuario(s) asignado(s). Reasígnelos primero.");

        await _rolRepository.EliminarAsync(_tenant.EmpresaId, id);
        await _auditor.RegistrarAsync(TipoEventoSeguridad.RolEliminado, _tenant.EmpresaId, _tenant.UsuarioId, null, $"Rol '{rol.Nombre}' eliminado");
    }

    private async Task<RolCatalogo> ObtenerRolAsync(int id) =>
        await _rolRepository.ObtenerPorIdAsync(_tenant.EmpresaId, id) ?? throw new EntidadNoEncontradaException("Rol", id);

    private static IReadOnlyCollection<string> ValidarPermisos(IReadOnlyList<string>? permisos)
    {
        var distintos = (permisos ?? []).Distinct(StringComparer.Ordinal).ToList();
        var desconocidos = distintos.Where(p => !Permisos.Existe(p)).ToList();
        if (desconocidos.Count > 0)
            throw new ReglaDeNegocioException($"Permiso(s) desconocido(s): {string.Join(", ", desconocidos)}.");
        return distintos;
    }

    private async Task AsegurarNombreLibreAsync(string nombre, string codigo, int? excluirId)
    {
        var existentes = await _rolRepository.ListarAsync(_tenant.EmpresaId);
        var duplicado = existentes.Any(r => r.Id != excluirId &&
            (string.Equals(r.Nombre, nombre, StringComparison.OrdinalIgnoreCase) || string.Equals(r.Codigo, codigo, StringComparison.Ordinal)));
        if (duplicado)
            throw new ReglaDeNegocioException($"Ya existe un rol con el nombre '{nombre}'.");
    }

    private static string? LimpiarDescripcion(string? descripcion) =>
        string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();

    /// <summary>Slug estable a partir del nombre: minúsculas, sin acentos, [a-z0-9_], máx. 50 caracteres.</summary>
    public static string GenerarCodigo(string nombre)
    {
        var sb = new StringBuilder();
        foreach (var c in nombre.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            var alfanumerico = c is (>= 'a' and <= 'z') or (>= '0' and <= '9');
            if (alfanumerico) sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '_') sb.Append('_');
        }

        var codigo = sb.ToString().Trim('_');
        if (codigo.Length > 50) codigo = codigo[..50].TrimEnd('_');
        if (codigo.Length == 0)
            throw new ReglaDeNegocioException("El nombre del rol debe contener letras o números.");
        return codigo;
    }
}
