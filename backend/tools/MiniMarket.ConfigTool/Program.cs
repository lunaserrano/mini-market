using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using MiniMarket.Infrastructure.Security;

// Herramienta de instalación del modo Desktop. Genera %ProgramData%\MiniMarket\appsettings.Secrets.json
// con la connection string y la Jwt:Key cifradas con DPAPI (alcance máquina). Es la alternativa por
// script a "Configurar conexión" dentro de MiniMarket.Desktop. Debe ejecutarse EN EL EQUIPO donde se
// usará la app: los valores cifrados no se pueden descifrar en otra PC.
//
//   MiniMarket.ConfigTool init --server ".\SQLEXPRESS" --database MiniMarket --user minimarket_api --password "***"
//   MiniMarket.ConfigTool init --integrated                    (autenticación de Windows)
//   MiniMarket.ConfigTool encrypt "<valor>"                    (cifra un valor suelto -> ENC:...)
//   MiniMarket.ConfigTool verify                               (comprueba que los secretos descifran y conectan)
//   --dir <carpeta> cambia la carpeta del archivo (por defecto %ProgramData%\MiniMarket).

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
    var dir = o.GetValueOrDefault("dir") ?? SecretosLocales.CarpetaPredeterminada;
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
        ApplicationName = "MiniMarket"
    };
    if (integrated)
    {
        csb.IntegratedSecurity = true;
    }
    else
    {
        csb.UserID = o.GetValueOrDefault("user") ?? "minimarket_api";
        // Orden: --password, variable MINIMARKET_SQL_PASSWORD (la usa install.ps1 para no
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

    var ruta = Path.Combine(dir, SecretosLocales.NombreArchivo);
    SecretosLocales.Guardar(ruta, csb.ConnectionString, rotarJwt: o.ContainsKey("rotate-jwt"));
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
    var ruta = Path.Combine(o.GetValueOrDefault("dir") ?? SecretosLocales.CarpetaPredeterminada, SecretosLocales.NombreArchivo);
    if (!File.Exists(ruta))
    {
        Console.Error.WriteLine($"No existe {ruta}. Ejecute 'init'.");
        return 1;
    }

    try
    {
        var cs = SecretosLocales.LeerConnectionString(ruta);
        var key = SecretosLocales.LeerJwtKey(ruta);
        if (cs is null || key is null)
        {
            Console.Error.WriteLine($"{ruta} está incompleto. Ejecute 'init'.");
            return 1;
        }
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

          init    [--server .\SQLEXPRESS] [--database MiniMarket]
                  [--user minimarket_api --password <pwd> | --integrated] [--rotate-jwt] [--force]
          verify
          (--dir <carpeta> en init/verify; por defecto %ProgramData%\MiniMarket)
          encrypt "<valor>"
        """);
    return 1;
}
