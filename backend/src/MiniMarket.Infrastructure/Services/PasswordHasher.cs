using MiniMarket.Application.Interfaces;

namespace MiniMarket.Infrastructure.Services;

public class PasswordHasher : IPasswordHasher
{
    public string Hash(string plainPassword) => BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: 11);

    public bool Verify(string plainPassword, string passwordHash) => BCrypt.Net.BCrypt.Verify(plainPassword, passwordHash);
}
