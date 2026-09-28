using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using MiniMarket.Infrastructure.Security;

// Herramienta de instalación del modo Desktop. Genera appsettings.Secrets.json junto a la Api con la
// connection string y la Jwt:Key cifradas con DPAPI (alcance máquina). Debe ejecutarse EN EL EQUIPO
// donde corre el servicio MiniMarketApi: los valores cifrados no se pueden descifrar en otra PC.
//
//   MiniMarket.ConfigTool init --dir "C:\Program Files\MiniMarket\Api" --server ".\SQLEXPRESS"
//                              --database MiniMarket --user minimarket_api --password "***"
//   MiniMarket.ConfigTool init --dir ... --integrated          (autenticación de Windows)
//   MiniMarket.ConfigTool encrypt "<valor>"                    (cifra un valor suelto -> ENC:...)
//   MiniMarket.ConfigTool verify --dir ...                     (comprueba que los secretos descifran y conectan)

Console.OutputEncoding = System.Text.Encoding.UTF8;

return args.FirstOrDefault()?.ToLowerInvariant() switch
{
    "init" => Init(Opciones(args)),
    "encrypt" when args.Length >= 2 => Encrypt(args[1]),
    "verify" => Verify(Opciones(args)),
    _ => Ayuda()
};

static int Init(Dictionary<string, string?> o)
{
    var dir = o.GetValueOrDefault("dir") ?? AppContext.BaseDirectory;
    var server = o.GetValueOrDefault("server") ?? @".\SQLEXPRESS";
    var database = o.GetValueOrDefault("database") ?? "MiniMarket";
    var integrated = o.ContainsKey("integrated");

    var csb = new SqlConnectionStringBuilder
    {
        DataSource = server,
        InitialCatalog = database,
        Encrypt = true,
        // SQL Server Express local usa un certificado autofirmado.
        TrustServerCertificate = true,
        ConnectTimeout = 30,
        ApplicationName = "MiniMarket.Api"
    };
    if (integrated)
    {
        csb.IntegratedSecurity = true;
    }
    else
    {
        csb.UserID = o.GetValueOrDefault("user") ?? "minimarket_api";
        // Orden: --password, variable MINIMARKET_SQL_PASSWORD (la usa install-api-service.ps1 para no
        // exponer la clave en la lista de procesos) o se pide por consola.
        csb.Password = o.GetValueOrDefault("password")
            ?? Environment.GetEnvironmentVariable("MINIMARKET_SQL_PASSWORD")
            ?? LeerPassword($"Contraseña SQL de '{csb.UserID}': ");
    }

    Console.WriteLine($"Probando conexión a {server} / {database}...");
    if (!ProbarConexion(csb.ConnectionString, out var error))
    {
        Console.Error.WriteLine($"No se pudo conectar: {error}");
        if (!o.ContainsKey("force")) return 2;
        Console.Error.WriteLine("--force: se guarda la configuración de todas formas.");
    }

    var ruta = Path.Combine(dir, "appsettings.Secrets.json");
    var existente = File.Exists(ruta) ? JsonNode.Parse(File.ReadAllText(ruta)) as JsonObject : null;

    // Se conserva la Jwt:Key existente (salvo --rotate-jwt): regenerarla invalida las sesiones abiertas.
    var jwtKeyCifrada = existente?["Jwt"]?["Key"]?.GetValue<string>();
    if (jwtKeyCifrada is null || o.ContainsKey("rotate-jwt"))
        jwtKeyCifrada = DpapiProtector.Cifrar(Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)));

    var json = new JsonObject
    {
        ["ConnectionStrings"] = new JsonObject { ["DefaultConnection"] = DpapiProtector.Cifrar(csb.ConnectionString) },
        ["Jwt"] = new JsonObject { ["Key"] = jwtKeyCifrada }
    };

    Directory.CreateDirectory(dir);
    File.WriteAllText(ruta, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Secretos cifrados escritos en {ruta}");
    return 0;
}

static int Encrypt(string valor)
{
    Console.WriteLine(DpapiProtector.Cifrar(valor));
    return 0;
}

static int Verify(Dictionary<string, string?> o)
{
    var ruta = Path.Combine(o.GetValueOrDefault("dir") ?? AppContext.BaseDirectory, "appsettings.Secrets.json");
    if (!File.Exists(ruta))
    {
        Console.Error.WriteLine($"No existe {ruta}. Ejecute 'init'.");
        return 1;
    }

    try
    {
        var json = JsonNode.Parse(File.ReadAllText(ruta))!;
        var cs = DpapiProtector.Descifrar(json["ConnectionStrings"]!["DefaultConnection"]!.GetValue<string>());
        var key = DpapiProtector.Descifrar(json["Jwt"]!["Key"]!.GetValue<string>());
        Console.WriteLine($"Jwt:Key descifrada correctamente ({key.Length} caracteres).");
        if (!ProbarConexion(cs, out var error))
        {
            Console.Error.WriteLine($"La connection string descifra, pero no conecta: {error}");
            return 2;
        }
        Console.WriteLine("Conexión a la base de datos OK.");
        return 0;
    }
    catch (CryptographicException)
    {
        Console.Error.WriteLine("Los secretos no se pueden descifrar en este equipo (¿se copiaron desde otra PC?). Ejecute 'init'.");
        return 3;
    }
}

static bool ProbarConexion(string connectionString, out string? error)
{
    try
    {
        using var conexion = new SqlConnection(connectionString);
        conexion.Open();
        error = null;
        return true;
    }
    catch (SqlException ex)
    {
        error = ex.Message;
        return false;
    }
}

static string LeerPassword(string mensaje)
{
    Console.Write(mensaje);
    var chars = new List<char>();
    ConsoleKeyInfo tecla;
    while ((tecla = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
    {
        if (tecla.Key == ConsoleKey.Backspace) { if (chars.Count > 0) chars.RemoveAt(chars.Count - 1); }
        else if (!char.IsControl(tecla.KeyChar)) chars.Add(tecla.KeyChar);
    }
    Console.WriteLine();
    return new string(chars.ToArray());
}

static Dictionary<string, string?> Opciones(string[] args)
{
    var o = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    for (var i = 1; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--")) continue;
        var clave = args[i][2..];
        var tieneValor = i + 1 < args.Length && !args[i + 1].StartsWith("--");
        o[clave] = tieneValor ? args[++i] : null;
    }
    return o;
}

static int Ayuda()
{
    Console.WriteLine("""
        MiniMarket.ConfigTool — secretos del modo Desktop (DPAPI, alcance máquina)

          init    --dir <carpeta Api> [--server .\SQLEXPRESS] [--database MiniMarket]
                  [--user minimarket_api --password <pwd> | --integrated] [--rotate-jwt] [--force]
          verify  --dir <carpeta Api>
          encrypt "<valor>"
        """);
    return 1;
}
