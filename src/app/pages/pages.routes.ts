import { Routes } from '@angular/router';
import { roleGuard } from './auth/auth.guard';

export default [
    {
        path: '',
        redirectTo: '/auth/login',
        pathMatch: 'full'
    },
    {
        path: 'auth/login',
        loadComponent: () => import('./auth/login').then(m => m.Login)
    },
    {
        path: 'productos',
        canActivate: [roleGuard(['admin', 'supervisor'])],
        loadComponent: () => import('./Administracion/main-productos').then(m => m.MainProductosComponent)
    },
    {
        path: 'categorias',
        canActivate: [roleGuard(['admin', 'supervisor'])],
        loadComponent: () => import('./Administracion/main-categorias').then(m => m.MainCategoriasComponent)
    },
    {
        path: 'proveedores',
        canActivate: [roleGuard(['admin', 'supervisor'])],
        loadComponent: () => import('./Administracion/main-proveedores').then(m => m.MainProveedoresComponent)
    },
    {
        path: 'inventario',
        canActivate: [roleGuard(['admin', 'supervisor'])],
        loadComponent: () => import('./Administracion/main-inventario').then(m => m.MainInventarioComponent)
    },
    {
        path: 'ventas',
        canActivate: [roleGuard(['admin', 'supervisor', 'cajero'])],
        loadComponent: () => import('./Administracion/main-ventas').then(m => m.MainVentasComponent)
    },
    {
        path: 'cajas',
        canActivate: [roleGuard(['admin', 'supervisor', 'cajero'])],
        loadComponent: () => import('./Administracion/main-cajas').then(m => m.MainCajasComponent)
    },
    {
        path: 'compras',
        canActivate: [roleGuard(['admin', 'supervisor'])],
        loadComponent: () => import('./Administracion/main-compras').then(m => m.MainComprasComponent)
    },
    {
        path: 'clientes',
        canActivate: [roleGuard(['admin', 'supervisor'])],
        loadComponent: () => import('./Administracion/main-clientes').then(m => m.MainClientesComponent)
    },
    {
        path: 'usuarios',
        canActivate: [roleGuard(['admin'])],
        loadComponent: () => import('./Administracion/main-usuarios').then(m => m.MainUsuariosComponent)
    },
    { path: '**', redirectTo: '/auth/login' }
] as Routes;
