using System.Security.Cryptography;
using System.Text;
using MiniMarket.Application.Interfaces;

namespace MiniMarket.Infrastructure.Services;

public class RefreshTokenGenerator : IRefreshTokenGenerator
{
    public (string Token, string Hash) Generar()
    {
        // 64 bytes aleatorios → ~86 caracteres base64url. El token no lleva información: es solo una llave opaca.
        var token = Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        return (token, Hashear(token));
    }

    public string Hashear(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
