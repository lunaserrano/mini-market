namespace MiniMarket.Desktop.Forms.Creditos;

/// <summary>Cuentas por cobrar: ventas a crédito, su saldo y los abonos del cliente.</summary>
public sealed class CreditosForm : ListForm<CreditoResumenDto>
{
    private readonly CreditosApi _api;
    private readonly SessionService _sesion;
    private readonly ComboBox _estado = Controles.Combo(new[] { "PENDIENTE", "PAGADO", "ANULADO", "" },
        e => e.Length == 0 ? "Todos" : Formatters.Estado(e));
    private readonly Label _totales = new() { AutoSize = true, Font = Theme.FuenteNegrita, Margin = new Padding(12, 9, 4, 4) };

    public CreditosForm(CreditosApi api, SessionService sesion) : base("Créditos")
    {
        _api = api;
        _sesion = sesion;
        BtnEditar.Text = "Ver / abonar";
        _estado.SelectedIndex = 0;
        _estado.Width = 130;
        _estado.SelectedIndexChanged += async (_, _) => await RecargarAsync();
        var etiqueta = Controles.Etiqueta("Estado");
        Barra.Controls.Add(etiqueta);
        Barra.Controls.Add(_estado);
        Barra.Controls.SetChildIndex(etiqueta, 1);
        Barra.Controls.SetChildIndex(_estado, 2);
        Barra.Controls.Add(_totales);
    }

    protected override bool PuedeEditar => true;
    protected override Color? ColorFila(CreditoResumenDto item) =>
        item.Vencido ? Theme.FilaAlerta : item.Estado != "PENDIENTE" ? Theme.FilaInactiva : null;

    protected override void ConfigurarColumnas(DataGridView grid)
    {
        grid.Col("#", nameof(CreditoResumenDto.Id), 60);
        grid.Col("Venta", nameof(CreditoResumenDto.VentaFolio), 70);
        grid.Col("Cliente", nameof(CreditoResumenDto.ClienteNombre), 230, relleno: true);
        grid.Col("Fecha", nameof(CreditoResumenDto.FechaCreacion), 140, FormatoColumna.FechaUtc);
        grid.Col("Vence", nameof(CreditoResumenDto.FechaVencimiento), 100, FormatoColumna.SoloFechaUtc);
        grid.Col("Monto", nameof(CreditoResumenDto.MontoOriginal), 110, FormatoColumna.Moneda);
        grid.Col("Saldo", nameof(CreditoResumenDto.SaldoPendiente), 110, FormatoColumna.Moneda);
        grid.Col("Estado", nameof(CreditoResumenDto.Estado), 100, FormatoColumna.Estado);
        grid.Col("Vencido", nameof(CreditoResumenDto.Vencido), 70, FormatoColumna.SiNo);
    }

    protected override async Task<IReadOnlyList<CreditoResumenDto>> ObtenerAsync()
    {
        var estado = _estado.Seleccion<string>();
        var creditos = await _api.ListarAsync(estado: string.IsNullOrEmpty(estado) ? null : estado);
        var pendientes = creditos.Where(c => c.Estado == "PENDIENTE").ToList();
        _totales.Text = $"Por cobrar: {Formatters.Moneda(pendientes.Sum(c => c.SaldoPendiente))} · Vencidos: {pendientes.Count(c => c.Vencido)}";
        return creditos.OrderByDescending(c => c.Vencido).ThenBy(c => c.FechaVencimiento ?? DateTime.MaxValue).ToList();
    }

    protected override bool Coincide(CreditoResumenDto i, string t) =>
        Contiene(i.ClienteNombre, t) || i.VentaFolio.ToString() == t.TrimStart('#') || i.Id.ToString() == t;

    protected override async Task EditarAsync(CreditoResumenDto item)
    {
        using var detalle = new CreditoDetalleForm(await _api.ObtenerAsync(item.Id), _api, _sesion.Tiene(Permisos.CreditosAbonar));
        detalle.ShowDialog(this);
        if (detalle.HuboAbonos) await CargarAsync();
    }
}

public sealed class CreditoDetalleForm : Form
{
    private readonly CreditosApi _api;
    private CreditoDto _credito;
    private readonly Label _encabezado = new() { AutoSize = true, Font = Theme.FuenteTitulo };
    private readonly Label _resumen = new() { AutoSize = true };
    private readonly DataGridView _abonos = new DataGridView().Estandar();
    private readonly Button _abonar;

