using System.Globalization;

namespace MiniMarket.Desktop.UI;

/// <summary>
/// Formato de moneda y fechas según la configuración de la empresa (GET empresa/actual). Las fechas
/// llegan de la Api en UTC (datetime2 sin zona) y se muestran en la zona horaria de la empresa.
/// </summary>
public static class Formatters
{
    private static string _simbolo = "$";
    private static TimeZoneInfo _zona = TimeZoneInfo.Local;
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-SV");

    public static decimal TasaImpuesto { get; private set; } = 13m;

    public static void Configurar(EmpresaDto empresa)
    {
        _simbolo = string.IsNullOrWhiteSpace(empresa.SimboloMoneda) ? "$" : empresa.SimboloMoneda;
        TasaImpuesto = empresa.TasaImpuesto;
        try
        {
            _zona = TimeZoneInfo.FindSystemTimeZoneById(empresa.ZonaHoraria);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            _zona = TimeZoneInfo.Local;
        }
    }

    public static string Simbolo => _simbolo;

    /// <summary>Formato de columnas de DataGridView para importes.</summary>
    public static string FormatoMoneda => $"{_simbolo}#,##0.00;-{_simbolo}#,##0.00";

    public static string Moneda(decimal valor) => valor.ToString(FormatoMoneda, Cultura);
    public static string Moneda(decimal? valor) => valor is null ? "" : Moneda(valor.Value);
    public static string Cantidad(decimal valor) => valor.ToString("#,##0.###", Cultura);

    /// <summary>UTC (Kind Unspecified desde la Api) -> hora de la empresa.</summary>
    public static DateTime ALocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zona);

    public static DateTime? ALocal(DateTime? utc) => utc is null ? null : ALocal(utc.Value);

    /// <summary>Fecha/hora local de la empresa -> UTC, para filtros enviados a la Api.</summary>
    public static DateTime AUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), _zona);

    public static string Fecha(DateTime utc) => ALocal(utc).ToString("dd/MM/yyyy HH:mm", Cultura);
    public static string Fecha(DateTime? utc) => utc is null ? "" : Fecha(utc.Value);
    public static string SoloFecha(DateTime? utc) => utc is null ? "" : ALocal(utc.Value).ToString("dd/MM/yyyy", Cultura);

    /// <summary>Precio sin IVA a partir del precio final con IVA incluido (redondeo a centavos, igual que la web).</summary>
    public static decimal SinImpuesto(decimal conImpuesto) => Math.Round(conImpuesto / (1 + TasaImpuesto / 100m), 2);
    public static decimal ConImpuesto(decimal sinImpuesto) => Math.Round(sinImpuesto * (1 + TasaImpuesto / 100m), 2);

    public static string Estado(string estado) => estado switch
    {
        "A" => "Activo",
        "I" => "Inactivo",
        _ => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(estado.ToLowerInvariant())
    };

    public static string MetodoPago(string metodo) => metodo switch
    {
        "EFECTIVO" => "Efectivo",
        "TARJETA" => "Tarjeta",
        "TRANSFERENCIA" => "Transferencia",
        _ => metodo
    };

    public static readonly (string Valor, string Texto)[] MetodosPago =
    {
        ("EFECTIVO", "Efectivo"), ("TARJETA", "Tarjeta"), ("TRANSFERENCIA", "Transferencia")
    };
}
