namespace MiniMarket.Desktop.Forms.Productos;

/// <summary>
/// Alta/edición de producto con sus tipos de precio (presentaciones: unidad, six-pack, caja...).
/// Alta: el producto y sus presentaciones se envían juntos (POST productos). Edición: los datos del
/// producto se guardan con PUT y cada presentación se agrega/edita/elimina al momento con su propio
/// endpoint (igual que la web).
/// </summary>
public sealed class ProductoEditForm : EditDialog
{
    private static readonly string[] UnidadesSugeridas = { "unidad", "libra", "kilogramo", "gramo", "litro", "onza", "galón", "arroba", "quintal" };

    private readonly ProductosApi _api;
    private ProductoDto? _producto;
    private readonly List<TipoPrecioSaveDto> _tiposNuevos = new();
    private readonly DataGridView _gridTipos = new DataGridView().Estandar();

    public bool HuboCambios { get; private set; }

    public ProductoEditForm(ProductosApi api, SessionService sesion, IReadOnlyList<CategoriaDto> categorias,
        IReadOnlyList<ProveedorDto> proveedores, ProductoDto? producto)
        : base(producto is null ? "Nuevo producto" : $"Producto: {producto.Nombre}", 760)
    {
        _api = api;
        _producto = producto;
        var editable = sesion.Tiene(Permisos.ProductosGestionar);

        var nombre = AgregarTexto("Nombre *", producto?.Nombre, maxLength: 150);
        var categoria = AgregarCombo("Categoría *", categorias, c => c.Nombre, c => c.Id == producto?.CategoriaId);
        var proveedor = AgregarCombo("Proveedor", proveedores.Prepend(null!), p => p?.Nombre ?? "(ninguno)", p => p?.Id == producto?.ProveedorId);
        if (producto?.ProveedorId is null) proveedor.SelectedIndex = 0;
        var codigoBarras = AgregarTexto("Código de barras", producto?.CodigoBarras, maxLength: 50);
        var codigoInterno = AgregarTexto("Código interno", producto?.CodigoInterno, maxLength: 50);
        var unidad = Agregar("Unidad base *", new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Text = producto?.UnidadBase ?? "unidad" });
        unidad.Items.AddRange(UnidadesSugeridas);
        var descripcion = AgregarTexto("Descripción", producto?.Descripcion, multilinea: true, maxLength: 500);

        // --- Tipos de precio ---
        AgregarAncho(new Label
        {
            Text = $"Presentaciones y precios (precio de venta con IVA {Formatters.TasaImpuesto:0.##}% incluido)",
            Font = Theme.FuenteNegrita,
            AutoSize = true,
            Margin = new Padding(4, 12, 4, 4)
        });
        _gridTipos.Dock = DockStyle.None;
        _gridTipos.Height = 170;
        _gridTipos.Width = 700;
        _gridTipos.Col("Presentación", "Nombre", 160, relleno: true);
        _gridTipos.Col("Cant. base", "CantidadBase", 90, FormatoColumna.Cantidad);
        _gridTipos.Col("Sin IVA", "PrecioSinIva", 95, FormatoColumna.Moneda);
        _gridTipos.Col("Precio venta", "PrecioVenta", 105, FormatoColumna.Moneda);
        _gridTipos.Col("Costo", "PrecioCompra", 95, FormatoColumna.Moneda);
        _gridTipos.Col("Default", "EsDefault", 70, FormatoColumna.SiNo);
        AgregarAncho(_gridTipos);

