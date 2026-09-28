using System.Text;

namespace MiniMarket.Desktop.UI;

/// <summary>Exporta lo que muestra una grilla (columnas visibles, valores ya formateados) a CSV para Excel.</summary>
public static class CsvExporter
{
    // ";" es el separador que Excel espera con configuración regional en español.
    private const char Separador = ';';

    public static void Exportar(IWin32Window owner, DataGridView grid, string nombre)
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"{nombre} {DateTime.Now:yyyy-MM-dd HHmm}.csv"
        };
        if (dlg.ShowDialog(owner) != DialogResult.OK) return;

        var columnas = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).OrderBy(c => c.DisplayIndex).ToList();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(Separador, columnas.Select(c => Escapar(c.HeaderText))));
        foreach (DataGridViewRow fila in grid.Rows)
            sb.AppendLine(string.Join(Separador, columnas.Select(c => Escapar(fila.Cells[c.Index].FormattedValue?.ToString()))));

        try
        {
            File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            Dialogs.Info(owner, $"Se exportaron {grid.Rows.Count} registros.");
        }
        catch (IOException ex)
        {
            Dialogs.Error(owner, $"No se pudo guardar el archivo (¿está abierto en Excel?).\n{ex.Message}");
        }
    }

    private static string Escapar(string? valor)
    {
        valor ??= "";
        return valor.IndexOfAny(new[] { Separador, '"', '\n', '\r' }) >= 0
            ? $"\"{valor.Replace("\"", "\"\"")}\""
            : valor;
    }
}
