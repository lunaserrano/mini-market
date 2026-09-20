import { PERMISOS } from '../core/security/permisos';

export interface MenuItem {
  label: string;
  icon: string;
  route: string;
  /** La entrada se muestra si el usuario tiene AL MENOS UNO de estos permisos. */
  permisos: string[];
  /** Encabezado de sección; se pinta antes de la primera entrada visible de cada sección. */
  seccion?: string;
}

/**
 * Menú lateral: cada entrada declara los permisos que la habilitan (filtrado en AppLayoutComponent).
 * El orden importa: la primera entrada permitida es la pantalla de inicio del usuario (ver rutaInicial).
 */
export const MENU_ITEMS: MenuItem[] = [
  { label: 'Punto de venta', icon: 'pi pi-shopping-cart', route: '/pos', permisos: [PERMISOS.VentasCrear] },
  { label: 'Caja', icon: 'pi pi-wallet', route: '/caja', permisos: [PERMISOS.CajaOperar] },
  { label: 'Ventas', icon: 'pi pi-receipt', route: '/ventas', permisos: [PERMISOS.VentasVer] },
  { label: 'Productos', icon: 'pi pi-box', route: '/productos', permisos: [PERMISOS.ProductosGestionar] },
  { label: 'Categorías', icon: 'pi pi-tags', route: '/categorias', permisos: [PERMISOS.CategoriasVer] },
  { label: 'Inventario', icon: 'pi pi-warehouse', route: '/inventario', permisos: [PERMISOS.InventarioVer] },
  { label: 'Proveedores', icon: 'pi pi-truck', route: '/proveedores', permisos: [PERMISOS.ProveedoresVer] },
  { label: 'Compras', icon: 'pi pi-shopping-bag', route: '/compras', permisos: [PERMISOS.ComprasVer] },
  { label: 'Clientes', icon: 'pi pi-users', route: '/clientes', permisos: [PERMISOS.ClientesVer] },
  { label: 'Créditos', icon: 'pi pi-credit-card', route: '/creditos', permisos: [PERMISOS.CreditosVer] },
  { label: 'Configuración', icon: 'pi pi-cog', route: '/configuracion', permisos: [PERMISOS.EmpresaEditar] },
  { label: 'Usuarios', icon: 'pi pi-user-edit', route: '/usuarios', permisos: [PERMISOS.UsuariosVer], seccion: 'Seguridad' },
  { label: 'Roles y permisos', icon: 'pi pi-shield', route: '/roles', permisos: [PERMISOS.RolesVer], seccion: 'Seguridad' },
  { label: 'Auditoría', icon: 'pi pi-history', route: '/auditoria', permisos: [PERMISOS.AuditoriaVer], seccion: 'Seguridad' }
];
