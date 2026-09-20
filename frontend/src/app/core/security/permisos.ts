/**
 * Códigos de permiso, espejo de MiniMarket.Domain/Security/Permisos.cs. El backend es la fuente de
 * verdad: aquí solo se ocultan pantallas y botones; cada endpoint valida el permiso de nuevo.
 */
export const PERMISOS = {
  VentasVer: 'ventas.ver',
  VentasCrear: 'ventas.crear',
  VentasAnular: 'ventas.anular',
  VentasVerTodas: 'ventas.ver_todas',

  CajaOperar: 'caja.operar',
  CajaVerTodas: 'caja.ver_todas',

  ProductosVer: 'productos.ver',
  ProductosGestionar: 'productos.gestionar',
  ProductosEliminar: 'productos.eliminar',

  CategoriasVer: 'categorias.ver',
  CategoriasGestionar: 'categorias.gestionar',
  CategoriasEliminar: 'categorias.eliminar',

  ClientesVer: 'clientes.ver',
  ClientesGestionar: 'clientes.gestionar',
  ClientesEliminar: 'clientes.eliminar',

  ProveedoresVer: 'proveedores.ver',
  ProveedoresGestionar: 'proveedores.gestionar',
  ProveedoresEliminar: 'proveedores.eliminar',

  CreditosVer: 'creditos.ver',
  CreditosAbonar: 'creditos.abonar',
  CreditosOtorgar: 'creditos.otorgar',

  InventarioConsultar: 'inventario.consultar',
  InventarioVer: 'inventario.ver',
  InventarioAjustar: 'inventario.ajustar',

  ComprasVer: 'compras.ver',
  ComprasCrear: 'compras.crear',
  ComprasAnular: 'compras.anular',

  EmpresaEditar: 'empresa.editar',

  UsuariosVer: 'usuarios.ver',
  UsuariosCrear: 'usuarios.crear',
  UsuariosEditar: 'usuarios.editar',
  UsuariosCambiarEstado: 'usuarios.cambiar_estado',
  UsuariosResetPassword: 'usuarios.reset_password',
  UsuariosDesbloquear: 'usuarios.desbloquear',

  RolesVer: 'roles.ver',
  RolesGestionar: 'roles.gestionar',

  AuditoriaVer: 'auditoria.ver'
} as const;

export type Permiso = (typeof PERMISOS)[keyof typeof PERMISOS];
