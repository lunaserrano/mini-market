namespace MiniMarket.Desktop.Forms.Inventario;

/// <summary>Existencias de la sucursal del usuario, con alerta de stock mínimo, ajustes manuales y configuración del mínimo.</summary>
public sealed class InventarioForm : ListForm<InventarioDto>
{
    private readonly InventarioApi _api;
    private readonly SessionService _sesion;
    private readonly NotificacionService _notificaciones;
    private readonly CheckBox _soloBajos = new() { Text = "Solo bajo mínimo", AutoSize = true, Margin = new Padding(12, 8, 4, 4) };

    public InventarioForm(InventarioApi api, SessionService sesion, NotificacionService notificaciones) : base("Existencias")
    {
        _api = api;
        _sesion = sesion;
        _notificaciones = notificaciones;

        BtnEditar.Text = "Ajustar stock / mínimo";
        var movimientos = Theme.Boton("Ver movimientos", (_, _) =>
        {
            var form = (MdiParent as MainForm)?.Abrir<MovimientosInventarioForm>();
            if (form is not null && Grid.Seleccionado<InventarioDto>() is { } item) form.FiltrarProducto(item.ProductoId);
        });
        Barra.Controls.Add(movimientos);
        Barra.Controls.Add(_soloBajos);
        Barra.Controls.SetChildIndex(movimientos, 3);
        _soloBajos.CheckedChanged += (_, _) => AplicarFiltro();
    }

    public override string Ruta => "inventario";

    /// <summary>Desde la campanita: muestra solo lo que está bajo el mínimo, opcionalmente filtrado por producto.</summary>
    public void MostrarBajoMinimo(string? producto = null)
    {
        _soloBajos.Checked = true;
        Buscar.Text = producto ?? "";
    }

    protected override bool PuedeEditar => _sesion.Tiene(Permisos.InventarioAjustar);
    protected override Color? ColorFila(InventarioDto item) => item.BajoMinimo ? Theme.FilaAlerta : null;

    protected override void ConfigurarColumnas(DataGridView grid)
    {
        grid.Col("Producto", nameof(InventarioDto.ProductoNombre), 300, relleno: true);
        grid.Col("Stock actual", nameof(InventarioDto.StockActual), 120, FormatoColumna.Cantidad);
        grid.Col("Stock mínimo", nameof(InventarioDto.StockMinimo), 120, FormatoColumna.Cantidad);
        grid.Col("Bajo mínimo", nameof(InventarioDto.BajoMinimo), 100, FormatoColumna.SiNo);
    }

    protected override async Task<IReadOnlyList<InventarioDto>> ObtenerAsync() =>
        await _api.ListarAsync(sucursalId: _sesion.SucursalId);

    protected override bool Coincide(InventarioDto i, string t) => Contiene(i.ProductoNombre, t);

    protected override Task EditarAsync(InventarioDto item) => AjustarAsync(item);

    // El filtro "solo bajo mínimo" se integra a la búsqueda de texto de la plantilla.
    protected override bool EsInactivo(InventarioDto item) => _soloBajos.Checked && !item.BajoMinimo;
    protected override bool TieneEstado => _soloBajos.Checked;

    /// <summary>
    /// Ajuste de cantidad y/o cambio del stock mínimo (al llegar a él aparece la alerta en la campanita).
    /// Se puede guardar solo uno de los dos: el motivo es obligatorio únicamente si cambia la cantidad.
    /// </summary>
    private async Task AjustarAsync(InventarioDto item)
    {
        using var dlg = new EditDialog($"Ajuste de inventario: {item.ProductoNombre}");
        dlg.AgregarAncho(new Label { Text = $"Stock actual: {Formatters.Cantidad(item.StockActual)}", Font = Theme.FuenteNegrita, AutoSize = true });
        var cantidad = dlg.AgregarNumero("Cantidad (+ entra / − sale)", 0, decimales: 3, minimo: -1_000_000, maximo: 1_000_000);
        var resultado = dlg.AgregarAncho(new Label { AutoSize = true, ForeColor = Theme.TextoSuave });
        var observacion = dlg.AgregarTexto("Motivo (si ajusta la cantidad)", "", multilinea: true, maxLength: 250);
        var minimo = dlg.AgregarNumero("Stock mínimo", item.StockMinimo, decimales: 3, minimo: 0, maximo: 1_000_000);
        dlg.AgregarAncho(new Label
        {
            Text = "Al llegar a este stock se avisa en la campanita de notificaciones. 0 = sin alerta.",
            AutoSize = true,
            MaximumSize = new Size(440, 0),
            ForeColor = Theme.TextoSuave
        });
        cantidad.ValueChanged += (_, _) => resultado.Text = $"Stock resultante: {Formatters.Cantidad(item.StockActual + cantidad.Value)}";

        bool CambiaMinimo() => minimo.Value != item.StockMinimo;
        dlg.Validar(() => cantidad.Value == 0 && !CambiaMinimo() ? "Indique una cantidad a ajustar o un nuevo stock mínimo." : null);
        dlg.Validar(() => cantidad.Value != 0 && string.IsNullOrWhiteSpace(observacion.Text) ? "Indique el motivo del ajuste." : null);
        dlg.Validar(() => item.StockActual + cantidad.Value < 0 ? "El ajuste dejaría el stock en negativo." : null);
        var minimoGuardado = false;
        dlg.AlGuardar(async () =>
        {
            if (CambiaMinimo())
            {
                await _api.ActualizarStockMinimoAsync(new StockMinimoRequest(item.ProductoId, item.SucursalId, minimo.Value));
                minimoGuardado = true;
            }
            if (cantidad.Value != 0)
                await _api.AjustarAsync(new AjusteInventarioRequest(item.ProductoId, item.SucursalId, cantidad.Value, observacion.Text.Trim()));
        });
        // minimoGuardado cubre el caso "se guardó el mínimo, falló el ajuste y se canceló el diálogo".
        if (dlg.ShowDialog(this) != DialogResult.OK && !minimoGuardado) return;
        await CargarAsync();
        _notificaciones.Refrescar();
    }
}

