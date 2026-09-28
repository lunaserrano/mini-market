namespace MiniMarket.Desktop.Forms.Ventas;

/// <summary>
/// Historial de ventas por rango de fechas. Quien no tiene ventas.ver_todas solo ve las propias (lo
/// filtra la Api). Doble clic: detalle, reimpresión del ticket y anulación.
/// </summary>
public sealed class VentasForm : ListForm<VentaResumenDto>
{
    private readonly VentasApi _api;
    private readonly SessionService _sesion;
    private readonly TicketPrinter _printer;
    private readonly DateTimePicker _desde = new() { Format = DateTimePickerFormat.Short, Width = 120 };
    private readonly DateTimePicker _hasta = new() { Format = DateTimePickerFormat.Short, Width = 120 };
    private readonly Label _totales = new() { AutoSize = true, Font = Theme.FuenteNegrita, Margin = new Padding(12, 9, 4, 4) };

    public VentasForm(VentasApi api, SessionService sesion, TicketPrinter printer) : base("Ventas")
    {
        _api = api;
        _sesion = sesion;
        _printer = printer;
        _desde.Value = DateTime.Today;
        _hasta.Value = DateTime.Today;
        BtnEditar.Text = "Ver detalle";

        var filtros = new Control[]
        {
            Controles.Etiqueta("Desde"), _desde, Controles.Etiqueta("Hasta"), _hasta,
            Theme.Boton("Consultar", async (_, _) => await RecargarAsync(), primario: true)
        };
        for (var i = 0; i < filtros.Length; i++)
        {
            Barra.Controls.Add(filtros[i]);
            Barra.Controls.SetChildIndex(filtros[i], i + 1);
        }
        Barra.Controls.Add(_totales);
    }

    protected override bool PuedeEditar => true;
    protected override Color? ColorFila(VentaResumenDto item) =>
        item.Estado == "ANULADA" ? Theme.FilaInactiva : item.SaldoCredito is > 0 ? Theme.FilaAviso : null;

    protected override void ConfigurarColumnas(DataGridView grid)
    {
        grid.Col("Folio", nameof(VentaResumenDto.Folio), 80);
        grid.Col("Fecha", nameof(VentaResumenDto.Fecha), 150, FormatoColumna.FechaUtc);
        grid.Col("Cliente", nameof(VentaResumenDto.ClienteNombre), 250, relleno: true);
        grid.Col("Total", nameof(VentaResumenDto.Total), 120, FormatoColumna.Moneda);
        grid.Col("Saldo crédito", nameof(VentaResumenDto.SaldoCredito), 120, FormatoColumna.Moneda);
        grid.Col("Estado", nameof(VentaResumenDto.Estado), 110, FormatoColumna.Estado);
    }

    protected override async Task<IReadOnlyList<VentaResumenDto>> ObtenerAsync()
    {
        var desde = Formatters.AUtc(_desde.Value.Date);
        var hasta = Formatters.AUtc(_hasta.Value.Date.AddDays(1).AddTicks(-1));
        var ventas = (await _api.ListarAsync(_sesion.SucursalId, desde: desde, hasta: hasta)).OrderByDescending(v => v.Fecha).ToList();
        var validas = ventas.Where(v => v.Estado != "ANULADA").ToList();
        _totales.Text = $"Vendido: {Formatters.Moneda(validas.Sum(v => v.Total))} en {validas.Count} ventas · Anuladas: {ventas.Count - validas.Count}";
        return ventas;
    }

    protected override bool Coincide(VentaResumenDto i, string t) =>
        i.Folio.ToString() == t.TrimStart('#') || Contiene(i.ClienteNombre, t) || Contiene(i.Estado, t);

    protected override async Task EditarAsync(VentaResumenDto item)
    {
        var venta = await _api.ObtenerAsync(item.Id);
        using var detalle = new VentaDetalleForm(venta, item.ClienteNombre, item.SaldoCredito, _api, _sesion, _printer);
        detalle.ShowDialog(this);
        if (detalle.Anulada) await CargarAsync();
    }
}

public sealed class VentaDetalleForm : Form
{
    public bool Anulada { get; private set; }

