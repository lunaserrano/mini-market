using MiniMarket.Domain.Entities;

namespace MiniMarket.Application.Interfaces;

public interface IPasswordHasher
{
    string Hash(string plainPassword);
    bool Verify(string plainPassword, string passwordHash);
}

public interface IJwtTokenGenerator
{
    /// <summary>Genera un JWT con claims empresa_id/sucursal_id/usuario_id/rol/username.</summary>
    string GenerarToken(Usuario usuario, string rolCodigo);
}
