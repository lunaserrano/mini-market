using MiniMarket.Application.DTOs;
using MiniMarket.Application.Interfaces;
using MiniMarket.Application.Interfaces.Repositories;
using MiniMarket.Domain.Exceptions;

namespace MiniMarket.Application.Services;

public class AuthService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthService(IUsuarioRepository usuarioRepository, IPasswordHasher passwordHasher, IJwtTokenGenerator jwtTokenGenerator)
    {
        _usuarioRepository = usuarioRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    /// <summary>
    /// Login multi-tenant: como el username es único por empresa (no global), en este esqueleto de
    /// una sola empresa se asume EmpresaId=1; una evolución SaaS real pediría también el "slug" o
    /// dominio de la empresa en el request de login para resolver el EmpresaId antes de buscar el usuario.
    /// </summary>
    public async Task<LoginResponse> LoginAsync(LoginRequest request, int empresaId = 1)
    {
        var usuario = await _usuarioRepository.ObtenerPorUsernameAsync(empresaId, request.Username)
            ?? throw new ReglaDeNegocioException("Usuario o contraseña incorrectos.");

        if (usuario.Estado != "A")
            throw new ReglaDeNegocioException("El usuario está inactivo.");

        if (!_passwordHasher.Verify(request.Password, usuario.PasswordHash))
            throw new ReglaDeNegocioException("Usuario o contraseña incorrectos.");

        var rolCodigo = await _usuarioRepository.ObtenerCodigoRolAsync(usuario.RolId);
        var token = _jwtTokenGenerator.GenerarToken(usuario, rolCodigo);

        return new LoginResponse(
            token,
            DateTime.UtcNow.AddHours(8),
            new UsuarioActualDto(usuario.Id, usuario.EmpresaId, usuario.SucursalId, usuario.NombreCompleto, usuario.Username, rolCodigo)
        );
    }

    public async Task<UsuarioActualDto> ObtenerActualAsync(ITenantContext tenant)
    {
        var usuario = await _usuarioRepository.ObtenerPorIdAsync(tenant.EmpresaId, tenant.UsuarioId)
            ?? throw new EntidadNoEncontradaException("Usuario", tenant.UsuarioId);

        return new UsuarioActualDto(usuario.Id, usuario.EmpresaId, usuario.SucursalId, usuario.NombreCompleto, usuario.Username, tenant.Rol);
    }
}
