namespace MiniMarket.Desktop.UI;

/// <summary>
/// Diálogo de edición construido por código: filas "etiqueta + control" en un TableLayoutPanel,
/// validaciones locales y, opcionalmente, una acción de guardado asíncrona (llamada a la Api). Si la
/// Api rechaza el guardado, el diálogo queda abierto con los datos para corregir.
/// </summary>
public class EditDialog : Form
{
    private readonly TableLayoutPanel _tabla;
    private readonly List<Func<string?>> _validaciones = new();
    private Func<Task>? _guardar;

    public Button BotonAceptar { get; }
    public Button BotonCancelar { get; }

    public EditDialog(string titulo, int ancho = 480)
    {
        Theme.Aplicar(this);
        Text = titulo;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(0, 0, 12, 12);

        // Raíz sin Dock: con AutoSize del formulario, los controles acoplados (Fill) no informan bien su tamaño.
        var raiz = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Location = new Point(12, 12)
        };

        _tabla = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Dock = DockStyle.Fill
        };
        _tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        _tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ancho - 180));

        BotonAceptar = Theme.Boton("Guardar", primario: true);
        BotonCancelar = Theme.Boton("Cancelar");
        BotonCancelar.DialogResult = DialogResult.Cancel;
        BotonAceptar.Click += async (_, _) => await AceptarAsync();

        var botones = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0)
        };
        botones.Controls.AddRange(new Control[] { BotonCancelar, BotonAceptar });

        raiz.Controls.Add(_tabla, 0, 0);
        raiz.Controls.Add(botones, 0, 1);
        Controls.Add(raiz);
        AcceptButton = BotonAceptar;
        CancelButton = BotonCancelar;
    }

    public T Agregar<T>(string etiqueta, T control) where T : Control
    {
        // Anchor (no Dock.Fill): en filas AutoSize conserva la altura propia del control (p. ej. multilínea).
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        control.Margin = new Padding(4);
        _tabla.RowCount++;
        _tabla.Controls.Add(new Label { Text = etiqueta, AutoSize = true, MaximumSize = new Size(145, 0), Anchor = AnchorStyles.Left, Margin = new Padding(4, 8, 8, 4) });
        _tabla.Controls.Add(control);
        return control;
    }

    /// <summary>Control que ocupa las dos columnas (ej. una grilla o una nota).</summary>
    public T AgregarAncho<T>(T control) where T : Control
    {
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        control.Margin = new Padding(4);
        _tabla.RowCount++;
        _tabla.Controls.Add(control);
        _tabla.SetColumnSpan(control, 2);
        return control;
    }

    public TextBox AgregarTexto(string etiqueta, string? valor = "", bool multilinea = false, bool password = false, int maxLength = 200) =>
        Agregar(etiqueta, new TextBox
        {
            Text = valor ?? "",
            Multiline = multilinea,
            Height = multilinea ? 70 : 27,
            ScrollBars = multilinea ? ScrollBars.Vertical : ScrollBars.None,
            UseSystemPasswordChar = password,
            MaxLength = maxLength
        });

    public NumericUpDown AgregarMonto(string etiqueta, decimal valor = 0, decimal minimo = 0) =>
        Agregar(etiqueta, Controles.Monto(valor, minimo));

    public NumericUpDown AgregarNumero(string etiqueta, decimal valor, int decimales = 0, decimal minimo = 0, decimal maximo = 1_000_000) =>
        Agregar(etiqueta, Controles.Numero(valor, decimales, minimo, maximo));

    public CheckBox AgregarCheck(string texto, bool valor = false)
    {
        var chk = new CheckBox { Text = texto, Checked = valor, AutoSize = true };
        return Agregar("", chk);
    }

    public ComboBox AgregarCombo<T>(string etiqueta, IEnumerable<T> items, Func<T, string> texto, Func<T, bool>? seleccionado = null)
    {
        var combo = Controles.Combo(items, texto);
        if (seleccionado is not null)
        {
            var idx = combo.Items.Cast<Opcion<T>>().ToList().FindIndex(o => seleccionado(o.Valor));
            combo.SelectedIndex = idx;
        }
        return Agregar(etiqueta, combo);
    }

    public DateTimePicker AgregarFecha(string etiqueta, DateTime? valor, bool opcional = true)
    {
        var picker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            ShowCheckBox = opcional,
            Checked = valor is not null,
            Value = valor ?? DateTime.Today
        };
        return Agregar(etiqueta, picker);
    }

    public void Validar(Func<string?> validacion) => _validaciones.Add(validacion);

    /// <summary>Acción que se ejecuta al presionar "Guardar" (después de validar). Si lanza, el diálogo no se cierra.</summary>
    public void AlGuardar(Func<Task> guardar) => _guardar = guardar;

    private async Task AceptarAsync()
    {
        var mensajes = _validaciones.Select(v => v()).Where(m => m is not null).ToList();
        if (mensajes.Count > 0)
        {
            Dialogs.Aviso(this, string.Join(Environment.NewLine, mensajes));
            return;
        }

        if (_guardar is not null)
        {
            BotonAceptar.Enabled = false;
            var ok = await this.EjecutarAsync(_guardar);
            BotonAceptar.Enabled = true;
            if (!ok) return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
