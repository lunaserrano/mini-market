using System.Drawing.Printing;
using System.Text;

namespace MiniMarket.Desktop.Services;

/// <summary>
/// Ticket de venta para impresora térmica (58/80 mm) o cualquier impresora de Windows. Impresora en
/// Ticket:Impresora (vacío = predeterminada) y ancho en Ticket:AnchoMm. Texto monoespaciado: no
/// depende de drivers ESC/POS.
/// </summary>
public sealed class TicketPrinter
{
    private readonly SessionService _sesion;
    private readonly string? _impresora;
    private readonly int _anchoMm;

    public TicketPrinter(SessionService sesion, IConfiguration configuration)
    {
        _sesion = sesion;
        _impresora = configuration["Ticket:Impresora"];
        _anchoMm = configuration.GetValue("Ticket:AnchoMm", 80);
    }

    private int Columnas => _anchoMm <= 58 ? 32 : 42;

    public string Construir(VentaDto venta, string? clienteNombre, decimal? saldoCredito = null, decimal? efectivoRecibido = null)
    {
        var n = Columnas;
        var sb = new StringBuilder();
        var empresa = _sesion.Empresa;

        void Centro(string? t) { if (!string.IsNullOrWhiteSpace(t)) sb.AppendLine(Centrar(t, n)); }
        void Linea() => sb.AppendLine(new string('-', n));
        void Par(string izq, string der) => sb.AppendLine(Justificar(izq, der, n));

        Centro(empresa?.Nombre);
        Centro(empresa?.RazonSocial);
        if (!string.IsNullOrWhiteSpace(empresa?.IdentificacionFiscal)) Centro("NIT/RUC: " + empresa.IdentificacionFiscal);
        Linea();
        Par($"Ticket #{venta.Folio}", Formatters.Fecha(venta.Fecha));
        sb.AppendLine(Recortar("Cajero: " + (_sesion.Usuario?.NombreCompleto ?? ""), n));
        if (!string.IsNullOrWhiteSpace(clienteNombre)) sb.AppendLine(Recortar("Cliente: " + clienteNombre, n));
        if (venta.Estado == "ANULADA") Centro("*** VENTA ANULADA ***");
        Linea();

        foreach (var d in venta.Detalles)
        {
            sb.AppendLine(Recortar($"{d.ProductoNombre} ({d.TipoPrecioNombre})", n));
            Par($"  {Formatters.Cantidad(d.Cantidad)} x {Formatters.Moneda(d.PrecioUnitario)}", Formatters.Moneda(d.Subtotal));
            if (d.Descuento > 0) Par("  Descuento", "-" + Formatters.Moneda(d.Descuento));
        }

        Linea();
        Par("Subtotal", Formatters.Moneda(venta.Subtotal));
        if (venta.DescuentoTotal > 0) Par("Descuentos", "-" + Formatters.Moneda(venta.DescuentoTotal));
        Par($"IVA incluido ({empresa?.TasaImpuesto ?? 0:0.##}%)", Formatters.Moneda(venta.ImpuestoTotal));
        Par("TOTAL", Formatters.Moneda(venta.Total));
        Linea();

        foreach (var p in venta.Pagos)
            Par(Formatters.MetodoPago(p.Metodo) + (string.IsNullOrWhiteSpace(p.Referencia) ? "" : $" ({p.Referencia})"), Formatters.Moneda(p.Monto));
        // La Api guarda solo el efectivo aplicado; lo entregado y el cambio los conoce el POS.
        var efectivoAplicado = venta.Pagos.Where(p => p.Metodo == "EFECTIVO").Sum(p => p.Monto);
        if (efectivoRecibido is { } recibido && recibido > efectivoAplicado)
        {
            Par("Efectivo recibido", Formatters.Moneda(recibido));
            Par("CAMBIO", Formatters.Moneda(recibido - efectivoAplicado));
        }
        if (saldoCredito is > 0) Par("SALDO A CRÉDITO", Formatters.Moneda(saldoCredito.Value));

        sb.AppendLine();
        Centro("¡Gracias por su compra!");
        return sb.ToString();
    }

    public void Imprimir(IWin32Window owner, string contenido, bool vistaPrevia = false)
    {
        var lineas = contenido.Replace("\r", "").Split('\n');
        var indice = 0;
        using var doc = new PrintDocument { DocumentName = "Ticket MiniMarket" };
        if (!string.IsNullOrWhiteSpace(_impresora)) doc.PrinterSettings.PrinterName = _impresora;
        if (!doc.PrinterSettings.IsValid)
        {
            Dialogs.Aviso(owner, $"La impresora '{_impresora}' no está disponible. Revise Ticket:Impresora en appsettings.json.");
            return;
        }

        // Papel continuo: alto calculado según las líneas (1/100 pulgada).
        var anchoCentesimas = (int)(_anchoMm / 25.4 * 100);
        var altoCentesimas = Math.Max(300, lineas.Length * 14 + 60);
        doc.DefaultPageSettings.PaperSize = new PaperSize("Ticket", anchoCentesimas, altoCentesimas);
        doc.DefaultPageSettings.Margins = new Margins(8, 8, 10, 10);
        doc.BeginPrint += (_, _) => indice = 0;
        doc.PrintPage += (_, e) =>
        {
            var g = e.Graphics!;
            var alto = Theme.FuenteTicket.GetHeight(g);
            float y = e.MarginBounds.Top;
            while (indice < lineas.Length && y + alto <= e.PageBounds.Bottom)
            {
                g.DrawString(lineas[indice++], Theme.FuenteTicket, Brushes.Black, e.MarginBounds.Left, y);
                y += alto;
            }
            e.HasMorePages = indice < lineas.Length;
        };

        try
        {
            if (vistaPrevia)
            {
                using var vista = new PrintPreviewDialog { Document = doc, Width = 420, Height = 700, UseAntiAlias = true };
                vista.ShowDialog(owner);
            }
            else
            {
                doc.Print();
            }
        }
        catch (InvalidPrinterException ex)
        {
            Dialogs.Aviso(owner, "No se pudo imprimir el ticket: " + ex.Message);
        }
    }

    private static string Recortar(string t, int n) => t.Length <= n ? t : t[..n];
    private static string Centrar(string t, int n)
    {
        t = Recortar(t, n);
        return new string(' ', (n - t.Length) / 2) + t;
    }
    private static string Justificar(string izq, string der, int n)
    {
        izq = Recortar(izq, Math.Max(1, n - der.Length - 1));
        return izq + new string(' ', Math.Max(1, n - izq.Length - der.Length)) + der;
    }
}
