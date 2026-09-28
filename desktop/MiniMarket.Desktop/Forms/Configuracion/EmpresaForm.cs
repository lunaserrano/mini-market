namespace MiniMarket.Desktop.Forms.Configuracion;

/// <summary>
/// Datos de la empresa: nombre y datos fiscales (salen en el ticket), zona horaria, moneda e IVA
/// (tasa única con la que se calcula el impuesto incluido en los precios).
/// </summary>
public sealed class EmpresaForm : ChildForm
{
    private readonly EmpresaApi _api;
    private readonly SessionService _sesion;

    private readonly TextBox _nombre = new() { Width = 360, MaxLength = 150 };
    private readonly TextBox _razon = new() { Width = 360, MaxLength = 200 };
    private readonly TextBox _nit = new() { Width = 200, MaxLength = 50 };
    private readonly ComboBox _zona = new() { Width = 300, DropDownStyle = ComboBoxStyle.DropDown };
    private readonly TextBox _codigoMoneda = new() { Width = 80, MaxLength = 3, CharacterCasing = CharacterCasing.Upper };
    private readonly TextBox _simbolo = new() { Width = 80, MaxLength = 5 };
    private readonly NumericUpDown _iva = Controles.Numero(13, 2, 0, 100);

    public EmpresaForm(EmpresaApi api, SessionService sesion)
    {
        _api = api;
        _sesion = sesion;
        Text = "Configuración de la empresa";
        _iva.Width = 100;
        _zona.Items.AddRange(new object[]
        {
            "America/El_Salvador", "America/Guatemala", "America/Tegucigalpa", "America/Managua", "America/Costa_Rica",
            "America/Panama", "America/Mexico_City", "America/Bogota", "America/Lima", "America/Santiago", "America/New_York"
        });

        var tabla = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Padding = new Padding(16), Location = new Point(8, 56) };
        void Fila(string etiqueta, Control c)
        {
            tabla.Controls.Add(new Label { Text = etiqueta, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(4, 8, 12, 4) });
            tabla.Controls.Add(c);
        }
        Fila("Nombre comercial *", _nombre);
        Fila("Razón social", _razon);
        Fila("NIT / Identificación fiscal", _nit);
        Fila("Zona horaria *", _zona);
        Fila("Código de moneda (ISO) *", _codigoMoneda);
        Fila("Símbolo de moneda *", _simbolo);
        Fila("IVA (%) *", _iva);
        tabla.Controls.Add(new Label());
        tabla.Controls.Add(Theme.Boton("Guardar cambios", async (_, _) => await this.EjecutarAsync(GuardarAsync), primario: true));

        Controls.Add(tabla);
        Controls.Add(new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8), Controls = { Theme.Titulo(Text) } });
    }

    public override string Ruta => "configuracion";

    protected override async Task CargarAsync()
    {
        var e = await _api.ObtenerActualAsync();
        _nombre.Text = e.Nombre;
        _razon.Text = e.RazonSocial;
        _nit.Text = e.IdentificacionFiscal;
        _zona.Text = e.ZonaHoraria;
        _codigoMoneda.Text = e.CodigoMoneda;
        _simbolo.Text = e.SimboloMoneda;
        _iva.Value = e.TasaImpuesto;
    }

    private async Task GuardarAsync()
    {
        if (string.IsNullOrWhiteSpace(_nombre.Text) || string.IsNullOrWhiteSpace(_zona.Text) ||
            _codigoMoneda.Text.Trim().Length != 3 || string.IsNullOrWhiteSpace(_simbolo.Text))
        {
            Dialogs.Aviso(this, "Complete los campos obligatorios (el código de moneda tiene 3 letras, ej. USD).");
            return;
        }
        if (_sesion.Empresa is { } actual && actual.TasaImpuesto != _iva.Value &&
            !Dialogs.Confirmar(this, $"Cambiar el IVA de {actual.TasaImpuesto:0.##}% a {_iva.Value:0.##}% afecta el cálculo de impuesto de las ventas nuevas.\n¿Continuar?"))
            return;

        var empresa = await _api.ActualizarAsync(new EmpresaUpdateDto(_nombre.Text.Trim(), _razon.TextoONull(), _nit.TextoONull(),
            _zona.Text.Trim(), _codigoMoneda.Text.Trim(), _simbolo.Text.Trim(), _iva.Value));
        _sesion.Empresa = empresa;
        Formatters.Configurar(empresa);
        Dialogs.Info(this, "Configuración guardada.");
    }
}
