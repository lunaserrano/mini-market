using System.Security.Cryptography;
using System.Text;

namespace MiniMarket.Desktop.Services;

/// <summary>
/// Persiste el refresh token para "Recordar sesión", cifrado con DPAPI en alcance de USUARIO de
/// Windows: solo el mismo usuario, en el mismo equipo, puede descifrarlo. El access token nunca se
/// escribe a disco. Archivo: %LOCALAPPDATA%\MiniMarket\session.dat
/// </summary>
public sealed class TokenStore
{
    private static readonly byte[] Entropia = Encoding.UTF8.GetBytes("MiniMarket.Desktop.Session|3f9d1c6e");
    private readonly string _ruta = Path.Combine(AppPaths.DatosUsuario, "session.dat");

    public void Guardar(string refreshToken)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DatosUsuario);
            var cifrado = ProtectedData.Protect(Encoding.UTF8.GetBytes(refreshToken), Entropia, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(_ruta, cifrado);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            // No poder recordar la sesión no debe impedir trabajar: el usuario volverá a loguearse.
        }
    }

    public string? Leer()
    {
        try
        {
            if (!File.Exists(_ruta)) return null;
            var bytes = ProtectedData.Unprotect(File.ReadAllBytes(_ruta), Entropia, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            Borrar();
            return null;
        }
    }

    public void Borrar()
    {
        try
        {
            if (File.Exists(_ruta)) File.Delete(_ruta);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

public static class AppPaths
{
    public static string DatosUsuario { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MiniMarket");

    /// <summary>Configuración del usuario que sobrescribe appsettings.json (URL de la Api elegida en Sistema > Conexión).</summary>
    public static string ConfigUsuario => Path.Combine(DatosUsuario, "desktop.settings.json");
}
