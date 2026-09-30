using MiniMarket.Desktop.Forms.Auditoria;
using MiniMarket.Desktop.Forms.Auth;
using MiniMarket.Desktop.Forms.Caja;
using MiniMarket.Desktop.Forms.Catalogos;
using MiniMarket.Desktop.Forms.Compras;
using MiniMarket.Desktop.Forms.Configuracion;
using MiniMarket.Desktop.Forms.Creditos;
using MiniMarket.Desktop.Forms.Inventario;
using MiniMarket.Desktop.Forms.Pos;
using MiniMarket.Desktop.Forms.Productos;
using MiniMarket.Desktop.Forms.Roles;
using MiniMarket.Desktop.Forms.Usuarios;
using MiniMarket.Desktop.Forms.Ventas;

namespace MiniMarket.Desktop.Forms;

/// <summary>
/// Ventana principal MDI. Cada opción de menú se muestra solo si el usuario tiene alguno de los
/// permisos que exige el endpoint correspondiente (mismos permisos que las rutas del frontend web).
/// </summary>
public sealed class MainForm : Form
{
    private readonly IServiceProvider _services;
    private readonly SessionService _sesion;
    private readonly ApiHealthMonitor _salud;
    private readonly NotificacionService _notificaciones;

    private readonly ToolStripStatusLabel _lblServicio = new() { Text = "● Verificando base de datos..." };
    private readonly ToolStripStatusLabel _lblReloj = new();
    private readonly System.Windows.Forms.Timer _reloj = new() { Interval = 1000 };
    private readonly ToolStripMenuItem _campana = new("🔔") { Alignment = ToolStripItemAlignment.Right, ToolTipText = "Notificaciones" };

    /// <summary>Máximo de notificaciones listadas en la campanita; el resto se ve en Existencias.</summary>
    private const int MaxNotificacionesMenu = 15;

    public bool CerrarSesionSolicitado { get; private set; }

    public MainForm(IServiceProvider services, SessionService sesion, ApiHealthMonitor salud, NotificacionService notificaciones)
    {
        _services = services;
        _sesion = sesion;
        _salud = salud;
        _notificaciones = notificaciones;

        Theme.Aplicar(this);
        IsMdiContainer = true;
        WindowState = FormWindowState.Maximized;
        Text = $"MiniMarket POS — {_sesion.Empresa?.Nombre}";
        MinimumSize = new Size(1024, 700);

        var menu = ConstruirMenu();
        MainMenuStrip = menu;
        Controls.Add(menu);
        Controls.Add(ConstruirBarraEstado());

        _sesion.SesionExpirada += OnSesionExpirada;
        _salud.EstadoCambiado += OnEstadoServicio;
        _notificaciones.Cambiado += OnNotificacionesCambiadas;
        _notificaciones.Nuevas += OnNotificacionesNuevas;
        _reloj.Tick += (_, _) => _lblReloj.Text = DateTime.Now.ToString("dddd dd/MM/yyyy  HH:mm");
        _reloj.Start();
    }

