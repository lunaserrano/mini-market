using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace MiniMarket.Infrastructure.Security;

/// <summary>
/// Cifra/descifra secretos de configuración (connection string, clave JWT) con DPAPI de Windows en
/// alcance de MÁQUINA: el valor cifrado solo se puede descifrar en el mismo equipo donde se generó
/// (copiar el appsettings a otra PC no expone el secreto). Formato: "ENC:" + Base64.
///
/// Lo usan la Api (EncryptedConfigurationExtensions, al arrancar) y MiniMarket.ConfigTool (al instalar),
/// que enlaza este mismo archivo fuente para no depender de toda la capa Infrastructure.
/// </summary>
[SupportedOSPlatform("windows")]
public static class DpapiProtector
{
    public const string Prefijo = "ENC:";

    // Entropía propia de la app: otra aplicación de la misma máquina que use DPAPI no puede
    // descifrar estos valores sin conocerla. No es un secreto fuerte (vive en el binario, ofuscado),
    // pero sube el costo frente a DPAPI "pelado".
    private static readonly byte[] Entropia = Encoding.UTF8.GetBytes("MiniMarket.Local.v1|7c1e2a9d-4b6f-4f0a-9e53-2d8b1c0f6a41");

    public static bool EstaCifrado(string? valor) =>
        valor is not null && valor.StartsWith(Prefijo, StringComparison.Ordinal);

    public static string Cifrar(string textoPlano)
    {
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(textoPlano), Entropia, DataProtectionScope.LocalMachine);
        return Prefijo + Convert.ToBase64String(bytes);
    }

    public static string Descifrar(string valorCifrado)
    {
        if (!EstaCifrado(valorCifrado)) return valorCifrado;
        var bytes = Convert.FromBase64String(valorCifrado[Prefijo.Length..]);
        return Encoding.UTF8.GetString(ProtectedData.Unprotect(bytes, Entropia, DataProtectionScope.LocalMachine));
    }
}