    public bool HuboAbonos { get; private set; }

    public CreditoDetalleForm(CreditoDto credito, CreditosApi api, bool puedeAbonar)
    {
        _api = api;
        _credito = credito;
        Theme.Aplicar(this);
        Text = $"Crédito #{credito.Id}";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(820, 560);
        MinimizeBox = false;
        ShowInTaskbar = false;

        _abonos.Col("Fecha", nameof(AbonoCreditoDto.Fecha), 150, FormatoColumna.FechaUtc);
        _abonos.Col("Método", nameof(AbonoCreditoDto.Metodo), 130, FormatoColumna.MetodoPago);
        _abonos.Col("Monto", nameof(AbonoCreditoDto.Monto), 120, FormatoColumna.Moneda);
        _abonos.Col("Referencia", nameof(AbonoCreditoDto.Referencia), 150);
        _abonos.Col("Recibió", nameof(AbonoCreditoDto.UsuarioNombre), 180, relleno: true);

        _abonar = Theme.Boton("Registrar abono", async (_, _) => await this.EjecutarAsync(AbonarAsync), primario: true);
        _abonar.Visible = puedeAbonar;
        var botones = Controles.Barra(_abonar);
        botones.Dock = DockStyle.Bottom;

        var cabecera = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, Padding = new Padding(12) };
        cabecera.Controls.AddRange(new Control[] { _encabezado, _resumen });

        Controls.Add(_abonos);
        Controls.Add(botones);
        Controls.Add(cabecera);
        Mostrar();
    }

    private void Mostrar()
    {
        var c = _credito;
        var vence = c.FechaVencimiento is null ? "sin fecha" : Formatters.SoloFecha(c.FechaVencimiento);
        (_encabezado.Text, _encabezado.ForeColor) = ($"{c.ClienteNombre} · Saldo {Formatters.Moneda(c.SaldoPendiente)}",
            c.Vencido ? Theme.Peligro : c.Estado == "PAGADO" ? Theme.Exito : Color.Black);
        _resumen.Text =
            $"Venta #{c.VentaFolio} del {Formatters.Fecha(c.FechaCreacion)} · Total venta {Formatters.Moneda(c.TotalVenta)} · Pagado al vender {Formatters.Moneda(c.TotalVenta - c.MontoOriginal)}\n" +
            $"Monto a crédito {Formatters.Moneda(c.MontoOriginal)} · Vence {vence}" +
            $" · Estado {Formatters.Estado(c.Estado)}{(c.Vencido ? " (VENCIDO)" : "")}" +
            (c.FechaCancelacion is not null ? $" · Cancelado el {Formatters.Fecha(c.FechaCancelacion)}" : "");
        _abonos.DataSource = c.Abonos.OrderByDescending(a => a.Fecha).ToList();
        _abonar.Enabled = c.Estado == "PENDIENTE";
    }

    private Task AbonarAsync()
    {
        using var dlg = new EditDialog($"Abono a {_credito.ClienteNombre}");
        dlg.AgregarAncho(new Label { Text = $"Saldo pendiente: {Formatters.Moneda(_credito.SaldoPendiente)}", Font = Theme.FuenteNegrita, AutoSize = true });
        var metodo = dlg.AgregarCombo("Método", Formatters.MetodosPago, m => m.Texto, m => m.Valor == "EFECTIVO");
        var monto = dlg.AgregarMonto("Monto *", _credito.SaldoPendiente);
        var referencia = dlg.AgregarTexto("Referencia", "", maxLength: 100);
        dlg.Validar(() => monto.Value <= 0 ? "El monto debe ser mayor a cero." : null);
        dlg.Validar(() => monto.Value > _credito.SaldoPendiente ? $"El abono no puede superar el saldo ({Formatters.Moneda(_credito.SaldoPendiente)})." : null);
        dlg.AlGuardar(async () =>
            _credito = await _api.AbonarAsync(_credito.Id, new AbonoCreditoCreateDto(metodo.Seleccion<(string Valor, string Texto)>().Valor, monto.Value, referencia.TextoONull())));
        if (dlg.ShowDialog(this) != DialogResult.OK) return Task.CompletedTask;

        HuboAbonos = true;
        Mostrar();
        if (_credito.Estado == "PAGADO") Dialogs.Info(this, "¡Crédito cancelado en su totalidad!");
        return Task.CompletedTask;
    }
}
