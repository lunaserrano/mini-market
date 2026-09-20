import { Routes } from '@angular/router';
import { authGuard, sesionGuard } from './core/guards/auth.guard';
import { inicioGuard, permissionGuard } from './core/guards/permission.guard';
import { PERMISOS } from './core/security/permisos';
import { AppLayoutComponent } from './layout/app-layout.component';

export const routes: Routes = [
  {
    path: 'auth/login',
    loadComponent: () => import('./features/auth/login.component').then((m) => m.LoginComponent)
  },
  {
    // Cambio de contraseña: voluntario (desde el layout) o forzado (tras un alta o restablecimiento por un admin).
    path: 'auth/cambiar-password',
    canActivate: [sesionGuard],
    loadComponent: () => import('./features/auth/cambiar-password.component').then((m) => m.CambiarPasswordComponent)
  },
  {
    path: 'auth/access',
    loadComponent: () => import('./features/auth/access-denied.component').then((m) => m.AccessDeniedComponent)
  },
  {
    path: '',
    component: AppLayoutComponent,
    canActivate: [authGuard],
    children: [
      // "/" lleva a la primera pantalla a la que el usuario tenga acceso (no todos los roles pueden usar el POS).
      { path: '', pathMatch: 'full', canActivate: [inicioGuard], children: [] },
      {
        path: 'pos',
        canActivate: [permissionGuard(PERMISOS.VentasCrear)],
        loadComponent: () => import('./features/pos-ventas/pos-ventas.component').then((m) => m.PosVentasComponent)
      },
      {
        path: 'caja',
        canActivate: [permissionGuard(PERMISOS.CajaOperar)],
        loadComponent: () => import('./features/caja/caja.component').then((m) => m.CajaComponent)
      },
      {
        path: 'ventas',
        canActivate: [permissionGuard(PERMISOS.VentasVer)],
        loadComponent: () => import('./features/ventas/ventas.component').then((m) => m.VentasComponent)
      },
      {
        path: 'productos',
        canActivate: [permissionGuard(PERMISOS.ProductosGestionar)],
        loadComponent: () => import('./features/productos/productos.component').then((m) => m.ProductosComponent)
      },
      {
        path: 'categorias',
        canActivate: [permissionGuard(PERMISOS.CategoriasVer)],
        loadComponent: () => import('./features/categorias/categorias.component').then((m) => m.CategoriasComponent)
      },
      {
        path: 'inventario',
        canActivate: [permissionGuard(PERMISOS.InventarioVer)],
        loadComponent: () => import('./features/inventario/inventario.component').then((m) => m.InventarioComponent)
      },
      {
        path: 'proveedores',
        canActivate: [permissionGuard(PERMISOS.ProveedoresVer)],
        loadComponent: () => import('./features/proveedores/proveedores.component').then((m) => m.ProveedoresComponent)
      },
      {
        path: 'compras',
        canActivate: [permissionGuard(PERMISOS.ComprasVer)],
        loadComponent: () => import('./features/compras/compras.component').then((m) => m.ComprasComponent)
      },
      {
        path: 'clientes',
        canActivate: [permissionGuard(PERMISOS.ClientesVer)],
        loadComponent: () => import('./features/clientes/clientes.component').then((m) => m.ClientesComponent)
      },
      {
        path: 'creditos',
        canActivate: [permissionGuard(PERMISOS.CreditosVer)],
        loadComponent: () => import('./features/creditos/creditos.component').then((m) => m.CreditosComponent)
      },
      {
        path: 'usuarios',
        canActivate: [permissionGuard(PERMISOS.UsuariosVer)],
        loadComponent: () => import('./features/usuarios/usuarios.component').then((m) => m.UsuariosComponent)
      },
      {
        path: 'roles',
        canActivate: [permissionGuard(PERMISOS.RolesVer)],
        loadComponent: () => import('./features/roles/roles.component').then((m) => m.RolesComponent)
      },
      {
        path: 'auditoria',
        canActivate: [permissionGuard(PERMISOS.AuditoriaVer)],
        loadComponent: () => import('./features/auditoria/auditoria.component').then((m) => m.AuditoriaComponent)
      },
      {
        path: 'configuracion',
        canActivate: [permissionGuard(PERMISOS.EmpresaEditar)],
        loadComponent: () => import('./features/configuracion/configuracion.component').then((m) => m.ConfiguracionComponent)
      }
    ]
  },
  {
    path: '**',
    loadComponent: () => import('./features/auth/not-found.component').then((m) => m.NotFoundComponent)
  }
];
