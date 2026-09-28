namespace MiniMarket.Desktop.Forms.Catalogos;

// Mantenimientos de catálogo simple sobre ListForm: solo columnas, filtro y diálogos de edición.
// "Desactivar" es un soft delete (Estado = 'I') en la Api; los inactivos se ven con "Mostrar inactivos".

public sealed class CategoriasForm : ListForm<CategoriaDto>
{
    private readonly CategoriasApi _api;
    private readonly SessionService _sesion;

    public CategoriasForm(CategoriasApi api, SessionService sesion) : base("Categorías")
    {
        _api = api;
        _sesion = sesion;
    }

    protected override bool PuedeCrear => _sesion.Tiene(Permisos.CategoriasGestionar);
    protected override bool PuedeEditar => _sesion.Tiene(Permisos.CategoriasGestionar);
    protected override bool PuedeDesactivar => _sesion.Tiene(Permisos.CategoriasEliminar);
    protected override bool TieneEstado => true;
    protected override bool EsInactivo(CategoriaDto item) => item.Estado != "A";

    protected override void ConfigurarColumnas(DataGridView grid)
    {
        grid.Col("Nombre", nameof(CategoriaDto.Nombre), 250);
        grid.Col("Descripción", nameof(CategoriaDto.Descripcion), 300, relleno: true);
        grid.Col("Estado", nameof(CategoriaDto.Estado), 100, FormatoColumna.Estado);
    }

    protected override async Task<IReadOnlyList<CategoriaDto>> ObtenerAsync() => await _api.ListarAsync();

    protected override bool Coincide(CategoriaDto i, string t) => Contiene(i.Nombre, t) || Contiene(i.Descripcion, t);

    protected override Task NuevoAsync() => AbrirEditorAsync(null);
    protected override Task EditarAsync(CategoriaDto item) => AbrirEditorAsync(item);

    private async Task AbrirEditorAsync(CategoriaDto? item)
    {
        using var dlg = new EditDialog(item is null ? "Nueva categoría" : "Editar categoría");
        var nombre = dlg.AgregarTexto("Nombre *", item?.Nombre, maxLength: 100);
        var descripcion = dlg.AgregarTexto("Descripción", item?.Descripcion, multilinea: true, maxLength: 250);
        dlg.Validar(() => string.IsNullOrWhiteSpace(nombre.Text) ? "El nombre es obligatorio." : null);
        dlg.AlGuardar(async () =>
        {
            var dto = new CategoriaSaveDto(nombre.Text.Trim(), descripcion.TextoONull());
            if (item is null) await _api.CrearAsync(dto);
            else await _api.ActualizarAsync(item.Id, dto);
        });
        if (dlg.ShowDialog(this) == DialogResult.OK) await CargarAsync();
    }

    protected override async Task DesactivarAsync(CategoriaDto item)
    {
        if (!Dialogs.Confirmar(this, $"¿Desactivar la categoría \"{item.Nombre}\"?")) return;
        await _api.DesactivarAsync(item.Id);
        await CargarAsync();
    }
}

public sealed class ClientesForm : ListForm<ClienteDto>
{
    private readonly ClientesApi _api;
    private readonly SessionService _sesion;

    public ClientesForm(ClientesApi api, SessionService sesion) : base("Clientes")
    {
        _api = api;
        _sesion = sesion;
    }

    protected override bool PuedeCrear => _sesion.Tiene(Permisos.ClientesGestionar);
    protected override bool PuedeEditar => _sesion.Tiene(Permisos.ClientesGestionar);
    protected override bool PuedeDesactivar => _sesion.Tiene(Permisos.ClientesEliminar);
    protected override bool TieneEstado => true;
    protected override bool EsInactivo(ClienteDto item) => item.Estado != "A";

    protected override void ConfigurarColumnas(DataGridView grid)
    {
        grid.Col("Nombre", nameof(ClienteDto.Nombre), 250, relleno: true);
        grid.Col("NIT / DUI", nameof(ClienteDto.IdentificacionFiscal), 140);
        grid.Col("Teléfono", nameof(ClienteDto.Telefono), 120);
        grid.Col("Email", nameof(ClienteDto.Email), 200);
        grid.Col("Dirección", nameof(ClienteDto.Direccion), 250);
        grid.Col("Estado", nameof(ClienteDto.Estado), 90, FormatoColumna.Estado);
    }

    protected override async Task<IReadOnlyList<ClienteDto>> ObtenerAsync() => await _api.ListarAsync();

    protected override bool Coincide(ClienteDto i, string t) =>
        Contiene(i.Nombre, t) || Contiene(i.IdentificacionFiscal, t) || Contiene(i.Telefono, t) || Contiene(i.Email, t);

    protected override Task NuevoAsync() => AbrirEditorAsync(null);
    protected override Task EditarAsync(ClienteDto item) => AbrirEditorAsync(item);

