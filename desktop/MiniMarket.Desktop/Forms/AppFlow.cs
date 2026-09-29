using MiniMarket.Desktop.Forms.Auth;
using MiniMarket.Desktop.Forms.Configuracion;

namespace MiniMarket.Desktop.Forms;

/// <summary>
/// Ciclo de vida de la app: conexión con SQL Server (solo la primera vez) -> arranque de la Api
/// embebida -> login (o sesión recordada) -> cambio de contraseña obligatorio si
/// corresponde -> ventana principal. Al cerrar sesión (o si la sesión expira) vuelve al login.
/// </summary>
public sealed class AppFlow : ApplicationContext
{
    private readonly IServiceProvider _services;
    private readonly SessionService _sesion;

    public AppFlow(IServiceProvider services)
    {
        _services = services;
        _sesion = services.GetRequiredService<SessionService>();
        System.Windows.Forms.Application.Idle += Iniciar;
    }

    private async void Iniciar(object? sender, EventArgs e)
    {
        System.Windows.Forms.Application.Idle -= Iniciar;
        if (!await IniciarApiAsync())
        {
            ExitThread();
            return;
        }
        await CicloAsync(intentarSesionRecordada: true);
    }

    /// <summary>
    /// Arranca la Api dentro del proceso. Si falta la conexión la pide; si SQL Server no responde
    /// ofrece reintentar o corregir la conexión. False = el usuario decidió salir.
    /// </summary>
    private async Task<bool> IniciarApiAsync()
    {
        var api = _services.GetRequiredService<ApiLocal>();
        while (true)
        {
            if (!ApiLocal.Configurada && !PedirConexion())
                return false;

            Exception error;
            using (var espera = PantallaInicio())
            {
                espera.Show();
                try
                {
                    // Fuera del hilo de UI: las migraciones y el arranque de Kestrel son síncronos en parte.
                    await Task.Run(() => api.IniciarAsync());
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            }

            var reintentar = new TaskDialogButton("Reintentar");
            var configurar = new TaskDialogButton("Configurar conexión...");
            var salir = new TaskDialogButton("Salir");
            var opcion = TaskDialog.ShowDialog(new TaskDialogPage
            {
                Caption = "MiniMarket",
                Heading = "No se pudo iniciar MiniMarket",
                Text = "Verifique que SQL Server esté iniciado y que los datos de conexión sean correctos.\n\n" + error.Message,
                Icon = TaskDialogIcon.Error,
                Buttons = { reintentar, configurar, salir },
                Expander = new TaskDialogExpander(error.ToString()) { CollapsedButtonText = "Detalle técnico" }
            });
            if (opcion == salir) return false;
            if (opcion == configurar && !PedirConexion()) return false;
        }
    }

    private static bool PedirConexion()
    {
        using var dlg = new ConexionForm(reiniciarAlGuardar: false);
        return dlg.ShowDialog() == DialogResult.OK;
    }

    private static Form PantallaInicio()
    {
        var form = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.CenterScreen,
            ShowInTaskbar = true,
            Text = "MiniMarket",
            Size = new Size(380, 130),
            BackColor = Theme.Panel
        };
        form.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = Theme.FuenteTitulo,
            ForeColor = Theme.Primario,
            Text = "MiniMarket POS\nIniciando..."
        });
        form.UseWaitCursor = true;
        return form;
    }

    private async Task CicloAsync(bool intentarSesionRecordada)
    {
        while (true)
        {
            var autenticado = intentarSesionRecordada && await RestaurarSesionAsync();
            intentarSesionRecordada = false;

            if (!autenticado)
            {
                using var login = ActivatorUtilities.CreateInstance<LoginForm>(_services);
                if (login.ShowDialog() != DialogResult.OK)
                {
                    ExitThread();
                    return;
                }
            }

            if (_sesion.Usuario!.DebeCambiarPassword)
            {
                using var cambio = ActivatorUtilities.CreateInstance<CambiarPasswordForm>(_services, true);
                if (cambio.ShowDialog() != DialogResult.OK)
                {
                    await CerrarSesionAsync();
                    continue;
                }
            }

            if (!await CargarEmpresaAsync()) continue;

            using var principal = ActivatorUtilities.CreateInstance<MainForm>(_services);
            principal.ShowDialog();
            if (!principal.CerrarSesionSolicitado)
            {
                ExitThread();
                return;
            }
            await CerrarSesionAsync();
        }
    }

    private async Task<bool> RestaurarSesionAsync()
    {
        var refresh = _services.GetRequiredService<TokenStore>().Leer();
        if (refresh is null) return false;
        try
        {
            var login = await _services.GetRequiredService<AuthApi>().RefreshAsync(refresh);
            _sesion.Iniciar(login, recordar: true);
            return true;
        }
        catch (ApiException)
        {
            _sesion.Cerrar();
            return false;
        }
    }

    private async Task<bool> CargarEmpresaAsync()
    {
        try
        {
            _sesion.Empresa = await _services.GetRequiredService<EmpresaApi>().ObtenerActualAsync();
            Formatters.Configurar(_sesion.Empresa);
            return true;
        }
        catch (ApiException ex)
        {
            Dialogs.Excepcion(null, ex);
            await CerrarSesionAsync();
            return false;
        }
    }

    private async Task CerrarSesionAsync()
    {
        try
        {
            if (_sesion.RefreshToken is not null)
                await _services.GetRequiredService<AuthApi>().LogoutAsync(_sesion.RefreshToken);
        }
        catch (ApiException)
        {
            // Sin conexión: la sesión se cierra localmente igual; el refresh token vence solo.
        }
        _sesion.Cerrar();
    }
}
