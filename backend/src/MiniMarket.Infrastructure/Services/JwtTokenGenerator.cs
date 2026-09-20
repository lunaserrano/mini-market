using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MiniMarket.Application.Interfaces;
using MiniMarket.Domain.Entities;

namespace MiniMarket.Infrastructure.Services;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _settings;
    private readonly TimeProvider _time;

    public JwtTokenGenerator(IOptions<JwtSettings> settings, TimeProvider time)
    {
        _settings = settings.Value;
        _time = time;
    }

    public AccessToken Generar(Usuario usuario, RolCatalogo rol, IReadOnlyCollection<string> permisos)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Username),
            new(TenantClaimTypes.EmpresaId, usuario.EmpresaId.ToString()),
            new(TenantClaimTypes.UsuarioId, usuario.Id.ToString()),
            new(TenantClaimTypes.Rol, rol.Codigo),
            new(ClaimTypes.Name, usuario.NombreCompleto)
        };
        if (usuario.SucursalId is int sucursalId)
            claims.Add(new Claim(TenantClaimTypes.SucursalId, sucursalId.ToString()));
        if (usuario.DebeCambiarPassword)
            claims.Add(new Claim(TenantClaimTypes.DebeCambiarPassword, "true"));
        claims.AddRange(permisos.Select(p => new Claim(TenantClaimTypes.Permiso, p)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var ahora = _time.GetUtcNow().UtcDateTime;
        var expira = ahora.AddMinutes(_settings.AccessTokenMinutes);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: ahora,
            expires: expira,
            signingCredentials: credentials
        );

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expira);
    }
}
