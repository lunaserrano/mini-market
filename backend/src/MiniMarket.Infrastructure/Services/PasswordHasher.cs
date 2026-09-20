using MiniMarket.Application.Interfaces;

namespace MiniMarket.Infrastructure.Services;

public class PasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 11;

    // Hash falso calculado una sola vez con el mismo work factor: verificarlo cuesta lo mismo que un hash real.
    private static readonly Lazy<string> HashFalso = new(() => BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"), WorkFactor));

    public string Hash(string plainPassword) => BCrypt.Net.BCrypt.HashPassword(plainPassword, WorkFactor);

    public bool Verify(string plainPassword, string passwordHash) => BCrypt.Net.BCrypt.Verify(plainPassword, passwordHash);

    public void SimularVerificacion(string plainPassword) => BCrypt.Net.BCrypt.Verify(plainPassword, HashFalso.Value);
}
