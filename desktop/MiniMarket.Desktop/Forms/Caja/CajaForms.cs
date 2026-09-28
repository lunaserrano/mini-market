using System.Text;

namespace MiniMarket.Desktop.Forms.Caja;

/// <summary>
/// Caja del usuario: apertura con fondo inicial, ingresos/egresos de efectivo y cierre (arqueo) con
/// la diferencia entre lo declarado y lo que calcula el sistema.
/// </summary>
public sealed class CajaForm : ChildForm
{
    private readonly CajaApi _api;
    private readonly VentasApi _ventas;
    private readonly SessionService _sesion;
    private readonly TicketPrinter _printer;
    private CajaDto? _caja;

    private readonly Label _estado = new() { AutoSize = true, Font = Theme.FuenteTitulo };
    private readonly Label _detalle = new() { AutoSize = true, ForeColor = Theme.TextoSuave, Margin = new Padding(4, 0, 4, 8) };
    private readonly NumericUpDown _montoInicial = Controles.Monto();
    private readonly DataGridView _grid = new DataGridView().Estandar();
    private readonly FlowLayoutPanel _panelApertura;
    private readonly FlowLayoutPanel _panelAbierta;
    private readonly Label _resumen = new() { AutoSize = true, Font = Theme.FuenteNegrita, Margin = new Padding(12, 9, 4, 4) };

    public CajaForm(CajaApi api, VentasApi ventas, SessionService sesion, TicketPrinter printer)
    {
        _api = api;
        _ventas = ventas;
        _sesion = sesion;
        _printer = printer;
        Text = "Caja";

        _panelApertura = Controles.Barra(
            Controles.Etiqueta("Fondo inicial"), _montoInicial,
            Theme.Boton("Abrir caja", async (_, _) => await this.EjecutarAsync(AbrirAsync), primario: true));
        _panelAbierta = Controles.Barra(
            Theme.Boton("Registrar ingreso", async (_, _) => await this.EjecutarAsync(() => MovimientoAsync("INGRESO"))),
            Theme.Boton("Registrar egreso", async (_, _) => await this.EjecutarAsync(() => MovimientoAsync("EGRESO"))),
            Theme.Boton("Cerrar caja (arqueo)", async (_, _) => await this.EjecutarAsync(CerrarAsync), peligro: true),
            _resumen);
        _montoInicial.Width = 140;

        if (_sesion.Tiene(Permisos.CajaVerTodas))
        {
            var historial = Theme.Boton("Historial de cajas", (_, _) => (MdiParent as MainForm)?.Abrir<HistorialCajasForm>());
            _panelApertura.Controls.Add(historial);
            _panelAbierta.Controls.Add(Theme.Boton("Historial de cajas", (_, _) => (MdiParent as MainForm)?.Abrir<HistorialCajasForm>()));
        }

        _grid.Col("Fecha", nameof(MovimientoCajaDto.Fecha), 150, FormatoColumna.FechaUtc);
        _grid.Col("Tipo", nameof(MovimientoCajaDto.Tipo), 110);
        _grid.Col("Concepto", nameof(MovimientoCajaDto.Concepto), 300, relleno: true);
        _grid.Col("Monto", nameof(MovimientoCajaDto.Monto), 120, FormatoColumna.Moneda);
        _grid.ColorearFilas<MovimientoCajaDto>(m => m.Tipo == "EGRESO" ? Theme.FilaAviso : null);

        var cabecera = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, Padding = new Padding(8) };
        cabecera.Controls.AddRange(new Control[] { _estado, _detalle });

