namespace MiniMarket.Desktop.Services;

/// <summary>
/// Códigos de permiso. Espejo de backend/src/MiniMarket.Domain/Security/Permisos.cs (y de
/// frontend/src/app/core/security/permisos.ts). La UI los usa solo para mostrar/ocultar opciones:
/// la autorización real la hace la Api con [HasPermission].
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
}
