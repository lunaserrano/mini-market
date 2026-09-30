using System.ComponentModel;

namespace MiniMarket.Desktop.Forms.Compras;

public sealed class ComprasForm : ListForm<CompraResumenDto>
{
    private readonly ComprasApi _compras;
    private readonly ProveedoresApi _proveedores;
    private readonly ProductosApi _productos;
    private readonly SessionService _sesion;
    private readonly NotificacionService _notificaciones;

    public ComprasForm(ComprasApi compras, ProveedoresApi proveedores, ProductosApi productos, SessionService sesion,
        NotificacionService notificaciones) : base("Compras")
    {
        _compras = compras;
        _proveedores = proveedores;
        _productos = productos;
        _sesion = sesion;
        _notificaciones = notificaciones;
        BtnNuevo.Text = "Registrar compra";
        BtnEditar.Text = "Ver detalle";
    }

    protected override bool PuedeCrear => _sesion.Tiene(Permisos.ComprasCrear);
    protected override bool PuedeEditar => true;
    protected override Color? ColorFila(CompraResumenDto item) => item.Estado == "ANULADA" ? Theme.FilaInactiva : null;

    protected override void ConfigurarColumnas(DataGridView grid)
    {
        grid.Col("#", nameof(CompraResumenDto.Id), 70);
        grid.Col("Fecha", nameof(CompraResumenDto.Fecha), 150, FormatoColumna.FechaUtc);
        grid.Col("Proveedor", nameof(CompraResumenDto.ProveedorNombre), 300, relleno: true);
        grid.Col("Total", nameof(CompraResumenDto.Total), 130, FormatoColumna.Moneda);
        grid.Col("Estado", nameof(CompraResumenDto.Estado), 110, FormatoColumna.Estado);
    }

    protected override async Task<IReadOnlyList<CompraResumenDto>> ObtenerAsync() =>
        (await _compras.ListarAsync(_sesion.SucursalId)).OrderByDescending(c => c.Fecha).ToList();

    protected override bool Coincide(CompraResumenDto i, string t) => i.Id.ToString() == t || Contiene(i.ProveedorNombre, t);

    protected override async Task NuevoAsync()
    {
        var proveedores = (await _proveedores.ListarAsync()).Where(p => p.Estado == "A").OrderBy(p => p.Nombre).ToList();
        var productos = (await _productos.ListarAsync()).Where(p => p.Estado == "A").OrderBy(p => p.Nombre).ToList();
        if (proveedores.Count == 0)
        {
            Dialogs.Aviso(this, "Registre al menos un proveedor activo antes de cargar compras.");
            return;
        }
        using var editor = new CompraEditForm(_compras, proveedores, productos);
        if (editor.ShowDialog(this) != DialogResult.OK) return;
        await CargarAsync();
        _notificaciones.Refrescar();
    }

    protected override async Task EditarAsync(CompraResumenDto item)
    {
        var compra = await _compras.ObtenerAsync(item.Id);
        using var dlg = new EditDialog($"Compra #{compra.Id} · {compra.ProveedorNombre}", 820);
        dlg.AgregarAncho(new Label
        {
            Text = $"Fecha {Formatters.Fecha(compra.Fecha)} · Doc. proveedor: {compra.NumeroDocumentoProveedor ?? "—"} · Estado: {Formatters.Estado(compra.Estado)}\n" +
                   $"Subtotal {Formatters.Moneda(compra.Subtotal)} · IVA {Formatters.Moneda(compra.ImpuestoTotal)} · TOTAL {Formatters.Moneda(compra.Total)}",
            AutoSize = true,
            Font = Theme.FuenteNegrita
        });
        var grid = new DataGridView().Estandar();
        grid.Dock = DockStyle.None;
        grid.Size = new Size(760, 300);
        grid.Col("Producto", nameof(DetalleCompraDto.ProductoNombre), 220, relleno: true);
        grid.Col("Presentación", nameof(DetalleCompraDto.TipoPrecioNombre), 120);
        grid.Col("Cantidad", nameof(DetalleCompraDto.Cantidad), 80, FormatoColumna.Cantidad);
        grid.Col("Unid. base", nameof(DetalleCompraDto.CantidadBaseCalculada), 90, FormatoColumna.Cantidad);
        grid.Col("Costo", nameof(DetalleCompraDto.CostoUnitario), 100, FormatoColumna.Moneda);
        grid.Col("Subtotal", nameof(DetalleCompraDto.Subtotal), 110, FormatoColumna.Moneda);
        grid.DataSource = compra.Detalles.ToList();
        dlg.AgregarAncho(grid);
        dlg.BotonCancelar.Text = "Cerrar";

        var puedeAnular = compra.Estado != "ANULADA" && _sesion.Tiene(Permisos.ComprasAnular);
        dlg.BotonAceptar.Visible = puedeAnular;
        dlg.BotonAceptar.Text = "Anular compra";
        dlg.BotonAceptar.BackColor = Theme.Peligro;
        dlg.AlGuardar(async () =>
        {
            if (!Dialogs.Confirmar(dlg, "¿Anular esta compra? Se revertirá el ingreso de mercadería al inventario."))
                throw new OperationCanceledException();
            await _compras.AnularAsync(compra.Id);
        });
        dlg.AcceptButton = null;
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        await CargarAsync();
        _notificaciones.Refrescar();
    }
}

