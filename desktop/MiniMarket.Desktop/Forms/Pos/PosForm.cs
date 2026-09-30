using System.ComponentModel;
using MiniMarket.Desktop.Forms.Caja;

namespace MiniMarket.Desktop.Forms.Pos;

/// <summary>Línea del carrito (editable en la grilla: cantidad y descuento).</summary>
[System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)] // enlazado a grillas por nombre de propiedad
public sealed class LineaCarrito : INotifyPropertyChanged
{
    private decimal _cantidad = 1;
    private decimal _descuento;

    public required int ProductoId { get; init; }
    public required string ProductoNombre { get; init; }
    public required int TipoPrecioId { get; init; }
    public required string TipoPrecioNombre { get; init; }
    public required decimal CantidadBase { get; init; }
    public required decimal PrecioUnitario { get; init; }
    public required decimal StockDisponible { get; init; }

    public decimal Cantidad
    {
        get => _cantidad;
        set
        {
            if (value <= 0) return; // un 0 mientras se edita no borra la línea (igual que la web)
            _cantidad = value;
            Notificar(nameof(Cantidad));
        }
    }

    public decimal Descuento
    {
        get => _descuento;
        set
        {
            _descuento = Math.Clamp(value, 0, PrecioUnitario * Cantidad);
            Notificar(nameof(Descuento));
        }
    }

    public decimal Subtotal => PrecioUnitario * Cantidad - Descuento;

    /// <summary>Unidades base que consume (para advertir stock insuficiente antes de cobrar; la Api lo valida igual).</summary>
    public decimal CantidadEnBase => Cantidad * CantidadBase;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notificar(string propiedad)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Subtotal)));
    }
}

/// <summary>
/// Punto de venta. Pensado para teclado y lector de código de barras (el lector "escribe" el código y
/// envía Enter): F2 buscar, F4 cobrar, Supr quitar línea, Esc limpiar búsqueda.
/// </summary>
public sealed class PosForm : ChildForm
{
    private readonly ProductosApi _productos;
    private readonly CajaApi _cajaApi;
    private readonly VentasApi _ventas;
    private readonly ClientesApi _clientes;
    private readonly SessionService _sesion;
    private readonly TicketPrinter _printer;
    private readonly NotificacionService _notificaciones;

