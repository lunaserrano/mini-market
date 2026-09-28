using MiniMarket.Desktop.Forms.Configuracion;

namespace MiniMarket.Desktop.Forms.Auth;

public sealed class LoginForm : Form
{
    private readonly AuthApi _auth;
    private readonly SessionService _sesion;
    private readonly ApiHealthMonitor _salud;
    private readonly IServiceProvider _services;

    private readonly TextBox _usuario = new() { Width = 280 };
    private readonly TextBox _password = new() { Width = 280, UseSystemPasswordChar = true };
    private readonly CheckBox _recordar = new() { Text = "Recordar sesión en este equipo", AutoSize = true };
    private readonly Label _estado = new() { AutoSize = true, ForeColor = Theme.TextoSuave };
    private readonly Button _ingresar;

    public LoginForm(AuthApi auth, SessionService sesion, ApiHealthMonitor salud, IServiceProvider services)
    {
        _auth = auth;
        _sesion = sesion;
        _salud = salud;
        _services = services;

        Theme.Aplicar(this);
        Text = "MiniMarket — Iniciar sesión";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(24);

        _ingresar = Theme.Boton("Ingresar", async (_, _) => await IngresarAsync(), primario: true);
        _ingresar.Width = 280;
        _ingresar.AutoSize = false;
        _ingresar.Height = 38;
        var conexion = new LinkLabel { Text = "Configurar conexión...", AutoSize = true, Margin = new Padding(3, 12, 3, 3) };
        conexion.LinkClicked += (_, _) =>
        {
            using var dlg = ActivatorUtilities.CreateInstance<ConexionForm>(_services);
            dlg.ShowDialog(this);
        };

        var panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false,
            Location = new Point(24, 24)
        };
        panel.Controls.AddRange(new Control[]
        {
            new Label { Text = "MiniMarket POS", Font = new Font("Segoe UI Semibold", 20F), AutoSize = true, ForeColor = Theme.Primario },
            new Label { Text = "Sistema local (sin internet)", AutoSize = true, ForeColor = Theme.TextoSuave, Margin = new Padding(3, 0, 3, 16) },
            new Label { Text = "Usuario", AutoSize = true }, _usuario,
            new Label { Text = "Contraseña", AutoSize = true, Margin = new Padding(3, 10, 3, 3) }, _password,
            _recordar,
            _ingresar,
            _estado,
            conexion
        });
        Controls.Add(panel);
        AcceptButton = _ingresar;
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _usuario.Focus();
        _estado.Text = "Verificando servicio local...";
        var estado = await _salud.VerificarAsync();
        (_estado.Text, _estado.ForeColor) = estado switch
        {
            EstadoServicio.EnLinea => ("● Servicio local en línea", Theme.Exito),
            EstadoServicio.SinBaseDatos => ("● El servicio responde, pero SQL Server no está disponible", Theme.Advertencia),
            _ => ("● Servicio local detenido: inicie 'MiniMarketApi' en services.msc", Theme.Peligro)
        };
    }

    private async Task IngresarAsync()
    {
        if (string.IsNullOrWhiteSpace(_usuario.Text) || _password.Text.Length == 0)
        {
            Dialogs.Aviso(this, "Ingrese usuario y contraseña.");
            return;
        }

        _ingresar.Enabled = false;
        var ok = await this.EjecutarAsync(async () =>
        {
            var login = await _auth.LoginAsync(_usuario.Text.Trim(), _password.Text);
            _sesion.Iniciar(login, _recordar.Checked);
        });
        _ingresar.Enabled = true;

        if (ok)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            _password.Clear();
            _password.Focus();
        }
    }
}
