import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { AppMenuitem } from './app.menuitem';

@Component({
    selector: 'app-menu',
    standalone: true,
    imports: [CommonModule, AppMenuitem, RouterModule],
    template: `<ul class="layout-menu">
        <li class="mb-3 text-center flex flex-col items-center">
            <span class="pi pi-user text-3xl mb-1"></span>
            <span class="font-semibold">¡Hola, {{ userName }}!</span>
        </li>
        <ng-container *ngFor="let item of model; let i = index">
            <li app-menuitem *ngIf="!item.separator" [item]="item" [index]="i" [root]="true"></li>
            <li *ngIf="item.separator" class="menu-separator"></li>
        </ng-container>
        <li class="mt-4 text-center">
            <button pButton type="button" class="p-button-danger w-full" (click)="logout()">Cerrar sesión</button>
        </li>
    </ul> `
})
export class AppMenu {
    model: MenuItem[] = [];
    userRole: string = '';
    userName: string = '';

    ngOnInit() {
        const jwt = localStorage.getItem('jwt');
        if (jwt) {
            const user = JSON.parse(jwt);
            this.userRole = user.rol;
            this.userName = user.nombre || user.name || 'Usuario';
        } else {
            this.userRole = '';
            this.userName = 'Usuario';
        }
        this.model = [
            {
                label: 'Home',
                items: [{ label: 'Inicio', icon: 'pi pi-fw pi-home', routerLink: ['/'] }]
            },
            {
                label: 'Administración',
                items: [
                    ...(this.userRole === 'admin' || this.userRole === 'supervisor' ? [
                        { label: 'Productos', icon: 'pi pi-fw pi-box', routerLink: ['/pages/productos'] },
                        { label: 'Categorías', icon: 'pi pi-fw pi-tags', routerLink: ['/pages/categorias'] },
                        { label: 'Proveedores', icon: 'pi pi-fw pi-truck', routerLink: ['/pages/proveedores'] },
                        { label: 'Inventario', icon: 'pi pi-fw pi-database', routerLink: ['/pages/inventario'] },
                        { label: 'Compras', icon: 'pi pi-fw pi-briefcase', routerLink: ['/pages/compras'] },
                        { label: 'Clientes', icon: 'pi pi-fw pi-users', routerLink: ['/pages/clientes'] }
                    ] : []),
                    { label: 'Ventas', icon: 'pi pi-fw pi-shopping-cart', routerLink: ['/pages/ventas'] },
                    { label: 'Cajas', icon: 'pi pi-fw pi-wallet', routerLink: ['/pages/cajas'] },
                    ...(this.userRole === 'admin' ? [
                        { label: 'Usuarios', icon: 'pi pi-fw pi-user', routerLink: ['/pages/usuarios'] }
                    ] : [])
                ]
            }
        ];
    }

    logout() {
        localStorage.removeItem('jwt');
        window.location.href = '/auth/login';
    }
}
