using System.Drawing.Printing;

namespace MiniMarket.Desktop.UI;

/// <summary>
/// Reporte impreso de una grilla (vista previa + impresora o "Microsoft Print to PDF"): título,
/// fecha, columnas visibles con sus valores formateados y paginación automática.
/// </summary>
public static class GridPrinter
{
    public static void Imprimir(IWin32Window owner, DataGridView grid, string titulo, string? subtitulo = null)
    {
        var columnas = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).OrderBy(c => c.DisplayIndex).ToList();
        var filas = grid.Rows.Cast<DataGridViewRow>()
            .Select(r => columnas.Select(c => r.Cells[c.Index].FormattedValue?.ToString() ?? "").ToArray())
            .ToList();
        if (filas.Count == 0)
        {
            Dialogs.Info(owner, "No hay datos para imprimir.");
            return;
        }

        var siguiente = 0;
        var pagina = 0;
        using var doc = new PrintDocument { DocumentName = titulo };
        doc.DefaultPageSettings.Landscape = columnas.Sum(c => c.Width) > 750;
        doc.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);
        doc.BeginPrint += (_, _) => { siguiente = 0; pagina = 0; };
        doc.PrintPage += (_, e) =>
        {
            var g = e.Graphics!;
            var area = e.MarginBounds;
            using var fTitulo = new Font("Segoe UI", 13, FontStyle.Bold);
            using var fEnc = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var fCelda = new Font("Segoe UI", 8.5f);
            var y = (float)area.Top;
            pagina++;

            g.DrawString(titulo, fTitulo, Brushes.Black, area.Left, y);
            y += fTitulo.GetHeight(g) + 2;
            g.DrawString($"{subtitulo ?? ""}  Impreso: {DateTime.Now:dd/MM/yyyy HH:mm}  ·  Página {pagina}".Trim(), fCelda, Brushes.Gray, area.Left, y);
            y += fCelda.GetHeight(g) + 8;

            // Anchos proporcionales al ancho de pantalla de cada columna.
            var total = columnas.Sum(c => (float)c.Width);
            var anchos = columnas.Select(c => c.Width / total * area.Width).ToArray();
            var alto = fCelda.GetHeight(g) + 6;
            var formato = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };

            void Fila(IReadOnlyList<string> valores, Font fuente, bool fondo)
            {
                var x = (float)area.Left;
                if (fondo) g.FillRectangle(Brushes.Gainsboro, area.Left, y, area.Width, alto);
                for (var i = 0; i < valores.Count; i++)
                {
                    var derecha = columnas[i].DefaultCellStyle.Alignment == DataGridViewContentAlignment.MiddleRight;
                    formato.Alignment = derecha ? StringAlignment.Far : StringAlignment.Near;
                    g.DrawString(valores[i], fuente, Brushes.Black, new RectangleF(x + 2, y + 3, anchos[i] - 4, alto), formato);
                    x += anchos[i];
                }
                y += alto;
                g.DrawLine(Pens.LightGray, area.Left, y, area.Right, y);
            }

            Fila(columnas.Select(c => c.HeaderText).ToArray(), fEnc, fondo: true);
            while (siguiente < filas.Count && y + alto <= area.Bottom)
                Fila(filas[siguiente++], fCelda, fondo: false);

            e.HasMorePages = siguiente < filas.Count;
        };

        using var vista = new PrintPreviewDialog { Document = doc, WindowState = FormWindowState.Maximized, UseAntiAlias = true };
        vista.ShowDialog(owner);
    }
}
