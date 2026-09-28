namespace MiniMarket.Desktop.Forms.Productos;

public sealed class ProductosForm : ListForm<ProductoDto>
{
    private readonly ProductosApi _productos;
    private readonly CategoriasApi _categorias;
    private readonly ProveedoresApi _proveedores;
    private readonly SessionService _sesion;

    public ProductosForm(ProductosApi productos, CategoriasApi categorias, ProveedoresApi proveedores, SessionService sesion)
        : base("Productos")
    {
        _productos = productos;
        _categorias = categorias;
        _proveedores = proveedores;
        _sesion = sesion;
    }

    protected override bool PuedeCrear => _sesion.Tiene(Permisos.ProductosGestionar);
    // "Editar" también sirve para consultar el detalle (precios) a quien solo puede ver.
    protected override bool PuedeEditar => true;
    protected override bool PuedeDesactivar => _sesion.Tiene(Permisos.ProductosEliminar);
    protected override bool TieneEstado => true;
    protected override bool EsInactivo(ProductoDto item) => item.Estado != "A";

    protected override void ConfigurarColumnas(DataGridView grid)
    {
        grid.Col("Nombre", nameof(ProductoDto.Nombre), 250, relleno: true);
        grid.Col("Categoría", nameof(ProductoDto.CategoriaNombre), 150);
        grid.Col("Código de barras", nameof(ProductoDto.CodigoBarras), 150);
        grid.Col("Código interno", nameof(ProductoDto.CodigoInterno), 110);
        grid.Col("Unidad", nameof(ProductoDto.UnidadBase), 90);
        grid.Col("Precio", nameof(ProductoDto.PrecioDefault), 100, FormatoColumna.Moneda);
        grid.Col("Estado", nameof(ProductoDto.Estado), 90, FormatoColumna.Estado);
        if (!PuedeCrear) BtnEditar.Text = "Ver detalle";
    }

    protected override async Task<IReadOnlyList<ProductoDto>> ObtenerAsync() => await _productos.ListarAsync();

    protected override bool Coincide(ProductoDto i, string t) =>
        Contiene(i.Nombre, t) || Contiene(i.CodigoBarras, t) || Contiene(i.CodigoInterno, t) || Contiene(i.CategoriaNombre, t);

    protected override Task NuevoAsync() => AbrirEditorAsync(null);
    protected override Task EditarAsync(ProductoDto item) => AbrirEditorAsync(item);

    private async Task AbrirEditorAsync(ProductoDto? producto)
    {
        var categorias = (await _categorias.ListarAsync()).Where(c => c.Estado == "A" || c.Id == producto?.CategoriaId).ToList();
        var proveedores = _sesion.Tiene(Permisos.ProveedoresVer)
            ? (await _proveedores.ListarAsync()).Where(p => p.Estado == "A" || p.Id == producto?.ProveedorId).ToList()
            : new List<ProveedorDto>();
        if (producto is not null) producto = await _productos.ObtenerAsync(producto.Id);

        using var editor = new ProductoEditForm(_productos, _sesion, categorias, proveedores, producto);
        editor.ShowDialog(this);
        if (editor.HuboCambios) await CargarAsync();
    }

    protected override async Task DesactivarAsync(ProductoDto item)
    {
        if (!Dialogs.Confirmar(this, $"¿Desactivar el producto \"{item.Nombre}\"?\nDejará de aparecer en el punto de venta.")) return;
        await _productos.DesactivarAsync(item.Id);
        await CargarAsync();
    }
}
