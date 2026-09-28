namespace MiniMarket.Desktop.UI;

/// <summary>Paleta y tipografía comunes. Se aplica por código (no hay archivos .Designer).</summary>
public static class Theme
{
    public static readonly Font Fuente = new("Segoe UI", 10F);
    public static readonly Font FuenteNegrita = new("Segoe UI", 10F, FontStyle.Bold);
    public static readonly Font FuenteTitulo = new("Segoe UI Semibold", 14F);
    public static readonly Font FuenteTotal = new("Segoe UI", 22F, FontStyle.Bold);
    public static readonly Font FuenteTicket = new("Consolas", 8.5F);

    public static readonly Color Primario = Color.FromArgb(37, 99, 235);
    public static readonly Color PrimarioTexto = Color.White;
    public static readonly Color Peligro = Color.FromArgb(220, 38, 38);
    public static readonly Color Exito = Color.FromArgb(22, 163, 74);
    public static readonly Color Advertencia = Color.FromArgb(217, 119, 6);
    public static readonly Color Fondo = Color.FromArgb(248, 250, 252);
    public static readonly Color Panel = Color.White;
    public static readonly Color Borde = Color.FromArgb(226, 232, 240);
    public static readonly Color TextoSuave = Color.FromArgb(100, 116, 139);
    public static readonly Color FilaAlerta = Color.FromArgb(254, 226, 226);
    public static readonly Color FilaAviso = Color.FromArgb(254, 243, 199);
    public static readonly Color FilaInactiva = Color.FromArgb(241, 245, 249);

    public static void Aplicar(Form form)
    {
        form.Font = Fuente;
        form.BackColor = Fondo;
        form.AutoScaleMode = AutoScaleMode.Dpi;
    }

    public static Button Boton(string texto, EventHandler? click = null, bool primario = false, bool peligro = false)
    {
        var b = new Button
        {
            Text = texto,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(4),
            FlatStyle = FlatStyle.Flat,
            BackColor = primario ? Primario : peligro ? Peligro : Panel,
            ForeColor = primario || peligro ? PrimarioTexto : Color.Black,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        b.FlatAppearance.BorderColor = primario ? Primario : peligro ? Peligro : Borde;
        if (click is not null) b.Click += click;
        return b;
    }

    public static Label Titulo(string texto) => new()
    {
        Text = texto,
        Font = FuenteTitulo,
        AutoSize = true,
        Margin = new Padding(4, 4, 4, 8)
    };
}
