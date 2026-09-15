using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Entities;
using MiniMarket.Domain.Exceptions;

namespace MiniMarket.Application.Services;

public class UsuarioService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IRolRepository _rolRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITenantContext _tenant;

    public UsuarioService(IUsuarioRepository usuarioRepository, IRolRepository rolRepository, IPasswordHasher passwordHasher, ITenantContext tenant)
    {
        _usuarioRepository = usuarioRepository;
        _rolRepository = rolRepository;
        _passwordHasher = passwordHasher;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<UsuarioDto>> ListarAsync()
    {
        var usuarios = await _usuarioRepository.ListarAsync(_tenant.EmpresaId);
        var roles = (await _rolRepository.ListarAsync()).ToDictionary(r => r.Id, r => r.Codigo);
        return usuarios.Select(u => new UsuarioDto(u.Id, u.SucursalId, u.NombreCompleto, u.Username, roles.GetValueOrDefault(u.RolId, "?"), u.Estado)).ToList();
    }

    public async Task<int> CrearAsync(UsuarioCreateDto dto)
    {
        var rol = await _rolRepository.ObtenerPorCodigoAsync(dto.Rol)
            ?? throw new ReglaDeNegocioException($"Rol '{dto.Rol}' no existe.");

        var existente = await _usuarioRepository.ObtenerPorUsernameAsync(_tenant.EmpresaId, dto.Username);
        if (existente is not null)
            throw new ReglaDeNegocioException($"El username '{dto.Username}' ya está en uso en esta empresa.");

        return await _usuarioRepository.CrearAsync(new Usuario
        {
            EmpresaId = _tenant.EmpresaId,
            SucursalId = dto.SucursalId,
            RolId = rol.Id,
            NombreCompleto = dto.NombreCompleto,
            Username = dto.Username,
            PasswordHash = _passwordHasher.Hash(dto.Password),
            CreadoPorUsuarioId = _tenant.UsuarioId,
            FechaCreacion = DateTime.UtcNow
        });
    }

    public async Task ActualizarAsync(int id, UsuarioUpdateDto dto)
    {
        var usuario = await _usuarioRepository.ObtenerPorIdAsync(_tenant.EmpresaId, id)
            ?? throw new EntidadNoEncontradaException("Usuario", id);
        var rol = await _rolRepository.ObtenerPorCodigoAsync(dto.Rol)
            ?? throw new ReglaDeNegocioException($"Rol '{dto.Rol}' no existe.");

        usuario.SucursalId = dto.SucursalId;
        usuario.RolId = rol.Id;
        usuario.NombreCompleto = dto.NombreCompleto;
        usuario.ModificadoPorUsuarioId = _tenant.UsuarioId;
        usuario.FechaModificacion = DateTime.UtcNow;

        await _usuarioRepository.ActualizarAsync(usuario);
    }

    public Task CambiarEstadoAsync(int id, bool activo) => _usuarioRepository.CambiarEstadoAsync(_tenant.EmpresaId, id, activo ? "A" : "I");

    public async Task ResetPasswordAsync(int id, ResetPasswordRequest request)
    {
        _ = await _usuarioRepository.ObtenerPorIdAsync(_tenant.EmpresaId, id) ?? throw new EntidadNoEncontradaException("Usuario", id);
        await _usuarioRepository.ActualizarPasswordAsync(_tenant.EmpresaId, id, _passwordHasher.Hash(request.NuevaPassword));
    }
}