/// <summary>Kardex: movimientos de inventario filtrables por producto y rango de fechas.</summary>
public sealed class MovimientosInventarioForm : ListForm<MovimientoInventarioDto>
{
    private readonly InventarioApi _api;
    private readonly SessionService _sesion;
    private readonly ComboBox _producto = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly DateTimePicker _desde = new() { Format = DateTimePickerFormat.Short, Width = 120 };
    private readonly DateTimePicker _hasta = new() { Format = DateTimePickerFormat.Short, Width = 120 };
    private int? _productoPendiente;

    public MovimientosInventarioForm(InventarioApi api, SessionService sesion) : base("Movimientos de inventario")
    {
        _api = api;
        _sesion = sesion;
        _desde.Value = DateTime.Today.AddDays(-30);
        _hasta.Value = DateTime.Today;

        var buscar = Theme.Boton("Consultar", async (_, _) => await RecargarAsync(), primario: true);
        Barra.Controls.Clear();
        Barra.Controls.AddRange(new Control[]
        {
            Controles.Etiqueta("Producto"), _producto,
            Controles.Etiqueta("Desde"), _desde,
            Controles.Etiqueta("Hasta"), _hasta,
            buscar,
            Theme.Boton("Exportar CSV", (_, _) => CsvExporter.Exportar(this, Grid, Text)),
            Theme.Boton("Imprimir", (_, _) => GridPrinter.Imprimir(this, Grid, Text, $"Del {_desde.Value:dd/MM/yyyy} al {_hasta.Value:dd/MM/yyyy}")),
            Buscar
        });
    }

    public void FiltrarProducto(int productoId)
    {
        _productoPendiente = productoId;
        if (_producto.Items.Count > 0)
        {
            _producto.Seleccionar<InventarioDto?>(i => i?.ProductoId == productoId);
            _ = RecargarAsync();
        }
    }

    protected override void ConfigurarColumnas(DataGridView grid)
    {
        grid.Col("Fecha", nameof(MovimientoInventarioDto.FechaMovimiento), 140, FormatoColumna.FechaUtc);
        grid.Col("Producto", nameof(MovimientoInventarioDto.ProductoNombre), 220, relleno: true);
        grid.Col("Tipo", nameof(MovimientoInventarioDto.TipoMovimiento), 150);
        grid.Col("Cantidad", nameof(MovimientoInventarioDto.Cantidad), 100, FormatoColumna.Cantidad);
        grid.Col("Stock resultante", nameof(MovimientoInventarioDto.StockResultante), 120, FormatoColumna.Cantidad);
        grid.Col("Documento", nameof(MovimientoInventarioDto.DocumentoOrigenTipo), 110);
        grid.Col("N.º doc.", nameof(MovimientoInventarioDto.DocumentoOrigenId), 80);
        grid.Col("Observación", nameof(MovimientoInventarioDto.Observacion), 220);
    }

    protected override async Task<IReadOnlyList<MovimientoInventarioDto>> ObtenerAsync()
    {
        if (_producto.Items.Count == 0)
        {
            var productos = await _api.ListarAsync(sucursalId: _sesion.SucursalId);
            _producto.Items.Add(new Opcion<InventarioDto?>(null, "(todos)"));
            foreach (var p in productos.OrderBy(p => p.ProductoNombre)) _producto.Items.Add(new Opcion<InventarioDto?>(p, p.ProductoNombre));
            _producto.SelectedIndex = 0;
            if (_productoPendiente is { } id) _producto.Seleccionar<InventarioDto?>(i => i?.ProductoId == id);
        }

        // Rango de días completos en la zona horaria de la empresa, enviado en UTC.
        var desde = Formatters.AUtc(_desde.Value.Date);
        var hasta = Formatters.AUtc(_hasta.Value.Date.AddDays(1).AddTicks(-1));
        var lista = await _api.MovimientosAsync(_producto.Seleccion<InventarioDto?>()?.ProductoId, _sesion.SucursalId, desde, hasta);
        return lista.OrderByDescending(m => m.FechaMovimiento).ToList();
    }

    protected override bool Coincide(MovimientoInventarioDto i, string t) =>
        Contiene(i.ProductoNombre, t) || Contiene(i.TipoMovimiento, t) || Contiene(i.Observacion, t);
}