    private MenuStrip ConstruirMenu()
    {
        var menu = new MenuStrip { Font = Theme.Fuente, Padding = new Padding(6, 4, 0, 4) };

        menu.Items.Add(Grupo("&Operación",
            Opcion<PosForm>("Punto de venta", Keys.F9, Permisos.VentasCrear),
            Opcion<CajaForm>("Caja", Keys.F8, Permisos.CajaOperar, Permisos.CajaVerTodas),
            Opcion<VentasForm>("Ventas", Keys.None, Permisos.VentasVer),
            Opcion<CreditosForm>("Créditos", Keys.None, Permisos.CreditosVer)));

        menu.Items.Add(Grupo("&Catálogos",
            Opcion<ProductosForm>("Productos", Keys.None, Permisos.ProductosVer),
            Opcion<CategoriasForm>("Categorías", Keys.None, Permisos.CategoriasVer),
            Opcion<ClientesForm>("Clientes", Keys.None, Permisos.ClientesVer),
            Opcion<ProveedoresForm>("Proveedores", Keys.None, Permisos.ProveedoresVer)));

        menu.Items.Add(Grupo("&Inventario",
            Opcion<InventarioForm>("Existencias", Keys.None, Permisos.InventarioVer),
            Opcion<MovimientosInventarioForm>("Movimientos de inventario", Keys.None, Permisos.InventarioVer),
            Opcion<ComprasForm>("Compras", Keys.None, Permisos.ComprasVer)));

        menu.Items.Add(Grupo("&Administración",
            Opcion<UsuariosForm>("Usuarios", Keys.None, Permisos.UsuariosVer),
            Opcion<RolesForm>("Roles y permisos", Keys.None, Permisos.RolesVer),
            Opcion<AuditoriaForm>("Auditoría", Keys.None, Permisos.AuditoriaVer),
            Opcion<EmpresaForm>("Configuración de la empresa", Keys.None, Permisos.EmpresaEditar)));

        var sistema = new ToolStripMenuItem("&Sistema");
        sistema.DropDownItems.Add("Cambiar contraseña...", null, (_, _) =>
        {
            using var dlg = ActivatorUtilities.CreateInstance<CambiarPasswordForm>(_services, false);
            dlg.ShowDialog(this);
        });
        sistema.DropDownItems.Add("Conexión...", null, (_, _) =>
        {
            using var dlg = new ConexionForm(reiniciarAlGuardar: true);
            dlg.ShowDialog(this);
        });
        sistema.DropDownItems.Add(new ToolStripSeparator());
        sistema.DropDownItems.Add("Cerrar sesión", null, (_, _) => CerrarSesion());
        sistema.DropDownItems.Add("Salir", null, (_, _) => Close());
        menu.Items.Add(sistema);

        var ventanas = new ToolStripMenuItem("&Ventanas");
        menu.MdiWindowListItem = ventanas;
        ventanas.DropDownItems.Add("Cerrar todas", null, (_, _) => { foreach (var f in MdiChildren) f.Close(); });
        menu.Items.Add(ventanas);

        // Campanita de alertas de stock mínimo (a la derecha del menú), igual que en la versión web.
        _campana.Visible = _sesion.Tiene(Permisos.InventarioVer);
        _campana.Font = new Font(Theme.Fuente.FontFamily, 11F);
        // Al abrirla se vuelve a consultar: cubre los movimientos hechos desde otras cajas.
        _campana.DropDownOpening += (_, _) => _notificaciones.Refrescar();
        ConstruirCampana();
        menu.Items.Add(_campana);

        return menu;
    }

    /// <summary>Grupo de menú; se oculta si el usuario no puede ver ninguna de sus opciones.</summary>
    private static ToolStripMenuItem Grupo(string texto, params ToolStripMenuItem?[] opciones)
    {
        var grupo = new ToolStripMenuItem(texto);
        foreach (var o in opciones.Where(o => o is not null)) grupo.DropDownItems.Add(o!);
        grupo.Visible = grupo.DropDownItems.Count > 0;
        return grupo;
    }

    private ToolStripMenuItem? Opcion<TForm>(string texto, Keys atajo, params string[] permisos) where TForm : ChildForm
    {
        if (!_sesion.Tiene(permisos)) return null;
        var item = new ToolStripMenuItem(texto, null, (_, _) => Abrir<TForm>()) { ShortcutKeys = atajo };
        return item;
    }

    /// <summary>Abre (o trae al frente) una pantalla. Una sola instancia por tipo.</summary>
    public TForm Abrir<TForm>() where TForm : ChildForm
    {
        var existente = MdiChildren.OfType<TForm>().FirstOrDefault();
        if (existente is not null)
        {
            existente.Activate();
            return existente;
        }

        var form = ActivatorUtilities.CreateInstance<TForm>(_services);
        form.MdiParent = this;
        form.Show();
        _services.GetRequiredService<NavegacionAuditor>().Registrar(form.Ruta, form.Text);
        return form;
    }

    private void ConstruirCampana()
    {
        var lista = _notificaciones.Notificaciones;
        var noLeidas = _notificaciones.NoLeidas;
        _campana.Text = noLeidas > 0 ? $"🔔 {noLeidas}" : "🔔";
        _campana.ForeColor = noLeidas > 0 ? Theme.Peligro : Color.Black;
        _campana.ToolTipText = noLeidas switch
        {
            0 => "Notificaciones",
            1 => "1 notificación sin leer",
            _ => $"{noLeidas} notificaciones sin leer"
        };

        var items = _campana.DropDownItems;
        items.Clear();
        items.Add(new ToolStripMenuItem("Notificaciones") { Enabled = false, Font = Theme.FuenteNegrita });
        if (noLeidas > 0) items.Add("Marcar todas como leídas", null, (_, _) => _notificaciones.MarcarTodasLeidas());
        items.Add(new ToolStripSeparator());

        if (lista.Count == 0)
            items.Add(new ToolStripMenuItem("No hay notificaciones: el stock está por encima del mínimo.") { Enabled = false });

        foreach (var n in lista.Take(MaxNotificacionesMenu))
        {
            var item = new ToolStripMenuItem($"{(n.Leida ? "    " : "●  ")}{n.Texto}   ·   {Relativo(n.FechaUtc)}")
            {
                Font = n.Leida ? Theme.Fuente : Theme.FuenteNegrita,
                ForeColor = n.SinStock ? Theme.Peligro : Color.Black,
                ToolTipText = "Ver en Existencias"
            };
            item.Click += (_, _) =>
            {
                _notificaciones.MarcarLeida(n.Id);
                Abrir<InventarioForm>().MostrarBajoMinimo(n.ProductoNombre);
            };
            items.Add(item);
        }

        if (lista.Count > 0)
        {
            items.Add(new ToolStripSeparator());
            var resto = lista.Count - MaxNotificacionesMenu;
            items.Add(resto > 0 ? $"Ver todos en Existencias ({resto} más)..." : "Ver todos en Existencias...", null,
                (_, _) => Abrir<InventarioForm>().MostrarBajoMinimo());
        }
    }

