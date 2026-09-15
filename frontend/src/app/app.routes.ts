import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';
import { AppLayoutComponent } from './layout/app-layout.component';

const ADMIN_SUPERVISOR = ['admin', 'supervisor'];
const TODOS = ['admin', 'supervisor', 'cajero'];

export const routes: Routes = [
  {
    path: 'auth/login',
    loadComponent: () => import('./features/auth/login.component').then((m) => m.LoginComponent)
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
      { path: '', pathMatch: 'full', redirectTo: 'pos' },
      {
        path: 'pos',
        canActivate: [roleGuard(TODOS)],
        loadComponent: () => import('./features/pos-ventas/pos-ventas.component').then((m) => m.PosVentasComponent)
      },
      {
        path: 'caja',
        canActivate: [roleGuard(TODOS)],
        loadComponent: () => import('./features/caja/caja.component').then((m) => m.CajaComponent)
      },
      {
        path: 'ventas',
        canActivate: [roleGuard(TODOS)],
        loadComponent: () => import('./features/ventas/ventas.component').then((m) => m.VentasComponent)
      },
      {
        path: 'productos',
        canActivate: [roleGuard(ADMIN_SUPERVISOR)],
        loadComponent: () => import('./features/productos/productos.component').then((m) => m.ProductosComponent)
      },
      {
        path: 'categorias',
        canActivate: [roleGuard(ADMIN_SUPERVISOR)],
        loadComponent: () => import('./features/categorias/categorias.component').then((m) => m.CategoriasComponent)
      },
      {
        path: 'inventario',
        canActivate: [roleGuard(ADMIN_SUPERVISOR)],
        loadComponent: () => import('./features/inventario/inventario.component').then((m) => m.InventarioComponent)
      },
      {
        path: 'proveedores',
        canActivate: [roleGuard(ADMIN_SUPERVISOR)],
        loadComponent: () => import('./features/proveedores/proveedores.component').then((m) => m.ProveedoresComponent)
      },
      {
        path: 'compras',
        canActivate: [roleGuard(ADMIN_SUPERVISOR)],
        loadComponent: () => import('./features/compras/compras.component').then((m) => m.ComprasComponent)
      },
      {
        path: 'clientes',
        canActivate: [roleGuard(ADMIN_SUPERVISOR)],
        loadComponent: () => import('./features/clientes/clientes.component').then((m) => m.ClientesComponent)
      },
      {
        path: 'usuarios',
        canActivate: [roleGuard(['admin'])],
        loadComponent: () => import('./features/usuarios/usuarios.component').then((m) => m.UsuariosComponent)
      },
      {
        path: 'configuracion',
        canActivate: [roleGuard(['admin'])],
        loadComponent: () => import('./features/configuracion/configuracion.component').then((m) => m.ConfiguracionComponent)
      }
    ]
  },
  {
    path: '**',
    loadComponent: () => import('./features/auth/not-found.component').then((m) => m.NotFoundComponent)
  }
];
