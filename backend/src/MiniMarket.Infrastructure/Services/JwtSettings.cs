namespace MiniMarket.Infrastructure.Services;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    /// <summary>Vida del access token. Corta a propósito: los permisos y el estado del usuario se revalidan al renovarlo con el refresh token.</summary>
    public int AccessTokenMinutes { get; set; } = 15;
}