    private async Task AbrirEditorAsync(ClienteDto? item)
    {
        using var dlg = new EditDialog(item is null ? "Nuevo cliente" : "Editar cliente");
        var nombre = dlg.AgregarTexto("Nombre *", item?.Nombre, maxLength: 150);
        var nit = dlg.AgregarTexto("NIT / DUI", item?.IdentificacionFiscal, maxLength: 50);
        var telefono = dlg.AgregarTexto("Teléfono", item?.Telefono, maxLength: 30);
        var email = dlg.AgregarTexto("Email", item?.Email, maxLength: 150);
        var direccion = dlg.AgregarTexto("Dirección", item?.Direccion, multilinea: true, maxLength: 250);
        dlg.Validar(() => string.IsNullOrWhiteSpace(nombre.Text) ? "El nombre es obligatorio." : null);
        dlg.AlGuardar(async () =>
        {
            var dto = new ClienteSaveDto(nombre.Text.Trim(), nit.TextoONull(), telefono.TextoONull(), email.TextoONull(), direccion.TextoONull());
            if (item is null) await _api.CrearAsync(dto);
            else await _api.ActualizarAsync(item.Id, dto);
        });
        if (dlg.ShowDialog(this) == DialogResult.OK) await CargarAsync();
    }

    protected override async Task DesactivarAsync(ClienteDto item)
    {
        if (!Dialogs.Confirmar(this, $"¿Desactivar el cliente \"{item.Nombre}\"?")) return;
        await _api.DesactivarAsync(item.Id);
        await CargarAsync();
    }
}

public sealed class ProveedoresForm : ListForm<ProveedorDto>
{
    private readonly ProveedoresApi _api;
    private readonly SessionService _sesion;

    public ProveedoresForm(ProveedoresApi api, SessionService sesion) : base("Proveedores")
    {
        _api = api;
        _sesion = sesion;
    }

    protected override bool PuedeCrear => _sesion.Tiene(Permisos.ProveedoresGestionar);
    protected override bool PuedeEditar => _sesion.Tiene(Permisos.ProveedoresGestionar);
    protected override bool PuedeDesactivar => _sesion.Tiene(Permisos.ProveedoresEliminar);
    protected override bool TieneEstado => true;
    protected override bool EsInactivo(ProveedorDto item) => item.Estado != "A";

    protected override void ConfigurarColumnas(DataGridView grid)
    {
        grid.Col("Nombre", nameof(ProveedorDto.Nombre), 220, relleno: true);
        grid.Col("Contacto", nameof(ProveedorDto.Contacto), 160);
        grid.Col("Teléfono", nameof(ProveedorDto.Telefono), 120);
        grid.Col("Email", nameof(ProveedorDto.Email), 200);
        grid.Col("NIT", nameof(ProveedorDto.IdentificacionFiscal), 130);
        grid.Col("Estado", nameof(ProveedorDto.Estado), 90, FormatoColumna.Estado);
    }

    protected override async Task<IReadOnlyList<ProveedorDto>> ObtenerAsync() => await _api.ListarAsync();

    protected override bool Coincide(ProveedorDto i, string t) =>
        Contiene(i.Nombre, t) || Contiene(i.Contacto, t) || Contiene(i.IdentificacionFiscal, t) || Contiene(i.Telefono, t);

    protected override Task NuevoAsync() => AbrirEditorAsync(null);
    protected override Task EditarAsync(ProveedorDto item) => AbrirEditorAsync(item);

    private async Task AbrirEditorAsync(ProveedorDto? item)
    {
        using var dlg = new EditDialog(item is null ? "Nuevo proveedor" : "Editar proveedor");
        var nombre = dlg.AgregarTexto("Nombre *", item?.Nombre, maxLength: 150);
        var contacto = dlg.AgregarTexto("Contacto", item?.Contacto, maxLength: 100);
        var telefono = dlg.AgregarTexto("Teléfono", item?.Telefono, maxLength: 30);
        var email = dlg.AgregarTexto("Email", item?.Email, maxLength: 150);
        var nit = dlg.AgregarTexto("NIT", item?.IdentificacionFiscal, maxLength: 50);
        var direccion = dlg.AgregarTexto("Dirección", item?.Direccion, multilinea: true, maxLength: 250);
        dlg.Validar(() => string.IsNullOrWhiteSpace(nombre.Text) ? "El nombre es obligatorio." : null);
        dlg.AlGuardar(async () =>
        {
            var dto = new ProveedorSaveDto(nombre.Text.Trim(), contacto.TextoONull(), telefono.TextoONull(), email.TextoONull(), direccion.TextoONull(), nit.TextoONull());
            if (item is null) await _api.CrearAsync(dto);
            else await _api.ActualizarAsync(item.Id, dto);
        });
        if (dlg.ShowDialog(this) == DialogResult.OK) await CargarAsync();
    }

    protected override async Task DesactivarAsync(ProveedorDto item)
    {
        if (!Dialogs.Confirmar(this, $"¿Desactivar el proveedor \"{item.Nombre}\"?")) return;
        await _api.DesactivarAsync(item.Id);
        await CargarAsync();
    }
}
