using MiniMarket.Infrastructure.Security;

namespace MiniMarket.Api.Configuration;

/// <summary>
/// Descifra en memoria los valores de configuración con prefijo "ENC:" (ver <see cref="DpapiProtector"/>).
/// Se agrega como ÚLTIMA fuente para que los valores descifrados sobrescriban a los cifrados; el
/// texto plano nunca se escribe a disco. Valores sin prefijo (Azure, Development) pasan sin cambios.
/// </summary>
public static class EncryptedConfigurationExtensions
{
    public static ConfigurationManager AddDecryptedValues(this ConfigurationManager configuration)
    {
        var cifrados = configuration.AsEnumerable()
            .Where(kv => DpapiProtector.EstaCifrado(kv.Value))
            .ToList();
        if (cifrados.Count == 0) return configuration;

        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Los valores 'ENC:' usan DPAPI y solo pueden descifrarse en Windows.");

        var descifrados = new Dictionary<string, string?>();
        foreach (var (clave, valor) in cifrados)
        {
            try
            {
                descifrados[clave] = DpapiProtector.Descifrar(valor!);
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                throw new InvalidOperationException(
                    $"No se pudo descifrar '{clave}'. Los valores ENC: solo son válidos en el equipo donde se generaron; " +
                    "vuelva a ejecutar 'MiniMarket.ConfigTool init-local' en este equipo.", ex);
            }
        }

        configuration.AddInMemoryCollection(descifrados);
        return configuration;
    }
}
