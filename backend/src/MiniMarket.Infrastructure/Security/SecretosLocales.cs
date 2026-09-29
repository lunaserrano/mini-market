using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MiniMarket.Infrastructure.Security;

/// <summary>
/// Archivo de secretos del modo Desktop (connection string y Jwt:Key cifrados con <see cref="DpapiProtector"/>).
/// Vive en %ProgramData%\MiniMarket: fuera de la carpeta del programa, así sobrevive a las
/// actualizaciones y lo comparten todos los usuarios de Windows del equipo.
///
/// Lo usan MiniMarket.Desktop (configuración de la conexión desde la app) y MiniMarket.ConfigTool
/// (instalación por script), que enlaza este mismo archivo fuente.
/// </summary>
public static class SecretosLocales
{
    public const string NombreArchivo = "appsettings.Secrets.json";

    public static string CarpetaPredeterminada { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MiniMarket");

    public static string RutaPredeterminada { get; } = Path.Combine(CarpetaPredeterminada, NombreArchivo);

    /// <summary>Connection string descifrada, o null si el archivo no existe o no tiene una.</summary>
    [SupportedOSPlatform("windows")]
    public static string? LeerConnectionString(string ruta) =>
        LeerValor(ruta, "ConnectionStrings", "DefaultConnection");

    [SupportedOSPlatform("windows")]
    public static string? LeerJwtKey(string ruta) => LeerValor(ruta, "Jwt", "Key");

    /// <summary>
    /// Escribe la connection string cifrada. Conserva la Jwt:Key existente (regenerarla invalida las
    /// sesiones abiertas) salvo <paramref name="rotarJwt"/>; si no hay una, la genera.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static void Guardar(string ruta, string connectionString, bool rotarJwt = false)
    {
        var existente = File.Exists(ruta) ? JsonNode.Parse(File.ReadAllText(ruta)) as JsonObject : null;
        var jwtKeyCifrada = existente?["Jwt"]?["Key"]?.GetValue<string>();
        if (jwtKeyCifrada is null || rotarJwt)
            jwtKeyCifrada = DpapiProtector.Cifrar(Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)));

        var json = new JsonObject
        {
            ["ConnectionStrings"] = new JsonObject { ["DefaultConnection"] = DpapiProtector.Cifrar(connectionString) },
            ["Jwt"] = new JsonObject { ["Key"] = jwtKeyCifrada }
        };

        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        File.WriteAllText(ruta, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    [SupportedOSPlatform("windows")]
    private static string? LeerValor(string ruta, string seccion, string clave)
    {
        if (!File.Exists(ruta)) return null;
        var valor = JsonNode.Parse(File.ReadAllText(ruta))?[seccion]?[clave]?.GetValue<string>();
        return string.IsNullOrWhiteSpace(valor) ? null : DpapiProtector.Descifrar(valor);
    }
}
