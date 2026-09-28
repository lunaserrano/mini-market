using System.ComponentModel;

namespace MiniMarket.Desktop.Forms.Pos;

/// <summary>
/// Cobro de la venta: uno o varios pagos (efectivo, tarjeta, transferencia) y, con permiso
/// creditos.otorgar, venta a crédito (el saldo no pagado queda como deuda del cliente).
///
/// Cambio: si el efectivo entregado supera lo que falta cubrir, a la Api se envía solo lo necesario
/// y la diferencia se muestra como cambio. Así el ingreso de efectivo que registra la Api en la caja
/// coincide con lo que realmente queda en la gaveta.
/// </summary>
public sealed class CobroDialog : Form
{
    private const decimal Epsilon = 0.005m;

    [System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)] // enlazado a grillas por nombre de propiedad
    private sealed class FilaPago
    {
        public string Metodo { get; set; } = "EFECTIVO";
        public decimal Monto { get; set; }
        public string? Referencia { get; set; }
    }

    private readonly decimal _total;
    private readonly ClienteDto? _cliente;
    private readonly BindingList<FilaPago> _pagos = new();
    private readonly DataGridView _grid = new DataGridView().Estandar(soloLectura: false);
    private readonly CheckBox _alCredito = new() { Text = "Venta a crédito (el saldo queda como deuda del cliente)", AutoSize = true };
    private readonly DateTimePicker _vence = new() { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false, Width = 140 };
    private readonly Label _lblPagado = new() { AutoSize = true, Font = Theme.FuenteNegrita };
    private readonly Label _lblResultado = new() { AutoSize = true, Font = new Font("Segoe UI", 16F, FontStyle.Bold) };
    private readonly Button _confirmar;

    public IReadOnlyList<PagoVentaCreateDto> Pagos { get; private set; } = Array.Empty<PagoVentaCreateDto>();
    public bool AlCredito => _alCredito.Checked;
    public DateTime? FechaVencimiento => AlCredito && _vence.Checked ? _vence.Value.Date : null;
    public decimal Cambio { get; private set; }
    public decimal? EfectivoRecibido { get; private set; }

    public CobroDialog(decimal total, ClienteDto? cliente, bool puedeCredito)
    {
        _total = total;
        _cliente = cliente;
        Theme.Aplicar(this);
        Text = "Cobrar venta";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(640, 520);
        KeyPreview = true;

        _confirmar = Theme.Boton("Confirmar venta (F4)", (_, _) => Confirmar(), primario: true);
        _confirmar.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        var cancelar = Theme.Boton("Cancelar");
        cancelar.DialogResult = DialogResult.Cancel;
        CancelButton = cancelar;

        var cabecera = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, Padding = new Padding(12) };
        cabecera.Controls.Add(new Label { Text = "Total a cobrar", AutoSize = true, ForeColor = Theme.TextoSuave });
        cabecera.Controls.Add(new Label { Text = Formatters.Moneda(total), Font = Theme.FuenteTotal, ForeColor = Theme.Primario, AutoSize = true });
        cabecera.Controls.Add(new Label { Text = "Cliente: " + (cliente?.Nombre ?? "Consumidor final"), AutoSize = true });
        if (puedeCredito)
        {
            cabecera.Controls.Add(_alCredito);
            var venceFila = new FlowLayoutPanel { AutoSize = true, Controls = { Controles.Etiqueta("Vence el"), _vence } };
            cabecera.Controls.Add(venceFila);
            _vence.MinDate = DateTime.Today;
            venceFila.Enabled = false;
            _alCredito.CheckedChanged += (_, _) =>
            {
                if (_alCredito.Checked && _cliente is null)
                {
                    Dialogs.Aviso(this, "Seleccione un cliente en el punto de venta para vender a crédito.");
                    _alCredito.Checked = false;
                    return;
                }
                venceFila.Enabled = _alCredito.Checked;
                // Igual que la web: a crédito el pago inicial arranca en 0; de contado cubre el total.
                _pagos.Clear();
                _pagos.Add(new FilaPago { Monto = _alCredito.Checked ? 0 : _total });
            };
        }

        var metodo = new DataGridViewComboBoxColumn
        {
            HeaderText = "Método",
            DataPropertyName = nameof(FilaPago.Metodo),
            Width = 160,
            DisplayMember = "Texto",
            ValueMember = "Valor",
            DataSource = Formatters.MetodosPago.Select(m => new Opcion<string>(m.Valor, m.Texto)).ToList(),
            FlatStyle = FlatStyle.Flat
        };
        _grid.Columns.Add(metodo);
        _grid.Col("Monto recibido", nameof(FilaPago.Monto), 150, FormatoColumna.Moneda, editable: true);
        _grid.Col("Referencia (voucher / transferencia)", nameof(FilaPago.Referencia), 200, relleno: true, editable: true);
        _grid.DataSource = _pagos;
        _grid.EditMode = DataGridViewEditMode.EditOnEnter;
        _grid.DataError += (_, e) => { e.Cancel = true; e.ThrowException = false; };
        _grid.CellValueChanged += (_, _) => Recalcular();
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewComboBoxCell) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _pagos.ListChanged += (_, _) => Recalcular();

        var acciones = Controles.Barra(
            Theme.Boton("+ Agregar pago", (_, _) => _pagos.Add(new FilaPago { Monto = Math.Max(0, Faltante) })),
            Theme.Boton("Quitar pago", (_, _) => { if (_grid.Seleccionado<FilaPago>() is { } p && _pagos.Count > 1) _pagos.Remove(p); }));
        acciones.Dock = DockStyle.Bottom;

        var pie = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.TopDown, Padding = new Padding(12) };
        pie.Controls.Add(_lblPagado);
        pie.Controls.Add(_lblResultado);
        var botones = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, Padding = new Padding(8) };
        botones.Controls.AddRange(new Control[] { _confirmar, cancelar });

        Controls.Add(_grid);
        Controls.Add(acciones);
        Controls.Add(pie);
        Controls.Add(botones);
        Controls.Add(cabecera);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F4) { e.Handled = true; Confirmar(); }
        };

        _pagos.Add(new FilaPago { Monto = total });
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Foco directo en el monto del primer pago: el cajero escribe lo que le entregan y presiona F4.
        if (_grid.Rows.Count > 0)
        {
            _grid.CurrentCell = _grid.Rows[0].Cells[1];
            _grid.BeginEdit(selectAll: true);
        }
    }

    private decimal TotalPagado => _pagos.Sum(p => p.Monto);
    private decimal Faltante => _total - TotalPagado;

    private void Recalcular()
    {
        _lblPagado.Text = $"Pagado: {Formatters.Moneda(TotalPagado)}";
        if (AlCredito)
        {
            var saldo = Math.Max(0, Faltante);
            (_lblResultado.Text, _lblResultado.ForeColor) = saldo > Epsilon
                ? ($"A crédito: {Formatters.Moneda(saldo)}", Theme.Advertencia)
                : ("Los pagos cubren el total: no queda saldo a crédito", Theme.Peligro);
            _confirmar.Enabled = saldo > Epsilon;
        }
        else if (Faltante > 0.01m)
        {
            (_lblResultado.Text, _lblResultado.ForeColor) = ($"Faltan: {Formatters.Moneda(Faltante)}", Theme.Peligro);
            _confirmar.Enabled = false;
        }
        else
        {
            var cambio = -Faltante;
            (_lblResultado.Text, _lblResultado.ForeColor) = cambio > Epsilon
                ? ($"Cambio: {Formatters.Moneda(cambio)}", Theme.Exito)
                : ("Pago exacto", Theme.Exito);
            _confirmar.Enabled = true;
        }
    }

    private void Confirmar()
    {
        if (_grid.IsCurrentCellInEditMode) _grid.EndEdit();
        Recalcular();
        if (!_confirmar.Enabled) return;

        var pagos = _pagos.Where(p => p.Monto > 0).ToList();
        var exceso = TotalPagado - _total;
        EfectivoRecibido = null;
        Cambio = 0;

        if (!AlCredito && exceso > Epsilon)
        {
            // El excedente solo puede devolverse en efectivo: se descuenta de los pagos en efectivo.
            var efectivo = pagos.Where(p => p.Metodo == "EFECTIVO").Sum(p => p.Monto);
            if (efectivo + Epsilon < exceso)
            {
                Dialogs.Aviso(this, "Los pagos con tarjeta/transferencia superan el total. Corrija los montos.");
                return;
            }
            EfectivoRecibido = efectivo;
            Cambio = exceso;
            var porDescontar = exceso;
            foreach (var p in pagos.Where(p => p.Metodo == "EFECTIVO").Reverse())
            {
                var descuento = Math.Min(p.Monto, porDescontar);
                p.Monto -= descuento;
                porDescontar -= descuento;
            }
            pagos = pagos.Where(p => p.Monto > 0).ToList();
        }

        if (pagos.Any(p => p.Metodo != "EFECTIVO" && string.IsNullOrWhiteSpace(p.Referencia)) &&
            !Dialogs.Confirmar(this, "Hay pagos con tarjeta/transferencia sin referencia. ¿Continuar de todas formas?"))
            return;

        Pagos = pagos.Select(p => new PagoVentaCreateDto(p.Metodo, Math.Round(p.Monto, 2), string.IsNullOrWhiteSpace(p.Referencia) ? null : p.Referencia.Trim())).ToList();
        DialogResult = DialogResult.OK;
        Close();
    }
}
