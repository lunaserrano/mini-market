using System.Text.Json;
using System.Text.Json.Nodes;

namespace MiniMarket.Application.Services;

/// <summary>Prepara los datos que se guardan en la auditoría: oculta secretos y acota el tamaño.</summary>
public static class AuditoriaDatos
{
    public const string Oculto = "***";
    public const int MaxCaracteres = 16_000;
    private const int MaxValorTexto = 300;

    // Cualquier propiedad cuyo nombre contenga uno de estos fragmentos se guarda como "***".
    private static readonly string[] FragmentosSecretos = ["password", "contrasena", "contraseña", "token", "secret", "clave", "authorization"];

    /// <summary>
    /// Devuelve el JSON con los secretos ocultos, o null si <paramref name="json"/> está vacío. Si no es JSON válido no se guarda nada:
    /// no se puede garantizar que no contenga secretos.
    /// </summary>
    public static string? SanitizarJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        JsonNode? nodo;
        try { nodo = JsonNode.Parse(json); }
        catch (JsonException) { return null; }
        if (nodo is null) return null;

        Ocultar(nodo);
        return Acotar(nodo.ToJsonString());
    }

    /// <summary>Serializa el descriptor de un clic. Ignora claves de secretos, acota valores y descarta el resto si son demasiadas.</summary>
    public static string? SerializarDescriptor(IDictionary<string, string?>? datos)
    {
        if (datos is null || datos.Count == 0) return null;
        var nodo = new JsonObject();
        foreach (var (clave, valor) in datos.Take(15))
        {
            if (string.IsNullOrWhiteSpace(clave)) continue;
            var k = Truncar(clave.Trim(), 40)!;
            nodo[k] = EsSecreto(k) ? Oculto : Truncar(valor, MaxValorTexto);
        }
        return nodo.Count == 0 ? null : nodo.ToJsonString();
    }

    public static string? Truncar(string? valor, int max) =>
        valor is null || valor.Length <= max ? valor : valor[..max];

    private static bool EsSecreto(string nombre) =>
        FragmentosSecretos.Any(f => nombre.Contains(f, StringComparison.OrdinalIgnoreCase));

    private static void Ocultar(JsonNode nodo)
    {
        switch (nodo)
        {
            case JsonObject objeto:
                foreach (var clave in objeto.Select(p => p.Key).ToList())
                {
                    if (EsSecreto(clave)) objeto[clave] = Oculto;
                    else if (objeto[clave] is { } hijo) Ocultar(hijo);
                }
                break;
            case JsonArray arreglo:
                foreach (var hijo in arreglo)
                    if (hijo is not null) Ocultar(hijo);
                break;
        }
    }

    private static string Acotar(string json) =>
        json.Length <= MaxCaracteres ? json : json[..MaxCaracteres] + "…[truncado]";
}
