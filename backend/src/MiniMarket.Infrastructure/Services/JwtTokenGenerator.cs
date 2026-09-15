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

    public JwtTokenGenerator(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public string GenerarToken(Usuario usuario, string rolCodigo)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Username),
            new(TenantClaimTypes.EmpresaId, usuario.EmpresaId.ToString()),
            new(TenantClaimTypes.UsuarioId, usuario.Id.ToString()),
            new(TenantClaimTypes.Rol, rolCodigo),
            new(ClaimTypes.Name, usuario.NombreCompleto)
        };
        if (usuario.SucursalId is int sucursalId)
            claims.Add(new Claim(TenantClaimTypes.SucursalId, sucursalId.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_settings.ExpirationHours),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