        var btnAgregar = Theme.Boton("Agregar presentación", async (_, _) => await this.EjecutarAsync(AgregarTipoAsync));
        var btnEditar = Theme.Boton("Editar", async (_, _) => await this.EjecutarAsync(EditarTipoAsync));
        var btnEliminar = Theme.Boton("Eliminar", async (_, _) => await this.EjecutarAsync(EliminarTipoAsync), peligro: true);
        var barra = new FlowLayoutPanel { AutoSize = true, Controls = { btnAgregar, btnEditar, btnEliminar } };
        AgregarAncho(barra);
        _gridTipos.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0 && editable) await this.EjecutarAsync(EditarTipoAsync); };

        if (producto is null)
            _tiposNuevos.Add(new TipoPrecioSaveDto("Unidad", 1, 0, 0, true));
        RefrescarTipos();

        if (!editable)
        {
            BotonAceptar.Visible = false;
            BotonCancelar.Text = "Cerrar";
            barra.Visible = false;
            foreach (var c in new Control[] { nombre, categoria, proveedor, codigoBarras, codigoInterno, unidad, descripcion }) c.Enabled = false;
            return;
        }

        Validar(() => string.IsNullOrWhiteSpace(nombre.Text) ? "El nombre es obligatorio." : null);
        Validar(() => categoria.Seleccion<CategoriaDto>() is null ? "Seleccione una categoría." : null);
        Validar(() => string.IsNullOrWhiteSpace(unidad.Text) ? "La unidad base es obligatoria." : null);
        Validar(() => _producto is null && _tiposNuevos.Count(t => t.EsDefault) != 1 ? "Debe marcar exactamente una presentación como default." : null);
        Validar(() => _producto is null && _tiposNuevos.Any(t => t.PrecioVenta <= 0) ? "Todas las presentaciones deben tener precio de venta mayor a cero." : null);

        AlGuardar(async () =>
        {
            var categoriaId = categoria.Seleccion<CategoriaDto>()!.Id;
            var proveedorId = proveedor.Seleccion<ProveedorDto?>()?.Id;
            if (_producto is null)
            {
                await _api.CrearAsync(new ProductoCreateDto(categoriaId, proveedorId, nombre.Text.Trim(), descripcion.TextoONull(),
                    codigoBarras.TextoONull(), codigoInterno.TextoONull(), null, unidad.Text.Trim(), _tiposNuevos));
            }
            else
            {
                await _api.ActualizarAsync(_producto.Id, new ProductoUpdateDto(categoriaId, proveedorId, nombre.Text.Trim(), descripcion.TextoONull(),
                    codigoBarras.TextoONull(), codigoInterno.TextoONull(), _producto.ImagenPath, unidad.Text.Trim()));
            }
            HuboCambios = true;
        });
    }

    /// <summary>Fila de la grilla de presentaciones: el precio sin IVA es solo referencia visual.</summary>
    [System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)] // enlazado a grillas por nombre de propiedad
    private sealed record FilaTipo(int? Id, string Nombre, decimal CantidadBase, decimal PrecioVenta, decimal? PrecioCompra, bool EsDefault)
    {
        public decimal PrecioSinIva => Formatters.SinImpuesto(PrecioVenta);
    }

    private void RefrescarTipos()
    {
        _gridTipos.DataSource = _producto is null
            ? _tiposNuevos.Select(t => new FilaTipo(null, t.Nombre, t.CantidadBase, t.PrecioVenta, t.PrecioCompra, t.EsDefault)).ToList()
            : _producto.TiposPrecio.Where(t => t.Estado == "A")
                .Select(t => new FilaTipo(t.Id, t.Nombre, t.CantidadBase, t.PrecioVenta, t.PrecioCompra, t.EsDefault)).ToList();
    }

    private async Task RecargarProductoAsync()
    {
        _producto = await _api.ObtenerAsync(_producto!.Id);
        HuboCambios = true;
        RefrescarTipos();
    }

    private async Task AgregarTipoAsync()
    {
        var dto = TipoPrecioDialog.Mostrar(this, null, primero: _producto is null ? _tiposNuevos.Count == 0 : false);
        if (dto is null) return;

        if (_producto is null)
        {
            if (dto.EsDefault) DesmarcarDefaultNuevos();
            _tiposNuevos.Add(dto);
            RefrescarTipos();
            return;
        }
        await _api.AgregarTipoPrecioAsync(_producto.Id, dto);
        await RecargarProductoAsync();
    }

    private async Task EditarTipoAsync()
    {
        if (_gridTipos.Seleccionado<FilaTipo>() is not { } fila)
        {
            Dialogs.Info(this, "Seleccione una presentación.");
            return;
        }
        var actual = new TipoPrecioSaveDto(fila.Nombre, fila.CantidadBase, fila.PrecioVenta, fila.PrecioCompra, fila.EsDefault);
        var dto = TipoPrecioDialog.Mostrar(this, actual, primero: false);
        if (dto is null) return;

        if (_producto is null)
        {
            var indice = _gridTipos.CurrentRow!.Index;
            if (dto.EsDefault) DesmarcarDefaultNuevos();
            _tiposNuevos[indice] = dto;
            RefrescarTipos();
            return;
        }
        await _api.ActualizarTipoPrecioAsync(_producto.Id, fila.Id!.Value, dto);
        await RecargarProductoAsync();
    }

    private async Task EliminarTipoAsync()
    {
        if (_gridTipos.Seleccionado<FilaTipo>() is not { } fila) return;
        if (!Dialogs.Confirmar(this, $"¿Eliminar la presentación \"{fila.Nombre}\"?")) return;

        if (_producto is null)
        {
            _tiposNuevos.RemoveAt(_gridTipos.CurrentRow!.Index);
            RefrescarTipos();
            return;
        }
        await _api.EliminarTipoPrecioAsync(_producto.Id, fila.Id!.Value);
        await RecargarProductoAsync();
    }

    private void DesmarcarDefaultNuevos()
    {
        for (var i = 0; i < _tiposNuevos.Count; i++)
            _tiposNuevos[i] = _tiposNuevos[i] with { EsDefault = false };
    }
}

