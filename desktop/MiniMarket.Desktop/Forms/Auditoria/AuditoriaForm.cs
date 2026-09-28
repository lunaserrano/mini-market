using System.Text.Json;

namespace MiniMarket.Desktop.Forms.Auditoria;

/// <summary>
/// Bitácora de seguridad y actividad (eventos de seguridad, peticiones a la Api y navegación de la
/// UI), paginada en el servidor. Con empresa.editar se puede activar/desactivar el registro.
/// </summary>
public sealed class AuditoriaForm : ChildForm
{
    private const int TamanoPagina = 50;

    private readonly AuditoriaApi _api;
    private readonly UsuariosApi _usuarios;
    private readonly SessionService _sesion;

    private readonly DateTimePicker _desde = new() { Format = DateTimePickerFormat.Short, Width = 120 };
    private readonly DateTimePicker _hasta = new() { Format = DateTimePickerFormat.Short, Width = 120 };
    private readonly ComboBox _tipo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    private readonly ComboBox _origen = Controles.Combo(new[] { "", "SEG", "API", "UI" },
        o => o switch { "" => "Todos", "SEG" => "Seguridad", "API" => "Api", _ => "Interfaz" });
    private readonly ComboBox _usuario = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    private readonly DataGridView _grid = new DataGridView().Estandar();
    private readonly TextBox _detalle = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font("Consolas", 9F), WordWrap = false };
    private readonly Label _paginacion = new() { AutoSize = true, Margin = new Padding(8, 9, 8, 4) };
    private readonly CheckBox _habilitada = new() { Text = "Registro de auditoría habilitado", AutoSize = true, Margin = new Padding(16, 8, 4, 4) };
    private int _pagina = 1;
    private int _total;
    private bool _cargandoEstado;

    public AuditoriaForm(AuditoriaApi api, UsuariosApi usuarios, SessionService sesion)
    {
        _api = api;
        _usuarios = usuarios;
        _sesion = sesion;
        Text = "Auditoría";
        _desde.Value = DateTime.Today.AddDays(-7);
        _hasta.Value = DateTime.Today;
        _origen.SelectedIndex = 0;
        _origen.Width = 110;

        var filtros = Controles.Barra(
            Controles.Etiqueta("Desde"), _desde, Controles.Etiqueta("Hasta"), _hasta,
            Controles.Etiqueta("Tipo"), _tipo, Controles.Etiqueta("Origen"), _origen,
            Controles.Etiqueta("Usuario"), _usuario,
            Theme.Boton("Consultar", async (_, _) => { _pagina = 1; await RecargarAsync(); }, primario: true));
        var paginas = Controles.Barra(
            Theme.Boton("◀ Anterior", async (_, _) => { if (_pagina > 1) { _pagina--; await RecargarAsync(); } }),
            _paginacion,
            Theme.Boton("Siguiente ▶", async (_, _) => { if (_pagina * TamanoPagina < _total) { _pagina++; await RecargarAsync(); } }),
            Theme.Boton("Exportar página CSV", (_, _) => CsvExporter.Exportar(this, _grid, Text)));
        paginas.Dock = DockStyle.Bottom;
        if (_sesion.Tiene(Permisos.EmpresaEditar)) paginas.Controls.Add(_habilitada);

        _grid.Col("Fecha", nameof(EventoSeguridadDto.FechaUtc), 150, FormatoColumna.FechaUtc);
        _grid.Col("Tipo", nameof(EventoSeguridadDto.Tipo), 150);
        _grid.Col("Origen", nameof(EventoSeguridadDto.Origen), 60);
        _grid.Col("Usuario", nameof(EventoSeguridadDto.ActorNombre), 150);
        _grid.Col("Método", nameof(EventoSeguridadDto.Metodo), 60);
        _grid.Col("Ruta", nameof(EventoSeguridadDto.Ruta), 220);
        _grid.Col("Estado", nameof(EventoSeguridadDto.StatusCode), 60);
        _grid.Col("ms", nameof(EventoSeguridadDto.DuracionMs), 60);
        _grid.Col("Detalle", nameof(EventoSeguridadDto.Detalle), 250, relleno: true);
        _grid.ColorearFilas<EventoSeguridadDto>(e => e.StatusCode >= 400 || e.Tipo.Contains("FALLIDO") || e.Tipo.Contains("BLOQUEADA") || e.Tipo.Contains("REUSO") ? Theme.FilaAlerta : null);
        _grid.SelectionChanged += (_, _) => MostrarDetalle(_grid.Seleccionado<EventoSeguridadDto>());

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 380 };
        split.Panel1.Controls.Add(_grid);
        split.Panel2.Controls.Add(_detalle);

        Controls.Add(split);
        Controls.Add(paginas);
        Controls.Add(filtros);

        _habilitada.CheckedChanged += async (_, _) =>
        {
            if (_cargandoEstado) return;
            var valor = _habilitada.Checked;
            if (!valor && !Dialogs.Confirmar(this, "¿Desactivar el registro de auditoría? Dejarán de registrarse accesos y operaciones."))
            {
                _cargandoEstado = true;
                _habilitada.Checked = true;
                _cargandoEstado = false;
                return;
            }
            await this.EjecutarAsync(() => _api.ActualizarEstadoAsync(valor));
        };
    }

    protected override async Task CargarAsync()
    {
        if (_tipo.Items.Count == 0)
        {
            _tipo.Items.Add(new Opcion<string?>(null, "Todos"));
            foreach (var t in await _api.TiposAsync()) _tipo.Items.Add(new Opcion<string?>(t, t));
            _tipo.SelectedIndex = 0;

            _usuario.Items.Add(new Opcion<UsuarioDto?>(null, "Todos"));
            if (_sesion.Tiene(Permisos.UsuariosVer))
                foreach (var u in (await _usuarios.ListarAsync()).OrderBy(u => u.NombreCompleto))
                    _usuario.Items.Add(new Opcion<UsuarioDto?>(u, $"{u.NombreCompleto} ({u.Username})"));
            _usuario.SelectedIndex = 0;

            if (_sesion.Tiene(Permisos.EmpresaEditar))
            {
                _cargandoEstado = true;
                _habilitada.Checked = (await _api.ObtenerEstadoAsync()).Habilitada;
                _cargandoEstado = false;
            }
        }

        var origen = _origen.Seleccion<string>();
        var resultado = await _api.ListarAsync(
            Formatters.AUtc(_desde.Value.Date),
            Formatters.AUtc(_hasta.Value.Date.AddDays(1).AddTicks(-1)),
            _usuario.Seleccion<UsuarioDto?>()?.Id,
            _tipo.Seleccion<string?>(),
            string.IsNullOrEmpty(origen) ? null : origen,
            _pagina, TamanoPagina);

        _total = resultado.Total;
        _grid.DataSource = resultado.Items.ToList();
        var paginas = Math.Max(1, (int)Math.Ceiling(_total / (double)TamanoPagina));
        _paginacion.Text = $"Página {_pagina} de {paginas} · {_total} eventos";
    }

    private void MostrarDetalle(EventoSeguridadDto? e)
    {
        if (e is null)
        {
            _detalle.Clear();
            return;
        }
        var datos = e.Datos;
        if (!string.IsNullOrWhiteSpace(datos))
        {
            try
            {
                using var doc = JsonDocument.Parse(datos);
                datos = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            }
            catch (JsonException)
            {
                // Se muestra tal cual.
            }
        }
        _detalle.Text = string.Join(Environment.NewLine, new[]
        {
            $"Evento #{e.Id} · {e.Tipo} · {Formatters.Fecha(e.FechaUtc)} · Origen {e.Origen}",
            $"Actor: {e.ActorNombre ?? "—"} · Objetivo: {e.ObjetivoNombre ?? "—"} · IP: {e.Ip ?? "—"}",
            $"{e.Metodo} {e.Ruta} -> {e.StatusCode} ({e.DuracionMs} ms)",
            $"Detalle: {e.Detalle}",
            "",
            datos ?? ""
        }).Replace("\n", Environment.NewLine);
    }
}
