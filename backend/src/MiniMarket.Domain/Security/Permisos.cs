namespace MiniMarket.Domain.Security;

public sealed record PermisoDefinicion(string Codigo, string Modulo, string Nombre);

/// <summary>
/// Catálogo de permisos del sistema, en formato "modulo.accion". Fuente de verdad en código:
/// PermisoCatalogSync lo sincroniza con la tabla Permiso en cada arranque, y los controllers lo
/// referencian con [HasPermission(Permisos.X)]. Agregar un permiso = agregar la constante y la
/// definición en <see cref="Catalogo"/>; el rol admin lo recibe automáticamente.
/// </summary>
public static class Permisos
{
    public const string VentasVer = "ventas.ver";
    public const string VentasCrear = "ventas.crear";
    public const string VentasAnular = "ventas.anular";
    public const string VentasVerTodas = "ventas.ver_todas";

    public const string CajaOperar = "caja.operar";
    public const string CajaVerTodas = "caja.ver_todas";

    public const string ProductosVer = "productos.ver";
    public const string ProductosGestionar = "productos.gestionar";
    public const string ProductosEliminar = "productos.eliminar";

    public const string CategoriasVer = "categorias.ver";
    public const string CategoriasGestionar = "categorias.gestionar";
    public const string CategoriasEliminar = "categorias.eliminar";

    public const string ClientesVer = "clientes.ver";
    public const string ClientesGestionar = "clientes.gestionar";
    public const string ClientesEliminar = "clientes.eliminar";

    public const string ProveedoresVer = "proveedores.ver";
    public const string ProveedoresGestionar = "proveedores.gestionar";
    public const string ProveedoresEliminar = "proveedores.eliminar";

    public const string CreditosVer = "creditos.ver";
    public const string CreditosAbonar = "creditos.abonar";
    public const string CreditosOtorgar = "creditos.otorgar";

    public const string InventarioConsultar = "inventario.consultar";
    public const string InventarioVer = "inventario.ver";
    public const string InventarioAjustar = "inventario.ajustar";

    public const string ComprasVer = "compras.ver";
    public const string ComprasCrear = "compras.crear";
    public const string ComprasAnular = "compras.anular";

    public const string EmpresaEditar = "empresa.editar";

    public const string UsuariosVer = "usuarios.ver";
    public const string UsuariosCrear = "usuarios.crear";
    public const string UsuariosEditar = "usuarios.editar";
    public const string UsuariosCambiarEstado = "usuarios.cambiar_estado";
    public const string UsuariosResetPassword = "usuarios.reset_password";
    public const string UsuariosDesbloquear = "usuarios.desbloquear";

    public const string RolesVer = "roles.ver";
    public const string RolesGestionar = "roles.gestionar";

    public const string AuditoriaVer = "auditoria.ver";

    public static readonly IReadOnlyList<PermisoDefinicion> Catalogo = new PermisoDefinicion[]
    {
        new(VentasVer, "Ventas", "Ver ventas"),
        new(VentasCrear, "Ventas", "Registrar ventas"),
        new(VentasAnular, "Ventas", "Anular ventas"),
        new(VentasVerTodas, "Ventas", "Ver las ventas de todos los usuarios"),

        new(CajaOperar, "Caja", "Abrir, cerrar y registrar movimientos de caja"),
        new(CajaVerTodas, "Caja", "Ver las cajas de todos los usuarios"),

        new(ProductosVer, "Productos", "Ver productos"),
        new(ProductosGestionar, "Productos", "Crear y editar productos y precios"),
        new(ProductosEliminar, "Productos", "Desactivar productos"),

        new(CategoriasVer, "Categorías", "Ver categorías"),
        new(CategoriasGestionar, "Categorías", "Crear y editar categorías"),
        new(CategoriasEliminar, "Categorías", "Desactivar categorías"),

        new(ClientesVer, "Clientes", "Ver clientes"),
        new(ClientesGestionar, "Clientes", "Crear y editar clientes"),
        new(ClientesEliminar, "Clientes", "Desactivar clientes"),

        new(ProveedoresVer, "Proveedores", "Ver proveedores"),
        new(ProveedoresGestionar, "Proveedores", "Crear y editar proveedores"),
        new(ProveedoresEliminar, "Proveedores", "Desactivar proveedores"),

        new(CreditosVer, "Créditos", "Ver créditos y sus abonos"),
        new(CreditosAbonar, "Créditos", "Registrar abonos a créditos"),
        new(CreditosOtorgar, "Créditos", "Vender a crédito"),

        new(InventarioConsultar, "Inventario", "Consultar existencias de un producto"),
        new(InventarioVer, "Inventario", "Ver inventario y movimientos"),
        new(InventarioAjustar, "Inventario", "Ajustar existencias"),

        new(ComprasVer, "Compras", "Ver compras"),
        new(ComprasCrear, "Compras", "Registrar compras"),
        new(ComprasAnular, "Compras", "Anular compras"),

        new(EmpresaEditar, "Configuración", "Editar la configuración de la empresa"),

        new(UsuariosVer, "Usuarios", "Ver usuarios"),
        new(UsuariosCrear, "Usuarios", "Crear usuarios"),
        new(UsuariosEditar, "Usuarios", "Editar usuarios y revocar sus sesiones"),
        new(UsuariosCambiarEstado, "Usuarios", "Activar y desactivar usuarios"),
        new(UsuariosResetPassword, "Usuarios", "Restablecer contraseñas"),
        new(UsuariosDesbloquear, "Usuarios", "Desbloquear cuentas"),

        new(RolesVer, "Roles y permisos", "Ver roles y permisos"),
        new(RolesGestionar, "Roles y permisos", "Crear, editar y eliminar roles"),

        new(AuditoriaVer, "Auditoría", "Consultar la auditoría de seguridad"),
    };

    public static readonly IReadOnlyList<string> Todos = Catalogo.Select(p => p.Codigo).ToArray();

    private static readonly HashSet<string> TodosSet = new(Todos, StringComparer.Ordinal);

    public static bool Existe(string codigo) => TodosSet.Contains(codigo);

    /// <summary>
    /// Permisos iniciales de los roles de sistema no administradores al crear una empresa nueva
    /// (DataSeeder). Debe coincidir con lo sembrado por database/migrations/0005_seguridad.sql para
    /// las empresas existentes. El rol admin no aparece: siempre tiene todos los permisos.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> PorDefecto =
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["supervisor"] = new[]
            {
                VentasVer, VentasCrear, VentasAnular, VentasVerTodas,
                CajaOperar, CajaVerTodas,
                ProductosVer, ProductosGestionar,
                CategoriasVer, CategoriasGestionar,
                ClientesVer, ClientesGestionar,
                CreditosVer, CreditosAbonar, CreditosOtorgar,
                ProveedoresVer, ProveedoresGestionar,
                InventarioConsultar, InventarioVer, InventarioAjustar,
                ComprasVer, ComprasCrear, ComprasAnular
            },
            ["cajero"] = new[]
            {
                VentasVer, VentasCrear,
                CajaOperar,
                CreditosVer, CreditosAbonar, CreditosOtorgar,
                ProductosVer,
                InventarioConsultar
            }
        };
}
