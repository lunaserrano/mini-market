namespace MiniMarket.Desktop.UI;

/// <summary>Mensajes uniformes de la aplicación.</summary>
public static class Dialogs
{
    private const string App = "MiniMarket";

    public static void Info(IWin32Window? owner, string mensaje, string titulo = App) =>
        MessageBox.Show(owner, mensaje, titulo, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static void Aviso(IWin32Window? owner, string mensaje, string titulo = App) =>
        MessageBox.Show(owner, mensaje, titulo, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static void Error(IWin32Window? owner, string mensaje, string titulo = App) =>
        MessageBox.Show(owner, mensaje, titulo, MessageBoxButtons.OK, MessageBoxIcon.Error);

    public static bool Confirmar(IWin32Window? owner, string mensaje, string titulo = App) =>
        MessageBox.Show(owner, mensaje, titulo, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    public static void Excepcion(IWin32Window? owner, Exception ex)
    {
        switch (ex)
        {
            case ApiException api when api.SinConexion:
                Error(owner, api.Message, "Servicio local no disponible");
                break;
            case ApiException api:
                Aviso(owner, api.MensajeCompleto);
                break;
            default:
                Error(owner, "Ocurrió un error inesperado:\n" + ex.Message);
                break;
        }
    }

    /// <summary>Pide un texto. Devuelve null si se cancela.</summary>
    public static string? PedirTexto(IWin32Window? owner, string titulo, string etiqueta, string valorInicial = "", bool multilinea = false, bool obligatorio = true)
    {
        using var dlg = new EditDialog(titulo);
        var txt = dlg.AgregarTexto(etiqueta, valorInicial, multilinea: multilinea);
        if (obligatorio) dlg.Validar(() => string.IsNullOrWhiteSpace(txt.Text) ? $"{etiqueta} es obligatorio." : null);
        return dlg.ShowDialog(owner) == DialogResult.OK ? txt.Text.Trim() : null;
    }

    /// <summary>Pide un importe. Devuelve null si se cancela.</summary>
    public static decimal? PedirMonto(IWin32Window? owner, string titulo, string etiqueta, decimal valorInicial = 0, decimal minimo = 0)
    {
        using var dlg = new EditDialog(titulo);
        var num = dlg.AgregarMonto(etiqueta, valorInicial);
        dlg.Validar(() => num.Value < minimo ? $"{etiqueta} debe ser mayor o igual a {minimo:N2}." : null);
        return dlg.ShowDialog(owner) == DialogResult.OK ? num.Value : null;
    }
}
