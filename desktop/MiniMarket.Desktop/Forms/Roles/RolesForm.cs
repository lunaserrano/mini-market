namespace MiniMarket.Desktop.Forms.Roles;

/// <summary>
/// Roles y sus permisos (árbol por módulo con casillas). El rol de sistema "admin" siempre tiene
/// todos los permisos y no se edita; los demás roles de sistema no se eliminan.
/// </summary>
public sealed class RolesForm : ListForm<RolDto>
{
    private const string CodigoAdmin = "admin";
    private readonly RolesApi _api;
    private readonly SessionService _sesion;

    public RolesForm(RolesApi api, SessionService sesion) : base("Roles y permisos")
    {
        _api = api;
        _sesion = sesion;
        BtnDesactivar.Text = "Eliminar";
    }

    protected override bool PuedeCrear => _sesion.Tiene(Permisos.RolesGestionar);
    protected override bool PuedeEditar => true;
    protected override bool PuedeDesactivar => _sesion.Tiene(Permisos.RolesGestionar);

    protected override void ConfigurarColumnas(DataGridView grid)
    {
        grid.Col("Nombre", nameof(RolDto.Nombre), 200);
        grid.Col("Código", nameof(RolDto.Codigo), 130);
        grid.Col("Descripción", nameof(RolDto.Descripcion), 300, relleno: true);
        grid.Col("Sistema", nameof(RolDto.EsSistema), 80, FormatoColumna.SiNo);
        grid.Col("Usuarios", nameof(RolDto.TotalUsuarios), 80);
        grid.Col("Permisos", nameof(RolDto.TotalPermisos), 80);
        if (!PuedeCrear) BtnEditar.Text = "Ver permisos";
    }

    protected override async Task<IReadOnlyList<RolDto>> ObtenerAsync() => await _api.ListarAsync();
    protected override bool Coincide(RolDto i, string t) => Contiene(i.Nombre, t) || Contiene(i.Codigo, t);

    protected override Task NuevoAsync() => AbrirEditorAsync(null);
    protected override Task EditarAsync(RolDto item) => AbrirEditorAsync(item);

    private async Task AbrirEditorAsync(RolDto? rol)
    {
        var catalogo = await _api.CatalogoPermisosAsync();
        var detalle = rol is null ? null : await _api.ObtenerAsync(rol.Id);
        var esAdmin = detalle?.Codigo == CodigoAdmin && detalle.EsSistema;
        var editable = _sesion.Tiene(Permisos.RolesGestionar) && !esAdmin;

        using var dlg = new EditDialog(rol is null ? "Nuevo rol" : $"Rol: {rol.Nombre}", 620);
        var nombre = dlg.AgregarTexto("Nombre *", detalle?.Nombre, maxLength: 100);
        var descripcion = dlg.AgregarTexto("Descripción", detalle?.Descripcion, multilinea: true, maxLength: 250);
        if (esAdmin)
            dlg.AgregarAncho(new Label { Text = "El rol Administrador tiene siempre todos los permisos y no se puede modificar.", ForeColor = Theme.Advertencia, AutoSize = true });

        var arbol = new TreeView { CheckBoxes = true, Size = new Size(560, 360), Enabled = editable };
        var marcados = new HashSet<string>(detalle?.Permisos ?? Array.Empty<string>());
        foreach (var modulo in catalogo)
        {
            var nodoModulo = arbol.Nodes.Add(modulo.Modulo);
            foreach (var p in modulo.Permisos)
            {
                var nodo = nodoModulo.Nodes.Add(p.Codigo, $"{p.Nombre}  ({p.Codigo})");
                nodo.Tag = p.Codigo;
                nodo.Checked = esAdmin || marcados.Contains(p.Codigo);
            }
            nodoModulo.Checked = nodoModulo.Nodes.Cast<TreeNode>().All(n => n.Checked);
        }
        arbol.ExpandAll();

        // Marcar un módulo marca/desmarca todos sus permisos; un permiso actualiza su módulo.
        var propagando = false;
        arbol.AfterCheck += (_, e) =>
        {
            if (propagando || e.Node is null) return;
            propagando = true;
            if (e.Node.Tag is null)
                foreach (TreeNode hijo in e.Node.Nodes) hijo.Checked = e.Node.Checked;
            else if (e.Node.Parent is { } padre)
                padre.Checked = padre.Nodes.Cast<TreeNode>().All(n => n.Checked);
            propagando = false;
        };
        dlg.AgregarAncho(new Label { Text = "Permisos", Font = Theme.FuenteNegrita, AutoSize = true });
        dlg.AgregarAncho(arbol);

        if (!editable)
        {
            dlg.BotonAceptar.Visible = false;
            dlg.BotonCancelar.Text = "Cerrar";
            nombre.Enabled = descripcion.Enabled = false;
            dlg.ShowDialog(this);
            return;
        }

        List<string> Seleccionados() => arbol.Nodes.Cast<TreeNode>()
            .SelectMany(m => m.Nodes.Cast<TreeNode>())
            .Where(n => n.Checked).Select(n => (string)n.Tag!).ToList();

        dlg.Validar(() => string.IsNullOrWhiteSpace(nombre.Text) ? "El nombre es obligatorio." : null);
        dlg.Validar(() => Seleccionados().Count == 0 ? "Asigne al menos un permiso." : null);
        dlg.AlGuardar(async () =>
        {
            var dto = new RolSaveDto(nombre.Text.Trim(), descripcion.TextoONull(), Seleccionados());
            if (rol is null) await _api.CrearAsync(dto);
            else await _api.ActualizarAsync(rol.Id, dto);
        });
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            if (rol is not null && rol.TotalUsuarios > 0)
                Dialogs.Info(this, "Los usuarios con este rol verán los cambios de permisos al renovar su sesión (máx. 15 minutos) o al volver a ingresar.");
            await CargarAsync();
        }
    }

    protected override async Task DesactivarAsync(RolDto item)
    {
        if (item.EsSistema)
        {
            Dialogs.Aviso(this, "Los roles de sistema no se pueden eliminar.");
            return;
        }
        if (item.TotalUsuarios > 0)
        {
            Dialogs.Aviso(this, $"El rol tiene {item.TotalUsuarios} usuario(s) asignado(s). Reasígnelos antes de eliminarlo.");
            return;
        }
        if (!Dialogs.Confirmar(this, $"¿Eliminar el rol \"{item.Nombre}\"?")) return;
        await _api.EliminarAsync(item.Id);
        await CargarAsync();
    }
}