/// <summary>
/// Presentación de precio. El precio con y sin IVA se recalculan entre sí en vivo (se guarda solo el
/// precio de venta final con IVA incluido, igual que la web).
/// </summary>
public static class TipoPrecioDialog
{
    public static TipoPrecioSaveDto? Mostrar(IWin32Window owner, TipoPrecioSaveDto? actual, bool primero)
    {
        using var dlg = new EditDialog(actual is null ? "Nueva presentación" : "Editar presentación");
        var nombre = dlg.AgregarTexto("Nombre *", actual?.Nombre ?? "", maxLength: 50);
        var cantidad = dlg.AgregarNumero("Cantidad base *", actual?.CantidadBase ?? 1, decimales: 3, minimo: 0.001m);
        var sinIva = dlg.AgregarMonto("Precio sin IVA", Formatters.SinImpuesto(actual?.PrecioVenta ?? 0));
        var conIva = dlg.AgregarMonto($"Precio venta (IVA {Formatters.TasaImpuesto:0.##}% incl.) *", actual?.PrecioVenta ?? 0);
        var compra = dlg.AgregarMonto("Precio de compra", actual?.PrecioCompra ?? 0);
        var esDefault = dlg.AgregarCheck("Presentación por defecto en el POS", actual?.EsDefault ?? primero);
        dlg.AgregarAncho(new Label
        {
            Text = "Cantidad base: cuántas unidades base descuenta del inventario (ej. six-pack = 6).",
            ForeColor = Theme.TextoSuave,
            AutoSize = true,
            MaximumSize = new Size(440, 0)
        });

        var sincronizando = false;
        sinIva.ValueChanged += (_, _) =>
        {
            if (sincronizando) return;
            sincronizando = true;
            conIva.Value = Math.Min(conIva.Maximum, Formatters.ConImpuesto(sinIva.Value));
            sincronizando = false;
        };
        conIva.ValueChanged += (_, _) =>
        {
            if (sincronizando) return;
            sincronizando = true;
            sinIva.Value = Formatters.SinImpuesto(conIva.Value);
            sincronizando = false;
        };

        dlg.Validar(() => string.IsNullOrWhiteSpace(nombre.Text) ? "El nombre es obligatorio." : null);
        dlg.Validar(() => conIva.Value <= 0 ? "El precio de venta debe ser mayor a cero." : null);
        dlg.BotonAceptar.Text = "Aceptar";

        return dlg.ShowDialog(owner) == DialogResult.OK
            ? new TipoPrecioSaveDto(nombre.Text.Trim(), cantidad.Value, conIva.Value, compra.Value == 0 ? null : compra.Value, esDefault.Checked)
            : null;
    }
}