    public VentaDetalleForm(VentaDto venta, string? clienteNombre, decimal? saldoCredito, VentasApi api, SessionService sesion, TicketPrinter printer)
    {
        Theme.Aplicar(this);
        Text = $"Venta #{venta.Folio}";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(900, 620);
        MinimizeBox = false;
        ShowInTaskbar = false;

        var cabecera = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, Padding = new Padding(12) };
        cabecera.Controls.Add(new Label { Text = $"Venta #{venta.Folio} · {Formatters.Estado(venta.Estado)}", Font = Theme.FuenteTitulo, AutoSize = true, ForeColor = venta.Estado == "ANULADA" ? Theme.Peligro : Color.Black });
        cabecera.Controls.Add(new Label { Text = $"Fecha: {Formatters.Fecha(venta.Fecha)} · Cliente: {clienteNombre ?? "Consumidor final"}", AutoSize = true });
        cabecera.Controls.Add(new Label
        {
            Text = $"Subtotal {Formatters.Moneda(venta.Subtotal)} · Descuentos {Formatters.Moneda(venta.DescuentoTotal)} · IVA incluido {Formatters.Moneda(venta.ImpuestoTotal)} · TOTAL {Formatters.Moneda(venta.Total)}" +
                   (saldoCredito is > 0 ? $" · Saldo a crédito {Formatters.Moneda(saldoCredito)}" : ""),
            AutoSize = true,
            Font = Theme.FuenteNegrita
        });

        var detalles = new DataGridView().Estandar();
        detalles.Col("Producto", nameof(DetalleVentaDto.ProductoNombre), 250, relleno: true);
        detalles.Col("Presentación", nameof(DetalleVentaDto.TipoPrecioNombre), 130);
        detalles.Col("Cantidad", nameof(DetalleVentaDto.Cantidad), 90, FormatoColumna.Cantidad);
        detalles.Col("Precio", nameof(DetalleVentaDto.PrecioUnitario), 100, FormatoColumna.Moneda);
        detalles.Col("Descuento", nameof(DetalleVentaDto.Descuento), 100, FormatoColumna.Moneda);
        detalles.Col("Subtotal", nameof(DetalleVentaDto.Subtotal), 110, FormatoColumna.Moneda);
        detalles.DataSource = venta.Detalles.ToList();

        var pagos = new DataGridView().Estandar();
        pagos.Dock = DockStyle.Bottom;
        pagos.Height = 120;
        pagos.Col("Método de pago", nameof(PagoVentaDto.Metodo), 160, FormatoColumna.MetodoPago);
        pagos.Col("Monto", nameof(PagoVentaDto.Monto), 120, FormatoColumna.Moneda);
        pagos.Col("Referencia", nameof(PagoVentaDto.Referencia), 200, relleno: true);
        pagos.DataSource = venta.Pagos.ToList();

        var botones = Controles.Barra(
            Theme.Boton("Reimprimir ticket", (_, _) => printer.Imprimir(this, printer.Construir(venta, clienteNombre, saldoCredito), vistaPrevia: true)));
        botones.Dock = DockStyle.Bottom;
        if (venta.Estado != "ANULADA" && sesion.Tiene(Permisos.VentasAnular))
        {
            botones.Controls.Add(Theme.Boton("Anular venta", async (_, _) =>
            {
                using var dlg = new EditDialog($"Anular venta #{venta.Folio}");
                var motivo = dlg.AgregarTexto("Motivo *", "", multilinea: true, maxLength: 250);
                var restituir = dlg.AgregarCheck("Devolver los productos al inventario", true);
                dlg.Validar(() => string.IsNullOrWhiteSpace(motivo.Text) ? "Indique el motivo de la anulación." : null);
                dlg.BotonAceptar.Text = "Anular";
                dlg.AlGuardar(() => api.AnularAsync(venta.Id, new AnularVentaRequest(motivo.Text.Trim(), restituir.Checked)));
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                Anulada = true;
                Dialogs.Info(this, "Venta anulada.");
                Close();
                await Task.CompletedTask;
            }, peligro: true));
        }

        Controls.Add(detalles);
        Controls.Add(pagos);
        Controls.Add(botones);
        Controls.Add(cabecera);
    }
}
