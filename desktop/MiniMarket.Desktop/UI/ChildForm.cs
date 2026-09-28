namespace MiniMarket.Desktop.UI;

/// <summary>
/// Pantalla hija del MDI de MainForm. Carga sus datos de forma asíncrona al mostrarse
/// (<see cref="CargarAsync"/>) y puede recargarse con F5.
/// </summary>
public class ChildForm : Form
{
    public ChildForm()
    {
        Theme.Aplicar(this);
        KeyPreview = true;
        WindowState = FormWindowState.Maximized;
        ShowIcon = false;
        KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.F5) return;
            e.Handled = true;
            await RecargarAsync();
        };
    }

    /// <summary>Clave de auditoría/navegación (ej. "pos", "productos"); se registra como NAVEGACION_UI.</summary>
    public virtual string Ruta => GetType().Name.Replace("Form", "").ToLowerInvariant();

    protected virtual Task CargarAsync() => Task.CompletedTask;

    public Task<bool> RecargarAsync() => this.EjecutarAsync(CargarAsync);

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await RecargarAsync();
    }
}
