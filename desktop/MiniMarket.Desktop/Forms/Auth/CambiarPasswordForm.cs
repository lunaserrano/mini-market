namespace MiniMarket.Desktop.Forms.Auth;

/// <summary>
/// Cambio de contraseña propia. Obligatorio tras el primer ingreso o un reset (DebeCambiarPassword).
/// La Api valida la política de contraseñas, revoca las demás sesiones y devuelve tokens nuevos.
/// </summary>
public sealed class CambiarPasswordForm : EditDialog
{
    public CambiarPasswordForm(AuthApi auth, SessionService sesion, bool obligatorio)
        : base(obligatorio ? "Debe cambiar su contraseña" : "Cambiar contraseña")
    {
        if (obligatorio)
            AgregarAncho(new Label
            {
                Text = "Por seguridad, defina una contraseña nueva antes de continuar.",
                AutoSize = true,
                ForeColor = Theme.Advertencia
            });

        var actual = AgregarTexto("Contraseña actual", password: true);
        var nueva = AgregarTexto("Contraseña nueva", password: true);
        var confirmar = AgregarTexto("Confirmar", password: true);

        Validar(() => actual.Text.Length == 0 || nueva.Text.Length == 0 ? "Complete todos los campos." : null);
        Validar(() => nueva.Text != confirmar.Text ? "La confirmación no coincide con la contraseña nueva." : null);
        Validar(() => nueva.Text == actual.Text ? "La contraseña nueva debe ser distinta de la actual." : null);

        AlGuardar(async () =>
        {
            var login = await auth.CambiarPasswordAsync(actual.Text, nueva.Text);
            sesion.ActualizarTokens(login);
            Dialogs.Info(this, "Contraseña actualizada. Las demás sesiones abiertas se cerraron.");
        });
    }
}
