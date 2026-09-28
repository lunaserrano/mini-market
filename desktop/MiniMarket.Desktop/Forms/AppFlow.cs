using MiniMarket.Desktop.Forms.Auth;

namespace MiniMarket.Desktop.Forms;

/// <summary>
/// Ciclo de vida de la app: login (o sesión recordada) -> cambio de contraseña obligatorio si
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
        Application.Idle += Iniciar;
    }

    private async void Iniciar(object? sender, EventArgs e)
    {
        Application.Idle -= Iniciar;
        await CicloAsync(intentarSesionRecordada: true);
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
