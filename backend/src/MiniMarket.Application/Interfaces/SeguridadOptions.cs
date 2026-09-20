namespace MiniMarket.Application.Interfaces;

/// <summary>Sección "Seguridad" de appsettings.</summary>
public class SeguridadOptions
{
    public const string SectionName = "Seguridad";

    /// <summary>Intentos de login fallidos consecutivos que bloquean la cuenta.</summary>
    public int MaxIntentosFallidos { get; set; } = 5;
    public int MinutosBloqueo { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
