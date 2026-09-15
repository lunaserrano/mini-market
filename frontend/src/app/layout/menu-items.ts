export interface MenuItem {
  label: string;
  icon: string;
  route: string;
  roles: Array<'admin' | 'supervisor' | 'cajero'>;
}

/** Menú lateral: cada entrada declara los roles que pueden verla (filtrado en AppLayoutComponent). */
export const MENU_ITEMS: MenuItem[] = [
  { label: 'Punto de venta', icon: 'pi pi-shopping-cart', route: '/pos', roles: ['admin', 'supervisor', 'cajero'] },
  { label: 'Caja', icon: 'pi pi-wallet', route: '/caja', roles: ['admin', 'supervisor', 'cajero'] },
  { label: 'Ventas', icon: 'pi pi-receipt', route: '/ventas', roles: ['admin', 'supervisor', 'cajero'] },
  { label: 'Productos', icon: 'pi pi-box', route: '/productos', roles: ['admin', 'supervisor'] },
  { label: 'Categorías', icon: 'pi pi-tags', route: '/categorias', roles: ['admin', 'supervisor'] },
  { label: 'Inventario', icon: 'pi pi-warehouse', route: '/inventario', roles: ['admin', 'supervisor'] },
  { label: 'Proveedores', icon: 'pi pi-truck', route: '/proveedores', roles: ['admin', 'supervisor'] },
  { label: 'Compras', icon: 'pi pi-shopping-bag', route: '/compras', roles: ['admin', 'supervisor'] },
  { label: 'Clientes', icon: 'pi pi-users', route: '/clientes', roles: ['admin', 'supervisor'] },
  { label: 'Usuarios', icon: 'pi pi-user-edit', route: '/usuarios', roles: ['admin'] },
  { label: 'Configuración', icon: 'pi pi-cog', route: '/configuracion', roles: ['admin'] }
];
