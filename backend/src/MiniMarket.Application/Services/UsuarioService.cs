using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;
using MiniMarket.Domain.Exceptions;
using MiniMarket.Domain.Security;

namespace MiniMarket.Application.Services;

public class UsuarioService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IRolRepository _rolRepository;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITenantContext _tenant;
    private readonly ISeguridadAuditor _auditor;
    private readonly TimeProvider _time;

    public UsuarioService(
        IUsuarioRepository usuarioRepository,
        IRolRepository rolRepository,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        ITenantContext tenant,
        ISeguridadAuditor auditor,
        TimeProvider time)
    {
        _usuarioRepository = usuarioRepository;
        _rolRepository = rolRepository;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _tenant = tenant;
        _auditor = auditor;
        _time = time;
    }

    private DateTime Ahora => _time.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<UsuarioDto>> ListarAsync()
    {
        var ahora = Ahora;
        var usuarios = await _usuarioRepository.ListarAsync(_tenant.EmpresaId);
        var roles = (await _rolRepository.ListarAsync(_tenant.EmpresaId)).ToDictionary(r => r.Id);

        return usuarios.Select(u =>
        {
            roles.TryGetValue(u.RolId, out var rol);
            return new UsuarioDto(u.Id, u.SucursalId, u.NombreCompleto, u.Username, u.RolId,
                rol?.Codigo ?? "?", rol?.Nombre ?? "?", u.Estado, u.EstaBloqueado(ahora),
                u.EstaBloqueado(ahora) ? Utc(u.BloqueadoHasta) : null, u.DebeCambiarPassword, Utc(u.UltimoLoginUtc));
        }).ToList();
    }

    // Las columnas DATETIME2 llegan con Kind=Unspecified; sin marcarlas como UTC el JSON no lleva "Z" y el navegador las lee como hora local.
    private static DateTime? Utc(DateTime? valor) => valor is DateTime v ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : null;

    public async Task<int> CrearAsync(UsuarioCreateDto dto)
    {
        var rol = await ObtenerRolAsync(dto.RolId);
        await ValidarSucursalAsync(dto.SucursalId);

        var username = dto.Username.Trim();
        if (await _usuarioRepository.ObtenerPorUsernameAsync(_tenant.EmpresaId, username) is not null)
            throw new ReglaDeNegocioException($"El username '{username}' ya está en uso en esta empresa.");

        var id = await _usuarioRepository.CrearAsync(new Usuario
        {
            EmpresaId = _tenant.EmpresaId,
            SucursalId = dto.SucursalId,
            RolId = rol.Id,
            NombreCompleto = dto.NombreCompleto.Trim(),
            Username = username,
            PasswordHash = _passwordHasher.Hash(dto.Password),
            DebeCambiarPassword = dto.DebeCambiarPassword,
            CreadoPorUsuarioId = _tenant.UsuarioId,
            FechaCreacion = Ahora
        });

        await _auditor.RegistrarAsync(TipoEventoSeguridad.UsuarioCreado, _tenant.EmpresaId, _tenant.UsuarioId, id,
            $"Usuario '{username}' creado con el rol '{rol.Nombre}'");
        return id;
    }

    public async Task ActualizarAsync(int id, UsuarioUpdateDto dto)
    {
        var usuario = await ObtenerUsuarioAsync(id);
        var rolNuevo = await ObtenerRolAsync(dto.RolId);
        await ValidarSucursalAsync(dto.SucursalId);

        var cambiaRol = usuario.RolId != rolNuevo.Id;
        RolCatalogo? rolActual = null;
        if (cambiaRol)
        {
            if (id == _tenant.UsuarioId)
                throw new ReglaDeNegocioException("No puede cambiar su propio rol.");

            rolActual = await _rolRepository.ObtenerPorIdAsync(_tenant.EmpresaId, usuario.RolId);
            if (rolActual is { EsAdministrador: true } && !rolNuevo.EsAdministrador)
                await AsegurarOtroAdministradorAsync(usuario);
        }

        usuario.SucursalId = dto.SucursalId;
        usuario.RolId = rolNuevo.Id;
        usuario.NombreCompleto = dto.NombreCompleto.Trim();
        usuario.ModificadoPorUsuarioId = _tenant.UsuarioId;
        usuario.FechaModificacion = Ahora;
        await _usuarioRepository.ActualizarAsync(usuario);

        if (cambiaRol)
            await _refreshTokens.RevocarTodosDeUsuarioAsync(id, Ahora); // que el nuevo rol aplique de inmediato al renovar

        await _auditor.RegistrarAsync(TipoEventoSeguridad.UsuarioEditado, _tenant.EmpresaId, _tenant.UsuarioId, id,
            cambiaRol ? $"Datos actualizados; rol '{rolActual?.Nombre ?? "?"}' → '{rolNuevo.Nombre}'" : "Datos actualizados");
    }

    public async Task CambiarEstadoAsync(int id, bool activo)
    {
        var usuario = await ObtenerUsuarioAsync(id);

        if (!activo)
        {
            if (id == _tenant.UsuarioId)
                throw new ReglaDeNegocioException("No puede desactivar su propia cuenta.");

            var rol = await _rolRepository.ObtenerPorIdAsync(_tenant.EmpresaId, usuario.RolId);
            if (rol is { EsAdministrador: true })
                await AsegurarOtroAdministradorAsync(usuario);
        }

        await _usuarioRepository.CambiarEstadoAsync(_tenant.EmpresaId, id, activo ? "A" : "I", _tenant.UsuarioId);

        if (!activo)
            await _refreshTokens.RevocarTodosDeUsuarioAsync(id, Ahora);

        await _auditor.RegistrarAsync(TipoEventoSeguridad.UsuarioEstado, _tenant.EmpresaId, _tenant.UsuarioId, id,
            activo ? "Usuario activado" : "Usuario desactivado");
    }

    public async Task ResetPasswordAsync(int id, ResetPasswordRequest request)
    {
        _ = await ObtenerUsuarioAsync(id);

        await _usuarioRepository.ActualizarPasswordAsync(
            _tenant.EmpresaId, id, _passwordHasher.Hash(request.NuevaPassword), request.DebeCambiarPassword, _tenant.UsuarioId);
        await _refreshTokens.RevocarTodosDeUsuarioAsync(id, Ahora);

        await _auditor.RegistrarAsync(TipoEventoSeguridad.PasswordReset, _tenant.EmpresaId, _tenant.UsuarioId, id,
            request.DebeCambiarPassword ? "Contraseña restablecida; deberá cambiarla al iniciar sesión" : "Contraseña restablecida");
    }

    public async Task DesbloquearAsync(int id)
    {
        _ = await ObtenerUsuarioAsync(id);
        await _usuarioRepository.DesbloquearAsync(_tenant.EmpresaId, id);
        await _auditor.RegistrarAsync(TipoEventoSeguridad.Desbloqueo, _tenant.EmpresaId, _tenant.UsuarioId, id);
    }

    public async Task RevocarSesionesAsync(int id)
    {
        _ = await ObtenerUsuarioAsync(id);
        await _refreshTokens.RevocarTodosDeUsuarioAsync(id, Ahora);
        await _auditor.RegistrarAsync(TipoEventoSeguridad.SesionesRevocadas, _tenant.EmpresaId, _tenant.UsuarioId, id);
    }

    private async Task<Usuario> ObtenerUsuarioAsync(int id) =>
        await _usuarioRepository.ObtenerPorIdAsync(_tenant.EmpresaId, id) ?? throw new EntidadNoEncontradaException("Usuario", id);

    /// <summary>Resuelve el rol dentro de la empresa del solicitante: un RolId de otra empresa se trata como inexistente.</summary>
    private async Task<RolCatalogo> ObtenerRolAsync(int rolId) =>
        await _rolRepository.ObtenerPorIdAsync(_tenant.EmpresaId, rolId) ?? throw new ReglaDeNegocioException("El rol indicado no existe.");

    private async Task ValidarSucursalAsync(int? sucursalId)
    {
        if (sucursalId is int id && !await _usuarioRepository.ExisteSucursalAsync(_tenant.EmpresaId, id))
            throw new ReglaDeNegocioException("La sucursal indicada no existe.");
    }

    /// <summary>Evita dejar a la empresa sin ningún administrador activo (desactivándolo o quitándole el rol).</summary>
    private async Task AsegurarOtroAdministradorAsync(Usuario usuario)
    {
        if (usuario.Estado != "A") return; // un admin ya inactivo no cuenta
        if (await _usuarioRepository.ContarAdministradoresActivosAsync(_tenant.EmpresaId, usuario.Id) < 1)
            throw new ReglaDeNegocioException("Debe existir al menos un administrador activo en la empresa.");
    }
}
