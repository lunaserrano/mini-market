namespace MiniMarket.Desktop.UI;

/// <summary>Tipo de formato especial de una columna (se resuelve en CellFormatting).</summary>
public enum FormatoColumna { Normal, Moneda, Cantidad, FechaUtc, SoloFechaUtc, Estado, MetodoPago, SiNo }

/// <summary>Configuración estándar de DataGridView: solo lectura, fila completa, columnas declaradas a mano.</summary>
public static class GridExtensions
{
    public static DataGridView Estandar(this DataGridView grid, bool soloLectura = true)
    {
        grid.Dock = DockStyle.Fill;
        grid.AutoGenerateColumns = false;
        grid.ReadOnly = soloLectura;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.RowHeadersVisible = false;
        grid.BackgroundColor = Theme.Panel;
        grid.BorderStyle = BorderStyle.None;
        grid.GridColor = Theme.Borde;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.Fondo;
        grid.ColumnHeadersDefaultCellStyle.Font = Theme.FuenteNegrita;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
        grid.DefaultCellStyle.SelectionForeColor = Color.Black;
        grid.RowTemplate.Height = 30;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        grid.CellFormatting += FormatearCelda;
        return grid;
    }

    public static DataGridViewColumn Col(this DataGridView grid, string encabezado, string propiedad, int ancho = 120,
        FormatoColumna formato = FormatoColumna.Normal, bool relleno = false, bool editable = false)
    {
        DataGridViewColumn col = formato == FormatoColumna.SiNo
            ? new DataGridViewCheckBoxColumn()
            : new DataGridViewTextBoxColumn();
        col.HeaderText = encabezado;
        col.DataPropertyName = propiedad;
        col.Name = propiedad;
        col.Width = ancho;
        col.ReadOnly = !editable;
        col.Tag = formato;
        col.SortMode = DataGridViewColumnSortMode.NotSortable;
        if (relleno)
        {
            col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            col.MinimumWidth = ancho;
        }
        if (formato is FormatoColumna.Moneda or FormatoColumna.Cantidad)
            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        grid.Columns.Add(col);
        return col;
    }

    public static T? Seleccionado<T>(this DataGridView grid) where T : class =>
        grid.CurrentRow?.DataBoundItem as T;

    /// <summary>Colorea filas según una regla (stock bajo, crédito vencido, inactivos...).</summary>
    public static void ColorearFilas<T>(this DataGridView grid, Func<T, Color?> color) where T : class
    {
        grid.RowPrePaint += (_, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= grid.Rows.Count) return;
            var fila = grid.Rows[e.RowIndex];
            if (fila.DataBoundItem is T item && color(item) is { } c)
                fila.DefaultCellStyle.BackColor = c;
            else
                fila.DefaultCellStyle.BackColor = Theme.Panel;
        };
    }

    private static void FormatearCelda(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (sender is not DataGridView grid || e.ColumnIndex < 0 || e.Value is null) return;
        if (grid.Columns[e.ColumnIndex].Tag is not FormatoColumna formato) return;

        switch (formato)
        {
            case FormatoColumna.Moneda when e.Value is decimal d:
                e.Value = Formatters.Moneda(d);
                e.FormattingApplied = true;
                break;
            case FormatoColumna.Cantidad when e.Value is decimal c:
                e.Value = Formatters.Cantidad(c);
                e.FormattingApplied = true;
                break;
            case FormatoColumna.FechaUtc when e.Value is DateTime f:
                e.Value = Formatters.Fecha(f);
                e.FormattingApplied = true;
                break;
            case FormatoColumna.SoloFechaUtc when e.Value is DateTime f:
                e.Value = Formatters.SoloFecha(f);
                e.FormattingApplied = true;
                break;
            case FormatoColumna.Estado when e.Value is string s:
                e.Value = Formatters.Estado(s);
                e.FormattingApplied = true;
                break;
            case FormatoColumna.MetodoPago when e.Value is string m:
                e.Value = Formatters.MetodoPago(m);
                e.FormattingApplied = true;
                break;
        }
    }
}