    private CajaDto? _caja;
    private readonly BindingList<LineaCarrito> _carrito = new();
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 300 };
    private CancellationTokenSource? _busquedaCts;

    private readonly TextBox _buscar = new() { Font = new Font("Segoe UI", 14F), Width = 520, PlaceholderText = "Código de barras o nombre del producto (F2)" };
    private readonly DataGridView _resultados = new DataGridView().Estandar();
    private readonly DataGridView _gridCarrito = new DataGridView().Estandar(soloLectura: false);
    private readonly Label _lblCaja = new() { AutoSize = true, Margin = new Padding(12, 12, 4, 4), Font = Theme.FuenteNegrita };
    private readonly Button _btnAbrirCaja;
    private readonly ComboBox _cliente = new() { Width = 300 };
    private readonly Label _lblSubtotal = new() { AutoSize = true, Font = Theme.Fuente };
    private readonly Label _lblDescuento = new() { AutoSize = true, Font = Theme.Fuente };
    private readonly Label _lblTotal = new() { AutoSize = true, Font = Theme.FuenteTotal, ForeColor = Theme.Primario };
    private readonly Label _lblArticulos = new() { AutoSize = true, ForeColor = Theme.TextoSuave };
    private readonly Button _btnCobrar;

    public PosForm(ProductosApi productos, CajaApi cajaApi, VentasApi ventas, ClientesApi clientes, SessionService sesion, TicketPrinter printer,
        NotificacionService notificaciones)
    {
        _productos = productos;
        _cajaApi = cajaApi;
        _ventas = ventas;
        _clientes = clientes;
        _sesion = sesion;
        _printer = printer;
        _notificaciones = notificaciones;
        Text = "Punto de venta";

        _btnAbrirCaja = Theme.Boton("Ir a Caja", (_, _) => (MdiParent as MainForm)?.Abrir<CajaForm>(), primario: true);
        _btnCobrar = Theme.Boton("COBRAR (F4)", async (_, _) => await CobrarAsync(), primario: true);
        _btnCobrar.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        _btnCobrar.AutoSize = false;
        _btnCobrar.Size = new Size(320, 64);

        ConstruirLayout();
        ConfigurarGrillas();
        ConfigurarEventos();
    }

    public override string Ruta => "pos";

    private void ConstruirLayout()
    {
        var barra = Controles.Barra(_buscar,
            Theme.Boton("Buscar", async (_, _) => await BuscarAsync(agregarSiUnico: true)),
            _lblCaja, _btnAbrirCaja);

        _resultados.Dock = DockStyle.Top;
        _resultados.Height = 190;
        _resultados.Visible = false;

        // Panel derecho: cliente, totales y cobro.
        var derecho = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 350,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(12),
            BackColor = Theme.Panel
        };
        var puedeElegirCliente = _sesion.Tiene(Permisos.ClientesVer, Permisos.CreditosOtorgar);
        if (puedeElegirCliente)
        {
            derecho.Controls.Add(Controles.Etiqueta("Cliente", negrita: true));
            derecho.Controls.Add(_cliente);
        }
        derecho.Controls.Add(new Label { Height = 16 });
        derecho.Controls.Add(_lblArticulos);
        derecho.Controls.Add(_lblSubtotal);
        derecho.Controls.Add(_lblDescuento);
        derecho.Controls.Add(new Label { Text = "TOTAL", Font = Theme.FuenteNegrita, AutoSize = true, Margin = new Padding(3, 16, 3, 0) });
        derecho.Controls.Add(_lblTotal);
        derecho.Controls.Add(new Label { Height = 12 });
        derecho.Controls.Add(_btnCobrar);
        derecho.Controls.Add(Theme.Boton("Quitar línea (Supr)", (_, _) => QuitarLinea()));
        derecho.Controls.Add(Theme.Boton("Cancelar venta", (_, _) => CancelarVenta(), peligro: true));
        derecho.Controls.Add(new Label
        {
            Text = "F2 buscar · F4 cobrar · Supr quitar · Esc limpiar\nDoble clic en Cantidad o Descuento para editar.",
            ForeColor = Theme.TextoSuave,
            AutoSize = true,
            Margin = new Padding(3, 16, 3, 3)
        });

        Controls.Add(_gridCarrito);
        Controls.Add(_resultados);
        Controls.Add(derecho);
        Controls.Add(barra);
    }

    private void ConfigurarGrillas()
    {
        _resultados.Col("Producto", nameof(ProductoPosDto.Nombre), 300, relleno: true);
        _resultados.Col("Código", nameof(ProductoPosDto.CodigoBarras), 150);
        _resultados.Col("Unidad", nameof(ProductoPosDto.UnidadBase), 90);
        _resultados.Col("Stock", nameof(ProductoPosDto.StockActual), 90, FormatoColumna.Cantidad);
        var precio = _resultados.Col("Precio", "Precio", 110, FormatoColumna.Moneda);
        precio.DataPropertyName = "";
        _resultados.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex == precio.Index && e.RowIndex >= 0 && _resultados.Rows[e.RowIndex].DataBoundItem is ProductoPosDto p)
            {
                var tp = TipoDefault(p);
                e.Value = tp is null ? "" : Formatters.Moneda(tp.PrecioVenta) + (p.TiposPrecio.Count > 1 ? $" (+{p.TiposPrecio.Count - 1})" : "");
                e.FormattingApplied = true;
            }
        };
        _resultados.ColorearFilas<ProductoPosDto>(p => p.StockActual <= 0 ? Theme.FilaAlerta : null);

        _gridCarrito.Col("Producto", nameof(LineaCarrito.ProductoNombre), 250, relleno: true);
        _gridCarrito.Col("Presentación", nameof(LineaCarrito.TipoPrecioNombre), 130);
        _gridCarrito.Col("Cantidad", nameof(LineaCarrito.Cantidad), 100, FormatoColumna.Cantidad, editable: true);
        _gridCarrito.Col("Precio", nameof(LineaCarrito.PrecioUnitario), 110, FormatoColumna.Moneda);
        _gridCarrito.Col("Descuento", nameof(LineaCarrito.Descuento), 110, FormatoColumna.Moneda, editable: true);
        _gridCarrito.Col("Subtotal", nameof(LineaCarrito.Subtotal), 120, FormatoColumna.Moneda);
        _gridCarrito.Columns[nameof(LineaCarrito.Cantidad)]!.DefaultCellStyle.BackColor = Color.FromArgb(239, 246, 255);
        _gridCarrito.Columns[nameof(LineaCarrito.Descuento)]!.DefaultCellStyle.BackColor = Color.FromArgb(239, 246, 255);
        _gridCarrito.ColorearFilas<LineaCarrito>(l => StockTotalCarrito(l.ProductoId) > l.StockDisponible ? Theme.FilaAviso : null);
        _gridCarrito.DataSource = _carrito;
        _gridCarrito.DataError += (_, e) =>
        {
            e.Cancel = true;
            e.ThrowException = false;
            Dialogs.Aviso(this, "Ingrese un número válido.");
        };
        _gridCarrito.EditingControlShowing += (_, e) =>
        {
            if (e.Control is TextBox tb) tb.Text = _gridCarrito.CurrentCell?.Value is decimal d ? d.ToString("0.###") : tb.Text;
        };
    }

    private void ConfigurarEventos()
    {
        _carrito.ListChanged += (_, _) => ActualizarTotales();
        _buscar.TextChanged += (_, _) => { _debounce.Stop(); _debounce.Start(); };
        _debounce.Tick += async (_, _) => { _debounce.Stop(); await BuscarAsync(agregarSiUnico: false); };
        _buscar.KeyDown += async (_, e) =>
        {
            switch (e.KeyCode)
            {
                case Keys.Enter:
                    e.SuppressKeyPress = true;
                    _debounce.Stop();
                    await BuscarAsync(agregarSiUnico: true);
                    break;
                case Keys.Down when _resultados.Visible && _resultados.Rows.Count > 0:
                    e.Handled = true;
                    _resultados.Focus();
                    break;
                case Keys.Escape:
                    LimpiarBusqueda();
                    break;
            }
        };
        _resultados.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                SeleccionarResultado();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                LimpiarBusqueda();
            }
        };
        _resultados.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) SeleccionarResultado(); };
        _gridCarrito.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete && !_gridCarrito.IsCurrentCellInEditMode)
            {
                e.Handled = true;
                QuitarLinea();
            }
        };
        KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.F2)
            {
                e.Handled = true;
                _buscar.Focus();
                _buscar.SelectAll();
            }
            else if (e.KeyCode == Keys.F4)
            {
                e.Handled = true;
                await CobrarAsync();
            }
        };
    }

    protected override async Task CargarAsync()
    {
        _caja = await _cajaApi.ObtenerActualAsync();
        (_lblCaja.Text, _lblCaja.ForeColor) = _caja is null
            ? ("Sin caja abierta: abra la caja para vender.", Theme.Peligro)
            : ($"Caja #{_caja.Id} abierta", Theme.Exito);
        _btnAbrirCaja.Visible = _caja is null && _sesion.Tiene(Permisos.CajaOperar);
        _btnCobrar.Enabled = _caja is not null;

        if (_sesion.Tiene(Permisos.ClientesVer, Permisos.CreditosOtorgar) && _cliente.Items.Count == 0)
        {
            var clientes = (await _clientes.ListarAsync()).Where(c => c.Estado == "A").OrderBy(c => c.Nombre).ToList();
            var combo = Controles.ComboBuscable(clientes.Cast<ClienteDto?>().Prepend(null), c => c?.Nombre ?? "Consumidor final");
            _cliente.DropDownStyle = combo.DropDownStyle;
            _cliente.AutoCompleteMode = combo.AutoCompleteMode;
            _cliente.AutoCompleteSource = combo.AutoCompleteSource;
            foreach (var item in combo.Items) _cliente.Items.Add(item);
            _cliente.SelectedIndex = 0;
            combo.Dispose();
        }

        ActualizarTotales();
        _buscar.Focus();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        // Al volver desde la pantalla de Caja (tras abrirla), refrescar el estado.
        if (_caja is null && IsHandleCreated) _ = RecargarAsync();
    }

    private async Task BuscarAsync(bool agregarSiUnico)
    {
        var termino = _buscar.Text.Trim();
        if (termino.Length == 0)
        {
            LimpiarBusqueda();
            return;
        }

        _busquedaCts?.Cancel();
        _busquedaCts = new CancellationTokenSource();
        var ct = _busquedaCts.Token;
        List<ProductoPosDto> resultados;
        try
        {
            resultados = await _productos.BuscarAsync(termino, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (ApiException ex)
        {
            if (agregarSiUnico) Dialogs.Excepcion(this, ex);
            return;
        }
        if (ct.IsCancellationRequested) return;

        // Lector de código de barras: coincidencia exacta de código -> se agrega con su presentación por defecto.
        var exacto = resultados.FirstOrDefault(p => string.Equals(p.CodigoBarras, termino, StringComparison.OrdinalIgnoreCase));
        if (agregarSiUnico && exacto is not null && TipoDefault(exacto) is { } tpExacto)
        {
            Agregar(exacto, tpExacto);
            return;
        }
        if (agregarSiUnico && resultados.Count == 1 && resultados[0].TiposPrecio.Count == 1)
        {
            Agregar(resultados[0], resultados[0].TiposPrecio[0]);
            return;
        }

        _resultados.DataSource = resultados;
        _resultados.Visible = resultados.Count > 0;
        if (agregarSiUnico && resultados.Count == 0)
            Dialogs.Info(this, $"No se encontró ningún producto activo para \"{termino}\".");
        else if (agregarSiUnico && resultados.Count > 0)
            _resultados.Focus();
    }

    private void SeleccionarResultado()
    {
        if (_resultados.Seleccionado<ProductoPosDto>() is not { } producto) return;
        var activos = producto.TiposPrecio.Where(t => t.Estado == "A").ToList();
        if (activos.Count == 0)
        {
            Dialogs.Aviso(this, "El producto no tiene presentaciones con precio activas.");
            return;
        }
        var tipo = activos.Count == 1 ? activos[0] : ElegirTipoPrecio(producto, activos);
        if (tipo is not null) Agregar(producto, tipo);
    }

    private TipoPrecioDto? ElegirTipoPrecio(ProductoPosDto producto, IReadOnlyList<TipoPrecioDto> tipos)
    {
        using var dlg = new EditDialog($"Presentación: {producto.Nombre}");
        var combo = dlg.AgregarCombo("Presentación", tipos,
            t => $"{t.Nombre} — {Formatters.Moneda(t.PrecioVenta)} ({Formatters.Cantidad(t.CantidadBase)} {producto.UnidadBase})",
            t => t.EsDefault);
        if (combo.SelectedIndex < 0) combo.SelectedIndex = 0;
        dlg.BotonAceptar.Text = "Agregar";
        return dlg.ShowDialog(this) == DialogResult.OK ? combo.Seleccion<TipoPrecioDto>() : null;
    }

    private void Agregar(ProductoPosDto producto, TipoPrecioDto tipo)
    {
        var existente = _carrito.FirstOrDefault(l => l.ProductoId == producto.ProductoId && l.TipoPrecioId == tipo.Id);
        if (existente is not null)
        {
            existente.Cantidad += 1;
        }
        else
        {
            _carrito.Add(new LineaCarrito
            {
                ProductoId = producto.ProductoId,
                ProductoNombre = producto.Nombre,
                TipoPrecioId = tipo.Id,
                TipoPrecioNombre = tipo.Nombre,
                CantidadBase = tipo.CantidadBase,
                PrecioUnitario = tipo.PrecioVenta,
                StockDisponible = producto.StockActual
            });
        }

        var linea = existente ?? _carrito[^1];
        if (StockTotalCarrito(producto.ProductoId) > producto.StockActual)
            System.Media.SystemSounds.Exclamation.Play();

        var fila = _carrito.IndexOf(linea);
        if (fila >= 0 && fila < _gridCarrito.Rows.Count)
            _gridCarrito.CurrentCell = _gridCarrito.Rows[fila].Cells[0];
        LimpiarBusqueda();
    }

    private decimal StockTotalCarrito(int productoId) =>
        _carrito.Where(l => l.ProductoId == productoId).Sum(l => l.CantidadEnBase);

    private void LimpiarBusqueda()
    {
        _debounce.Stop();
        _buscar.Clear();
        _resultados.DataSource = null;
        _resultados.Visible = false;
        _buscar.Focus();
    }

    private void QuitarLinea()
    {
        if (_gridCarrito.Seleccionado<LineaCarrito>() is { } linea) _carrito.Remove(linea);
        _buscar.Focus();
    }

    private void CancelarVenta()
    {
        if (_carrito.Count == 0 || !Dialogs.Confirmar(this, "¿Cancelar la venta actual y vaciar el carrito?")) return;
        Reiniciar();
    }

    private void Reiniciar()
    {
        _carrito.Clear();
        if (_cliente.Items.Count > 0) _cliente.SelectedIndex = 0;
        LimpiarBusqueda();
    }

    private decimal Total => _carrito.Sum(l => l.Subtotal);

    private void ActualizarTotales()
    {
        var subtotal = _carrito.Sum(l => l.PrecioUnitario * l.Cantidad);
        var descuento = _carrito.Sum(l => l.Descuento);
        _lblArticulos.Text = $"{_carrito.Count} líneas · {Formatters.Cantidad(_carrito.Sum(l => l.Cantidad))} artículos";
        _lblSubtotal.Text = $"Subtotal: {Formatters.Moneda(subtotal)}";
        _lblDescuento.Text = $"Descuentos: {Formatters.Moneda(descuento)}";
        _lblTotal.Text = Formatters.Moneda(subtotal - descuento);
        _gridCarrito.Invalidate();
    }

    private async Task CobrarAsync()
    {
        if (_gridCarrito.IsCurrentCellInEditMode) _gridCarrito.EndEdit();
        if (_caja is null)
        {
            Dialogs.Aviso(this, "Debe abrir la caja antes de vender.");
            return;
        }
        if (_carrito.Count == 0)
        {
            Dialogs.Info(this, "El carrito está vacío.");
            return;
        }

        var cliente = _cliente.Seleccion<ClienteDto?>();
        using var cobro = new CobroDialog(Total, cliente, _sesion.Tiene(Permisos.CreditosOtorgar));
        if (cobro.ShowDialog(this) != DialogResult.OK) return;

        await this.EjecutarAsync(async () =>
        {
            var venta = await _ventas.CrearAsync(new VentaCreateDto(
                cliente?.Id,
                _carrito.Select(l => new DetalleVentaCreateDto(l.ProductoId, l.TipoPrecioId, l.Cantidad, l.Descuento)).ToList(),
                cobro.Pagos,
                cobro.AlCredito,
                cobro.FechaVencimiento));

            var saldo = cobro.AlCredito ? venta.Total - venta.Pagos.Sum(p => p.Monto) : (decimal?)null;
            var resumen = $"Venta #{venta.Folio} registrada.\nTotal: {Formatters.Moneda(venta.Total)}" +
                          (cobro.Cambio > 0 ? $"\nCAMBIO: {Formatters.Moneda(cobro.Cambio)}" : "") +
                          (saldo > 0 ? $"\nA crédito: {Formatters.Moneda(saldo)}" : "") +
                          "\n\n¿Imprimir ticket?";
            Reiniciar();
            _notificaciones.Refrescar();
            if (Dialogs.Confirmar(this, resumen, "Venta registrada"))
                _printer.Imprimir(this, _printer.Construir(venta, cliente?.Nombre, saldo, cobro.EfectivoRecibido));
        });
        _buscar.Focus();
    }

    private static TipoPrecioDto? TipoDefault(ProductoPosDto p) =>
        p.TiposPrecio.FirstOrDefault(t => t.EsDefault && t.Estado == "A") ?? p.TiposPrecio.FirstOrDefault(t => t.Estado == "A");

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _debounce.Dispose();
            _busquedaCts?.Dispose();
        }
        base.Dispose(disposing);
    }
}
