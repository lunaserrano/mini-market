namespace MiniMarket.Desktop.UI;

/// <summary>
/// Aviso emergente no modal en la esquina inferior derecha de la ventana dueña (equivalente al toast
/// del frontend web). No roba el foco (el cajero puede seguir escaneando) y se cierra solo o con un clic.
/// </summary>
public sealed class Toast : Form
{
    private readonly System.Windows.Forms.Timer _cierre;

    private Toast(string titulo, string mensaje, int milisegundos, Action? alHacerClic)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        BackColor = Theme.Advertencia;
        Padding = new Padding(4, 1, 1, 1); // franja lateral y borde con el color de advertencia
        AutoScaleMode = AutoScaleMode.Dpi;
        Size = new Size(380, 96);
        Cursor = Cursors.Hand;

        var contenido = new Panel { Dock = DockStyle.Fill, BackColor = Theme.FilaAviso, Padding = new Padding(10, 8, 10, 8) };
        var lblMensaje = new Label { Text = mensaje, Dock = DockStyle.Fill, Font = Theme.Fuente, AutoEllipsis = true };
        var lblTitulo = new Label { Text = "⚠ " + titulo, Dock = DockStyle.Top, Height = 24, Font = Theme.FuenteNegrita, ForeColor = Theme.Advertencia };
        contenido.Controls.Add(lblMensaje);
        contenido.Controls.Add(lblTitulo);
        Controls.Add(contenido);

        foreach (var c in new Control[] { this, contenido, lblMensaje, lblTitulo })
            c.Click += (_, _) =>
            {
                Close();
                alHacerClic?.Invoke();
            };

        _cierre = new System.Windows.Forms.Timer { Interval = milisegundos };
        _cierre.Tick += (_, _) => Close();
    }

    protected override bool ShowWithoutActivation => true;

    /// <summary>Muestra el aviso sobre <paramref name="dueño"/>; reemplaza al anterior si seguía abierto.</summary>
    public static void Mostrar(Form dueño, string titulo, string mensaje, Action? alHacerClic = null, int milisegundos = 6000)
    {
        foreach (var previo in dueño.OwnedForms.OfType<Toast>()) previo.Close();

        var toast = new Toast(titulo, mensaje, milisegundos, alHacerClic) { Owner = dueño };
        var area = dueño.RectangleToScreen(dueño.ClientRectangle);
        toast.Location = new Point(area.Right - toast.Width - 16, area.Bottom - toast.Height - 40);
        toast.Show(dueño);
        toast._cierre.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _cierre.Dispose();
        base.OnFormClosed(e); // un Form no modal se libera solo al cerrarse
    }
}
