namespace MiniMarket.Desktop.UI;

/// <summary>
/// Plantilla de listado reutilizada por todos los mantenimientos: barra con búsqueda, Nuevo, Editar,
/// Desactivar, Refrescar, Exportar CSV e Imprimir; grilla filtrable en memoria y contador de registros.
/// Cada pantalla solo declara columnas, cómo obtener los datos y qué hacer en cada acción.
/// </summary>
public abstract class ListForm<T> : ChildForm where T : class
{
    private List<T> _todos = new();
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 250 };

    protected readonly DataGridView Grid = new DataGridView().Estandar();
    protected readonly TextBox Buscar = new() { Width = 260, PlaceholderText = "Buscar..." };
    protected readonly CheckBox MostrarInactivos = new() { Text = "Mostrar inactivos", AutoSize = true, Margin = new Padding(12, 8, 4, 4) };
    protected readonly FlowLayoutPanel Barra;
    protected readonly Button BtnNuevo;
    protected readonly Button BtnEditar;
    protected readonly Button BtnDesactivar;
    private readonly Label _contador = new() { AutoSize = true, ForeColor = Theme.TextoSuave, Margin = new Padding(12, 9, 4, 4) };

    protected ListForm(string titulo)
    {
        Text = titulo;
        BtnNuevo = Theme.Boton("Nuevo", async (_, _) => await AccionAsync(NuevoAsync), primario: true);
        BtnEditar = Theme.Boton("Editar", async (_, _) => await AccionSeleccionAsync(EditarAsync));
        BtnDesactivar = Theme.Boton(TextoDesactivar, async (_, _) => await AccionSeleccionAsync(DesactivarAsync), peligro: true);
        var btnRefrescar = Theme.Boton("Refrescar (F5)", async (_, _) => await RecargarAsync());
        var btnExportar = Theme.Boton("Exportar CSV", (_, _) => CsvExporter.Exportar(this, Grid, Text));
        var btnImprimir = Theme.Boton("Imprimir", (_, _) => GridPrinter.Imprimir(this, Grid, Text));

        Barra = Controles.Barra(Buscar, BtnNuevo, BtnEditar, BtnDesactivar, btnRefrescar, btnExportar, btnImprimir, MostrarInactivos, _contador);

        Buscar.TextChanged += (_, _) => { _debounce.Stop(); _debounce.Start(); };
        _debounce.Tick += (_, _) => { _debounce.Stop(); AplicarFiltro(); };
        MostrarInactivos.CheckedChanged += (_, _) => AplicarFiltro();
        Grid.CellDoubleClick += async (_, e) =>
        {
            if (e.RowIndex >= 0 && BtnEditar.Visible) await AccionSeleccionAsync(EditarAsync);
        };
        Grid.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && BtnEditar.Visible)
            {
                e.Handled = true;
                await AccionSeleccionAsync(EditarAsync);
            }
        };
        Grid.ColorearFilas<T>(item => EsInactivo(item) ? Theme.FilaInactiva : ColorFila(item));

        Controls.Add(Grid);
        Controls.Add(Barra);
        Controls.Add(ConTitulo(Theme.Titulo(titulo)));
        Load += (_, _) =>
        {
            ConfigurarColumnas(Grid);
            BtnNuevo.Visible = PuedeCrear;
            BtnEditar.Visible = PuedeEditar;
            BtnDesactivar.Visible = PuedeDesactivar;
            MostrarInactivos.Visible = TieneEstado;
        };
    }

    private static Control ConTitulo(Label titulo)
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8, 8, 8, 0) };
        panel.Controls.Add(titulo);
        return panel;
    }

    protected abstract void ConfigurarColumnas(DataGridView grid);
    protected abstract Task<IReadOnlyList<T>> ObtenerAsync();
    protected abstract bool Coincide(T item, string texto);

    protected virtual bool PuedeCrear => false;
    protected virtual bool PuedeEditar => false;
    protected virtual bool PuedeDesactivar => false;
    protected virtual string TextoDesactivar => "Desactivar";

    /// <summary>True si la entidad maneja Estado A/I (activa el filtro "Mostrar inactivos").</summary>
    protected virtual bool TieneEstado => false;
    protected virtual bool EsInactivo(T item) => false;
    protected virtual Color? ColorFila(T item) => null;

    // Las acciones corren dentro de EjecutarAsync (cursor de espera + errores); para refrescar la
    // grilla al terminar deben llamar a CargarAsync() directamente, no a RecargarAsync().
    protected virtual Task NuevoAsync() => Task.CompletedTask;
    protected virtual Task EditarAsync(T item) => Task.CompletedTask;
    protected virtual Task DesactivarAsync(T item) => Task.CompletedTask;

    protected IReadOnlyList<T> Items => _todos;

    protected override async Task CargarAsync()
    {
        var seleccionado = Grid.CurrentRow?.Index ?? 0;
        _todos = (await ObtenerAsync()).ToList();
        AplicarFiltro();
        if (Grid.Rows.Count > 0)
            Grid.CurrentCell = Grid.Rows[Math.Min(seleccionado, Grid.Rows.Count - 1)].Cells[Grid.Columns.GetFirstColumn(DataGridViewElementStates.Visible)!.Index];
    }

    protected void AplicarFiltro()
    {
        var texto = Buscar.Text.Trim();
        var filtrados = _todos
            .Where(i => !TieneEstado || MostrarInactivos.Checked || !EsInactivo(i))
            .Where(i => texto.Length == 0 || Coincide(i, texto))
            .ToList();
        Grid.DataSource = filtrados;
        _contador.Text = filtrados.Count == _todos.Count ? $"{_todos.Count} registros" : $"{filtrados.Count} de {_todos.Count} registros";
    }

    protected static bool Contiene(string? valor, string texto) =>
        valor is not null && valor.Contains(texto, StringComparison.CurrentCultureIgnoreCase);

    private Task AccionAsync(Func<Task> accion) => this.EjecutarAsync(accion);

    private async Task AccionSeleccionAsync(Func<T, Task> accion)
    {
        var item = Grid.Seleccionado<T>();
        if (item is null)
        {
            Dialogs.Info(this, "Seleccione un registro.");
            return;
        }
        await this.EjecutarAsync(() => accion(item));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _debounce.Dispose();
        base.Dispose(disposing);
    }
}
