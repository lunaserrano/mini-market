using System.Text.Json;

namespace MiniMarket.Desktop.Services;

public sealed record Notificacion(string Id, int ProductoId, string ProductoNombre, bool SinStock, string Mensaje, DateTime FechaUtc, bool Leida)
{
    public string Texto => $"{ProductoNombre} {Mensaje}";
}

/// <summary>Notificaciones recién detectadas en una consulta, para el aviso emergente.</summary>
public sealed class NotificacionesNuevasEventArgs(IReadOnlyList<Notificacion> nuevas, bool primeraCarga) : EventArgs
{
    public IReadOnlyList<Notificacion> Nuevas { get; } = nuevas;
    public bool PrimeraCarga { get; } = primeraCarga;
}

/// <summary>
/// Notificaciones de la campanita de <see cref="Forms.MainForm"/> (mismo comportamiento que la
/// versión web). Genera alertas de stock mínimo a partir de GET /api/inventario: no hay tabla de
/// notificaciones en el backend, así que el estado leída/no leída se guarda por usuario en
/// %LOCALAPPDATA%\MiniMarket\notificaciones.{usuarioId}.json. Cuando un producto se repone por encima
/// del mínimo, su notificación desaparece; si vuelve a bajar, aparece como nueva.
///
/// Sin sondeo periódico: se consulta al entrar, al abrir la campanita y después de cada operación que
/// mueve stock (venta, anulación de venta, compra, anulación de compra, ajuste), que llaman a
/// <see cref="Refrescar"/>. Abrir la campanita cubre los movimientos hechos por otras cajas.
/// Todo corre en el hilo de UI: los eventos se disparan ahí.
/// </summary>
public sealed class NotificacionService
{
    private readonly IServiceProvider _services;
    private readonly SessionService _sesion;
    private List<Notificacion> _lista = new();
    private int _consulta;
    private bool _primeraCarga = true;

    public NotificacionService(IServiceProvider services, SessionService sesion)
    {
        _services = services;
        _sesion = sesion;
    }

    public IReadOnlyList<Notificacion> Notificaciones => _lista;
    public int NoLeidas => _lista.Count(n => !n.Leida);

    /// <summary>La lista cambió (nueva consulta o marcado como leída).</summary>
    public event EventHandler? Cambiado;

    /// <summary>Se detectaron alertas que el usuario no había visto antes.</summary>
    public event EventHandler<NotificacionesNuevasEventArgs>? Nuevas;

    /// <summary>Primera consulta de la sesión; la llama MainForm al mostrarse.</summary>
    public void Iniciar()
    {
        _primeraCarga = true;
        Refrescar();
    }

    public void Detener()
    {
        _consulta++;
        _lista = new();
        Cambiado?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Vuelve a revisar el stock mínimo; se llama después de cada operación que mueve stock.</summary>
    public void Refrescar() => _ = RefrescarAsync();

    public async Task RefrescarAsync()
    {
        if (!_sesion.Tiene(Permisos.InventarioVer)) return;
        // Si llegan dos refrescos seguidos (ej. venta + abrir campanita), solo cuenta la respuesta más reciente.
        var consulta = ++_consulta;
        List<InventarioDto> inventario;
        try
        {
            inventario = await _services.GetRequiredService<InventarioApi>().ListarAsync(sucursalId: _sesion.SucursalId);
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or TaskCanceledException)
        {
            // Un fallo no debe interrumpir al usuario: se reintenta en la siguiente operación.
            return;
        }
        if (consulta != _consulta || _sesion.Usuario is null) return;
        Procesar(inventario);
    }

    public void MarcarLeida(string id) => ActualizarLeidas(n => n.Id == id);

    public void MarcarTodasLeidas() => ActualizarLeidas(_ => true);

    private void Procesar(List<InventarioDto> inventario)
    {
        var guardado = LeerEstado();
        var nuevas = new List<Notificacion>();

        var lista = inventario.Where(i => i.BajoMinimo).Select(item =>
        {
            var id = $"stock-{item.ProductoId}-{item.SucursalId}";
            var previo = guardado.GetValueOrDefault(id);
            var sinStock = item.StockActual <= 0;
            var notificacion = new Notificacion(
                id,
                item.ProductoId,
                item.ProductoNombre,
                sinStock,
                sinStock
                    ? $"se agotó (mínimo {Formatters.Cantidad(item.StockMinimo)})."
                    : $"llegó a su stock mínimo: quedan {Formatters.Cantidad(item.StockActual)} (mínimo {Formatters.Cantidad(item.StockMinimo)}).",
                previo?.FechaUtc ?? DateTime.UtcNow,
                previo?.Leida ?? false);
            if (previo is null) nuevas.Add(notificacion);
            return notificacion;
        }).OrderByDescending(n => n.FechaUtc).ToList();

        _lista = lista;
        GuardarEstado();
        Cambiado?.Invoke(this, EventArgs.Empty);
        if (nuevas.Count > 0) Nuevas?.Invoke(this, new NotificacionesNuevasEventArgs(nuevas, _primeraCarga));
        _primeraCarga = false;
    }

    private void ActualizarLeidas(Func<Notificacion, bool> debeMarcar)
    {
        _lista = _lista.Select(n => debeMarcar(n) ? n with { Leida = true } : n).ToList();
        GuardarEstado();
        Cambiado?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Estado persistido por notificación: cuándo se detectó por primera vez y si ya se leyó.</summary>
    [System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)] // System.Text.Json lo mapea por nombre de propiedad
    private sealed record EstadoGuardado(DateTime FechaUtc, bool Leida);

    private string RutaEstado => Path.Combine(AppPaths.DatosUsuario, $"notificaciones.{_sesion.Usuario?.Id.ToString() ?? "anonimo"}.json");

    private Dictionary<string, EstadoGuardado> LeerEstado()
    {
        try
        {
            if (!File.Exists(RutaEstado)) return new();
            return JsonSerializer.Deserialize<Dictionary<string, EstadoGuardado>>(File.ReadAllText(RutaEstado)) ?? new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new();
        }
    }

    private void GuardarEstado()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DatosUsuario);
            var estado = _lista.ToDictionary(n => n.Id, n => new EstadoGuardado(n.FechaUtc, n.Leida));
            File.WriteAllText(RutaEstado, JsonSerializer.Serialize(estado));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Sin disco disponible las notificaciones siguen funcionando en memoria.
        }
    }
}