    private static string Relativo(DateTime utc)
    {
        var transcurrido = DateTime.UtcNow - utc;
        if (transcurrido.TotalMinutes < 1) return "hace un momento";
        if (transcurrido.TotalHours < 1) return $"hace {(int)transcurrido.TotalMinutes} min";
        if (transcurrido.TotalDays < 1) return $"hace {(int)transcurrido.TotalHours} h";
        return Formatters.Fecha(utc);
    }

    private void OnNotificacionesCambiadas(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        ConstruirCampana();
    }

    private void OnNotificacionesNuevas(object? sender, NotificacionesNuevasEventArgs e)
    {
        if (IsDisposed || !Visible) return;
        var mensaje = e.Nuevas.Count == 1
            ? e.Nuevas[0].Texto
            : $"{e.Nuevas.Count} productos {(e.PrimeraCarga ? "están" : "llegaron")} en su stock mínimo. Revise las notificaciones.";
        Toast.Mostrar(this, "Stock mínimo", mensaje, () => _campana.ShowDropDown());
    }

    private StatusStrip ConstruirBarraEstado()
    {
        var u = _sesion.Usuario!;
        var barra = new StatusStrip { Font = Theme.Fuente, SizingGrip = false };
        barra.Items.Add(new ToolStripStatusLabel($"👤 {u.NombreCompleto} ({u.Username})"));
        barra.Items.Add(new ToolStripStatusLabel($"Rol: {u.RolNombre}") { BorderSides = ToolStripStatusLabelBorderSides.Left });
        barra.Items.Add(new ToolStripStatusLabel($"Sucursal #{u.SucursalId?.ToString() ?? "—"}") { BorderSides = ToolStripStatusLabelBorderSides.Left });
        barra.Items.Add(new ToolStripStatusLabel { Spring = true });
        barra.Items.Add(_lblServicio);
        _lblReloj.BorderSides = ToolStripStatusLabelBorderSides.Left;
        barra.Items.Add(_lblReloj);
        return barra;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // El login ya verificó la base de datos: el evento solo avisa cambios, así que se pinta el estado actual.
        OnEstadoServicio(this, _salud.Estado);
        _salud.Iniciar();
        _notificaciones.Iniciar();
        // El cajero entra directo al punto de venta.
        if (_sesion.Tiene(Permisos.VentasCrear)) Abrir<PosForm>();
    }

    private void OnEstadoServicio(object? sender, EstadoServicio estado)
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(() =>
        {
            (_lblServicio.Text, _lblServicio.ForeColor) = estado switch
            {
                EstadoServicio.EnLinea => ("● Base de datos conectada", Theme.Exito),
                EstadoServicio.SinBaseDatos => ("● SQL Server no disponible", Theme.Advertencia),
                EstadoServicio.Detenido => ("● Motor local detenido: reinicie la aplicación", Theme.Peligro),
                _ => ("● Verificando...", Theme.TextoSuave)
            };
        });
    }

    private void OnSesionExpirada(object? sender, EventArgs e)
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(() =>
        {
            Dialogs.Aviso(this, "Su sesión expiró o fue revocada. Inicie sesión nuevamente.");
            CerrarSesionSolicitado = true;
            ForzarCierre();
        });
    }

    private void CerrarSesion()
    {
        if (!Dialogs.Confirmar(this, "¿Desea cerrar la sesión?")) return;
        CerrarSesionSolicitado = true;
        ForzarCierre();
    }

    private bool _forzado;

    private void ForzarCierre()
    {
        _forzado = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_forzado && e.CloseReason == CloseReason.UserClosing && !Dialogs.Confirmar(this, "¿Desea salir de MiniMarket?"))
        {
            e.Cancel = true;
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _salud.Detener();
        _salud.EstadoCambiado -= OnEstadoServicio;
        _notificaciones.Cambiado -= OnNotificacionesCambiadas;
        _notificaciones.Nuevas -= OnNotificacionesNuevas;
        _notificaciones.Detener();
        _sesion.SesionExpirada -= OnSesionExpirada;
        _reloj.Dispose();
        base.OnFormClosed(e);
    }
}