        Controls.Add(_grid);
        Controls.Add(_panelAbierta);
        Controls.Add(_panelApertura);
        Controls.Add(cabecera);
    }

    public override string Ruta => "caja";

    /// <summary>Caja abierta actual (la usa el POS).</summary>
    public CajaDto? CajaActual => _caja;

    protected override async Task CargarAsync()
    {
        _caja = _sesion.Tiene(Permisos.CajaOperar) ? await _api.ObtenerActualAsync() : null;
        var abierta = _caja is not null;

        _panelApertura.Visible = !abierta && _sesion.Tiene(Permisos.CajaOperar);
        _panelAbierta.Visible = abierta;
        _grid.Visible = abierta;

        if (!abierta)
        {
            (_estado.Text, _estado.ForeColor) = ("Caja cerrada", Theme.TextoSuave);
            _detalle.Text = _sesion.Tiene(Permisos.CajaOperar)
                ? "Ingrese el fondo inicial (efectivo en gaveta) y abra la caja para comenzar a vender."
                : "No tiene permiso para operar caja.";
            _grid.DataSource = null;
            return;
        }

        (_estado.Text, _estado.ForeColor) = ($"Caja #{_caja!.Id} abierta", Theme.Exito);
        _detalle.Text = $"Abierta por {_caja.UsuarioAperturaNombre} el {Formatters.Fecha(_caja.FechaApertura)} · Fondo inicial {Formatters.Moneda(_caja.MontoInicial)}";
        var movimientos = await _api.MovimientosAsync(_caja.Id);
        _grid.DataSource = movimientos.OrderByDescending(m => m.Fecha).ToList();

        var ingresos = movimientos.Where(m => m.Tipo == "INGRESO").Sum(m => m.Monto);
        var egresos = movimientos.Where(m => m.Tipo == "EGRESO").Sum(m => m.Monto);
        _resumen.Text = $"Ingresos {Formatters.Moneda(ingresos)} · Egresos {Formatters.Moneda(egresos)}";
    }

    private async Task AbrirAsync()
    {
        if (!Dialogs.Confirmar(this, $"¿Abrir caja con un fondo inicial de {Formatters.Moneda(_montoInicial.Value)}?")) return;
        await _api.AbrirAsync(_montoInicial.Value);
        _montoInicial.Value = 0;
        await CargarAsync();
    }

    private async Task MovimientoAsync(string tipo)
    {
        using var dlg = new EditDialog(tipo == "INGRESO" ? "Ingreso de efectivo" : "Egreso de efectivo");
        var concepto = dlg.AgregarTexto("Concepto *", "", maxLength: 200);
        var monto = dlg.AgregarMonto("Monto *");
        dlg.Validar(() => string.IsNullOrWhiteSpace(concepto.Text) ? "Indique el concepto." : null);
        dlg.Validar(() => monto.Value <= 0 ? "El monto debe ser mayor a cero." : null);
        dlg.AlGuardar(() => _api.RegistrarMovimientoAsync(_caja!.Id, new MovimientoCajaCreateDto(tipo, concepto.Text.Trim(), monto.Value)));
        if (dlg.ShowDialog(this) == DialogResult.OK) await CargarAsync();
    }

    private async Task CerrarAsync()
    {
        var declarado = Dialogs.PedirMonto(this, "Cierre de caja", "Efectivo contado en gaveta");
        if (declarado is null) return;
        if (!Dialogs.Confirmar(this, $"¿Cerrar la caja #{_caja!.Id} declarando {Formatters.Moneda(declarado.Value)}?\nNo podrá registrar más ventas en esta caja.")) return;

        var cerrada = await _api.CerrarAsync(_caja.Id, declarado.Value);
        var diferencia = cerrada.Diferencia ?? 0;
        var mensaje = $"Caja #{cerrada.Id} cerrada.\n\n" +
                      $"Efectivo según sistema: {Formatters.Moneda(cerrada.MontoFinalSistema)}\n" +
                      $"Efectivo declarado:     {Formatters.Moneda(cerrada.MontoFinalDeclarado)}\n" +
                      $"Diferencia:             {Formatters.Moneda(diferencia)} {(diferencia == 0 ? "(cuadrada)" : diferencia > 0 ? "(sobrante)" : "(faltante)")}\n\n" +
                      "¿Imprimir el comprobante de cierre?";
        if (Dialogs.Confirmar(this, mensaje, "Cierre de caja"))
            _printer.Imprimir(this, await ComprobanteCierreAsync(cerrada), vistaPrevia: true);
        await CargarAsync();
    }

    private async Task<string> ComprobanteCierreAsync(CajaDto caja)
    {
        var sb = new StringBuilder();
        sb.AppendLine(_sesion.Empresa?.Nombre);
        sb.AppendLine($"CIERRE DE CAJA #{caja.Id}");
        sb.AppendLine(new string('-', 42));
        sb.AppendLine($"Apertura: {Formatters.Fecha(caja.FechaApertura)} ({caja.UsuarioAperturaNombre})");
        sb.AppendLine($"Cierre:   {Formatters.Fecha(caja.FechaCierre)} ({caja.UsuarioCierreNombre})");
        sb.AppendLine(new string('-', 42));
        sb.AppendLine($"Fondo inicial:      {Formatters.Moneda(caja.MontoInicial),15}");
        if (_sesion.Tiene(Permisos.VentasVer))
        {
            var ventas = await _ventas.ListarAsync(cajaId: caja.Id);
            var completadas = ventas.Where(v => v.Estado != "ANULADA").ToList();
            sb.AppendLine($"Ventas ({completadas.Count}):        {Formatters.Moneda(completadas.Sum(v => v.Total)),15}");
            sb.AppendLine($"Ventas anuladas:    {ventas.Count(v => v.Estado == "ANULADA"),15}");
        }
        sb.AppendLine($"Efectivo sistema:   {Formatters.Moneda(caja.MontoFinalSistema),15}");
        sb.AppendLine($"Efectivo declarado: {Formatters.Moneda(caja.MontoFinalDeclarado),15}");
        sb.AppendLine($"Diferencia:         {Formatters.Moneda(caja.Diferencia),15}");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("Firma cajero: ______________________");
        sb.AppendLine();
        sb.AppendLine("Firma supervisor: __________________");
        return sb.ToString();
    }
}

