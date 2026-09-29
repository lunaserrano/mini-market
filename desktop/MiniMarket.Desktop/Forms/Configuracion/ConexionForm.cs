using Microsoft.Data.SqlClient;
using MiniMarket.Infrastructure.Security;

namespace MiniMarket.Desktop.Forms.Configuracion;

/// <summary>
/// Conexión con SQL Server, lo único externo que necesita la app. Se guarda cifrada con DPAPI (alcance
/// máquina) en %ProgramData%\MiniMarket\appsettings.Secrets.json, compartida por todos los usuarios de
/// Windows del equipo. Se muestra sola en el primer arranque; después, desde el login o Sistema > Conexión.
/// En una red con varias cajas, cada PC apunta al mismo SQL Server (ej. CAJA1\SQLEXPRESS).
/// </summary>
public sealed class ConexionForm : EditDialog
{
    private const string UsuarioPredeterminado = "minimarket_api";

    private readonly TextBox _servidor;
    private readonly TextBox _baseDatos;
    private readonly CheckBox _windows;
    private readonly TextBox _usuario;
    private readonly TextBox _password;
    private readonly Label _resultado;
    private readonly SqlConnectionStringBuilder? _actual;

    /// <param name="reiniciarAlGuardar">True si la Api ya está corriendo: la nueva conexión exige reiniciar la app.</param>
    public ConexionForm(bool reiniciarAlGuardar) : base("Conexión con la base de datos", 560)
    {
        _actual = LeerActual();
        if (!reiniciarAlGuardar) StartPosition = FormStartPosition.CenterScreen;

        _servidor = AgregarTexto("Servidor SQL", _actual?.DataSource ?? @".\SQLEXPRESS");
        _baseDatos = AgregarTexto("Base de datos", _actual?.InitialCatalog ?? "MiniMarket");
        _windows = AgregarCheck("Autenticación de Windows", _actual?.IntegratedSecurity ?? false);
        _usuario = AgregarTexto("Usuario SQL", string.IsNullOrEmpty(_actual?.UserID) ? UsuarioPredeterminado : _actual.UserID);
        _password = AgregarTexto("Contraseña", "", password: true);
        if (_actual is { IntegratedSecurity: false })
            AgregarAncho(new Label { AutoSize = true, ForeColor = Theme.TextoSuave, Text = "Deje la contraseña vacía para conservar la actual." });

        _resultado = AgregarAncho(new Label { AutoSize = true, ForeColor = Theme.TextoSuave, Text = "Pruebe la conexión antes de guardar." });
        var probar = Theme.Boton("Probar conexión");
        AgregarAncho(probar).Anchor = AnchorStyles.Left;

        _windows.CheckedChanged += (_, _) => ActualizarCredenciales();
        ActualizarCredenciales();

        probar.Click += async (_, _) =>
        {
            probar.Enabled = false;
            _resultado.Text = "Probando...";
            _resultado.ForeColor = Theme.TextoSuave;
            var error = await ProbarAsync(Construir());
            (_resultado.Text, _resultado.ForeColor) = error is null
                ? ("✔ Conexión correcta.", Theme.Exito)
                : ("✖ " + error, Theme.Peligro);
            probar.Enabled = true;
        };

        Validar(() => string.IsNullOrWhiteSpace(_servidor.Text) ? "Indique el servidor SQL (ej. .\\SQLEXPRESS)." : null);
        Validar(() => string.IsNullOrWhiteSpace(_baseDatos.Text) ? "Indique el nombre de la base de datos." : null);
        Validar(() => !_windows.Checked && string.IsNullOrWhiteSpace(_usuario.Text) ? "Indique el usuario SQL." : null);
        Validar(() => !_windows.Checked && _password.Text.Length == 0 && !PuedeConservarPassword()
            ? "Indique la contraseña del usuario SQL." : null);

        AlGuardar(async () =>
        {
            var csb = Construir();
            var error = await ProbarAsync(csb);
            if (error is not null && !Dialogs.Confirmar(this, $"No se pudo conectar:\n{error}\n\n¿Guardar la configuración de todas formas?"))
                throw new OperationCanceledException();

            try
            {
                SecretosLocales.Guardar(SecretosLocales.RutaPredeterminada, csb.ConnectionString);
            }
            catch (UnauthorizedAccessException)
            {
                throw new InvalidOperationException(
                    $"Este usuario de Windows no puede escribir en {SecretosLocales.CarpetaPredeterminada}.\n" +
                    "Vuelva a ejecutar el instalador o abra MiniMarket como administrador una vez para configurar la conexión.");
            }

            if (reiniciarAlGuardar &&
                Dialogs.Confirmar(this, "Configuración guardada. Es necesario reiniciar la aplicación.\n¿Reiniciar ahora?"))
                System.Windows.Forms.Application.Restart();
        });
    }

    private void ActualizarCredenciales()
    {
        _usuario.Enabled = !_windows.Checked;
        _password.Enabled = !_windows.Checked;
    }

    private bool PuedeConservarPassword() =>
        _actual is { IntegratedSecurity: false } &&
        !string.IsNullOrEmpty(_actual.Password) &&
        string.Equals(_actual.UserID, _usuario.Text.Trim(), StringComparison.OrdinalIgnoreCase);

    private SqlConnectionStringBuilder Construir()
    {
        var csb = new SqlConnectionStringBuilder
        {
            DataSource = _servidor.Text.Trim(),
            InitialCatalog = _baseDatos.Text.Trim(),
            Encrypt = true,
            // SQL Server Express local usa un certificado autofirmado.
            TrustServerCertificate = true,
            ConnectTimeout = 30,
            ApplicationName = "MiniMarket"
        };
        if (_windows.Checked)
        {
            csb.IntegratedSecurity = true;
        }
        else
        {
            csb.UserID = _usuario.Text.Trim();
            csb.Password = _password.Text.Length == 0 && PuedeConservarPassword() ? _actual!.Password : _password.Text;
        }
        return csb;
    }

    private static SqlConnectionStringBuilder? LeerActual()
    {
        try
        {
            var cs = SecretosLocales.LeerConnectionString(SecretosLocales.RutaPredeterminada);
            return cs is null ? null : new SqlConnectionStringBuilder(cs);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
                                       or System.Text.Json.JsonException or System.Security.Cryptography.CryptographicException)
        {
            // Ilegible o de otra PC: se configura de cero.
            return null;
        }
    }

    /// <summary>Null si conecta; si no, el motivo.</summary>
    private static async Task<string?> ProbarAsync(SqlConnectionStringBuilder csb)
    {
        var prueba = new SqlConnectionStringBuilder(csb.ConnectionString) { ConnectTimeout = 8 };
        try
        {
            await using var conexion = new SqlConnection(prueba.ConnectionString);
            await conexion.OpenAsync();
            return null;
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException or ArgumentException)
        {
            return ex.Message;
        }
    }
}