/// <summary>Registro de una compra a proveedor: ingresa mercadería al inventario de la sucursal.</summary>
public sealed class CompraEditForm : EditDialog
{
    [System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)] // enlazado a grillas por nombre de propiedad
    private sealed class LineaCompra
    {
        public required int ProductoId { get; init; }
        public required string ProductoNombre { get; init; }
        public required int TipoPrecioId { get; init; }
        public required string TipoPrecioNombre { get; init; }
        public required decimal Cantidad { get; init; }
        public required decimal CostoUnidadMedida { get; init; }
        public decimal Subtotal => Cantidad * CostoUnidadMedida;
    }

    private readonly BindingList<LineaCompra> _lineas = new();
    private readonly Label _total = new() { AutoSize = true, Font = Theme.FuenteNegrita };

    public CompraEditForm(ComprasApi api, IReadOnlyList<ProveedorDto> proveedores, IReadOnlyList<ProductoDto> productos)
        : base("Registrar compra", 860)
    {
        var proveedor = Agregar("Proveedor *", Controles.ComboBuscable(proveedores, p => p.Nombre));
        var documento = AgregarTexto("N.º factura proveedor", "", maxLength: 50);

        var grid = new DataGridView().Estandar();
        grid.Dock = DockStyle.None;
        grid.Size = new Size(800, 280);
        grid.Col("Producto", nameof(LineaCompra.ProductoNombre), 250, relleno: true);
        grid.Col("Presentación", nameof(LineaCompra.TipoPrecioNombre), 140);
        grid.Col("Cantidad", nameof(LineaCompra.Cantidad), 90, FormatoColumna.Cantidad);
        grid.Col("Costo x presentación", nameof(LineaCompra.CostoUnidadMedida), 140, FormatoColumna.Moneda);
        grid.Col("Subtotal", nameof(LineaCompra.Subtotal), 110, FormatoColumna.Moneda);
        grid.DataSource = _lineas;
        _lineas.ListChanged += (_, _) => _total.Text = $"{_lineas.Count} líneas · Total (sin IVA calculado por el sistema): {Formatters.Moneda(_lineas.Sum(l => l.Subtotal))}";

        AgregarAncho(Controles.Barra(
            Theme.Boton("+ Agregar producto", (_, _) => { if (PedirLinea(productos) is { } l) _lineas.Add(l); }, primario: true),
            Theme.Boton("Quitar línea", (_, _) => { if (grid.Seleccionado<LineaCompra>() is { } l) _lineas.Remove(l); }),
            _total));
        AgregarAncho(grid);
        _total.Text = "Sin líneas";

        Validar(() => proveedor.Seleccion<ProveedorDto>() is null ? "Seleccione un proveedor de la lista." : null);
        Validar(() => _lineas.Count == 0 ? "Agregue al menos un producto." : null);

        BotonAceptar.Text = "Registrar compra";
        AlGuardar(async () =>
        {
            await api.CrearAsync(new CompraCreateDto(
                proveedor.Seleccion<ProveedorDto>()!.Id,
                documento.TextoONull(),
                _lineas.Select(l => new DetalleCompraCreateDto(l.ProductoId, l.TipoPrecioId, l.Cantidad, l.CostoUnidadMedida)).ToList()));
            Dialogs.Info(this, "Compra registrada. El inventario fue actualizado.");
        });
    }

    private LineaCompra? PedirLinea(IReadOnlyList<ProductoDto> productos)
    {
        using var dlg = new EditDialog("Agregar producto a la compra");
        var producto = dlg.Agregar("Producto *", Controles.ComboBuscable(productos, p => string.IsNullOrEmpty(p.CodigoBarras) ? p.Nombre : $"{p.Nombre} [{p.CodigoBarras}]"));
        var tipo = dlg.Agregar("Presentación *", new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList });
        var cantidad = dlg.AgregarNumero("Cantidad *", 1, decimales: 3, minimo: 0.001m);
        var costo = dlg.AgregarMonto("Costo por presentación *");
        var ayuda = dlg.AgregarAncho(new Label { AutoSize = true, ForeColor = Theme.TextoSuave, MaximumSize = new Size(440, 0) });

        producto.SelectedIndexChanged += (_, _) =>
        {
            tipo.Items.Clear();
            if (producto.Seleccion<ProductoDto>() is not { } p) return;
            foreach (var t in p.TiposPrecio.Where(t => t.Estado == "A"))
                tipo.Items.Add(new Opcion<TipoPrecioDto>(t, $"{t.Nombre} ({Formatters.Cantidad(t.CantidadBase)} {p.UnidadBase})"));
            tipo.Seleccionar<TipoPrecioDto>(t => t.EsDefault);
            if (tipo.SelectedIndex < 0 && tipo.Items.Count > 0) tipo.SelectedIndex = 0;
        };
        tipo.SelectedIndexChanged += (_, _) =>
        {
            if (tipo.Seleccion<TipoPrecioDto>() is not { } t) return;
            costo.Value = t.PrecioCompra ?? 0;
            ayuda.Text = $"Precio de la presentación completa (ej. la caja entera). Ingresarán {Formatters.Cantidad(t.CantidadBase)} unidades base por cada una.";
        };

        dlg.Validar(() => producto.Seleccion<ProductoDto>() is null ? "Seleccione un producto de la lista." : null);
        dlg.Validar(() => tipo.Seleccion<TipoPrecioDto>() is null ? "El producto no tiene presentaciones activas." : null);
        dlg.Validar(() => costo.Value <= 0 ? "El costo debe ser mayor a cero." : null);
        dlg.BotonAceptar.Text = "Agregar";
        if (dlg.ShowDialog(this) != DialogResult.OK) return null;

        var prod = producto.Seleccion<ProductoDto>()!;
        var tp = tipo.Seleccion<TipoPrecioDto>()!;
        return new LineaCompra
        {
            ProductoId = prod.Id,
            ProductoNombre = prod.Nombre,
            TipoPrecioId = tp.Id,
            TipoPrecioNombre = tp.Nombre,
            Cantidad = cantidad.Value,
            CostoUnidadMedida = costo.Value
        };
    }
}
