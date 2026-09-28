namespace MiniMarket.Desktop.UI;

/// <summary>Elemento de ComboBox: valor tipado + texto visible.</summary>
[System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)] // enlazado a grillas por nombre de propiedad
public sealed record Opcion<T>(T Valor, string Texto)
{
    public override string ToString() => Texto;
}

/// <summary>Fábrica de controles con la configuración estándar de la app.</summary>
public static class Controles
{
    public static NumericUpDown Monto(decimal valor = 0, decimal minimo = 0) =>
        Numero(valor, 2, minimo, 99_999_999);

    public static NumericUpDown Numero(decimal valor, int decimales = 0, decimal minimo = 0, decimal maximo = 1_000_000)
    {
        var n = new NumericUpDown
        {
            DecimalPlaces = decimales,
            Minimum = minimo,
            Maximum = maximo,
            ThousandsSeparator = true,
            TextAlign = HorizontalAlignment.Right,
            Increment = decimales > 0 ? 1 : 1
        };
        n.Value = Math.Clamp(valor, minimo, maximo);
        // Seleccionar todo al entrar: permite escribir el importe directamente.
        n.Enter += (_, _) => n.Select(0, n.Text.Length);
        return n;
    }

    public static ComboBox Combo<T>(IEnumerable<T> items, Func<T, string> texto)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var item in items) combo.Items.Add(new Opcion<T>(item, texto(item)));
        return combo;
    }

    /// <summary>Combo con búsqueda por texto (autocompletado), para listas largas como clientes o productos.</summary>
    public static ComboBox ComboBuscable<T>(IEnumerable<T> items, Func<T, string> texto)
    {
        var combo = Combo(items, texto);
        combo.DropDownStyle = ComboBoxStyle.DropDown;
        combo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        combo.AutoCompleteSource = AutoCompleteSource.ListItems;
        return combo;
    }

    public static T? Seleccion<T>(this ComboBox combo) =>
        combo.SelectedItem is Opcion<T> o ? o.Valor : default;

    public static void Seleccionar<T>(this ComboBox combo, Func<T, bool> predicado)
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is Opcion<T> o && predicado(o.Valor))
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        combo.SelectedIndex = -1;
    }

    public static string? TextoONull(this TextBox txt) =>
        string.IsNullOrWhiteSpace(txt.Text) ? null : txt.Text.Trim();

    public static FlowLayoutPanel Barra(params Control[] controles)
    {
        var barra = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Padding = new Padding(4),
            BackColor = Theme.Panel
        };
        barra.Controls.AddRange(controles);
        return barra;
    }

    public static Label Etiqueta(string texto, bool negrita = false) => new()
    {
        Text = texto,
        AutoSize = true,
        Margin = new Padding(6, 9, 2, 4),
        Font = negrita ? Theme.FuenteNegrita : Theme.Fuente
    };
}