/// <summary>Cajas de todos los usuarios (permiso caja.ver_todas).</summary>
public sealed class HistorialCajasForm : ListForm<CajaDto>
{
    private readonly CajaApi _api;
    private readonly SessionService _sesion;

    public HistorialCajasForm(CajaApi api, SessionService sesion) : base("Historial de cajas")
    {
        _api = api;
        _sesion = sesion;
        BtnEditar.Text = "Ver movimientos";
    }

    protected override bool PuedeEditar => _sesion.Tiene(Permisos.CajaOperar);

    protected override Color? ColorFila(CajaDto item) =>
        item.Estado == "ABIERTA" ? Theme.FilaAviso : item.Diferencia is < 0 ? Theme.FilaAlerta : null;

    protected override void ConfigurarColumnas(DataGridView grid)
    {
        grid.Col("#", nameof(CajaDto.Id), 60);
        grid.Col("Apertura", nameof(CajaDto.FechaApertura), 140, FormatoColumna.FechaUtc);
        grid.Col("Abrió", nameof(CajaDto.UsuarioAperturaNombre), 160, relleno: true);
        grid.Col("Fondo", nameof(CajaDto.MontoInicial), 100, FormatoColumna.Moneda);
        grid.Col("Cierre", nameof(CajaDto.FechaCierre), 140, FormatoColumna.FechaUtc);
        grid.Col("Cerró", nameof(CajaDto.UsuarioCierreNombre), 160);
        grid.Col("Sistema", nameof(CajaDto.MontoFinalSistema), 100, FormatoColumna.Moneda);
        grid.Col("Declarado", nameof(CajaDto.MontoFinalDeclarado), 100, FormatoColumna.Moneda);
        grid.Col("Diferencia", nameof(CajaDto.Diferencia), 100, FormatoColumna.Moneda);
        grid.Col("Estado", nameof(CajaDto.Estado), 90, FormatoColumna.Estado);
    }

    protected override async Task<IReadOnlyList<CajaDto>> ObtenerAsync() =>
        (await _api.ListarAsync(_sesion.SucursalId)).OrderByDescending(c => c.FechaApertura).ToList();

    protected override bool Coincide(CajaDto i, string t) =>
        i.Id.ToString() == t || Contiene(i.UsuarioAperturaNombre, t) || Contiene(i.UsuarioCierreNombre, t);

    protected override async Task EditarAsync(CajaDto item)
    {
        var movimientos = await _api.MovimientosAsync(item.Id);
        using var dlg = new EditDialog($"Movimientos de la caja #{item.Id}", 720);
        var grid = new DataGridView().Estandar();
        grid.Dock = DockStyle.None;
        grid.Size = new Size(660, 300);
        grid.Col("Fecha", nameof(MovimientoCajaDto.Fecha), 140, FormatoColumna.FechaUtc);
        grid.Col("Tipo", nameof(MovimientoCajaDto.Tipo), 90);
        grid.Col("Concepto", nameof(MovimientoCajaDto.Concepto), 250, relleno: true);
        grid.Col("Monto", nameof(MovimientoCajaDto.Monto), 110, FormatoColumna.Moneda);
        grid.DataSource = movimientos;
        dlg.AgregarAncho(grid);
        dlg.BotonAceptar.Visible = false;
        dlg.BotonCancelar.Text = "Cerrar";
        dlg.ShowDialog(this);
    }
}
