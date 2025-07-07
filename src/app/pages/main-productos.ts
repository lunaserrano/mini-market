import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { Dialog } from 'primeng/dialog';
import { InputText } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { InputNumberModule } from 'primeng/inputnumber';
import { Button } from 'primeng/button';
import { CardModule } from 'primeng/card';

interface Producto {
    nombre: string;
    marca: string;
    descripcion: string;
    precio: number;
}

@Component({
    selector: 'app-main-productos',
    standalone: true,
    imports: [CommonModule, FormsModule, TableModule, Dialog, InputText, Button, TextareaModule, InputNumberModule, CardModule],
    template: `
    <div class="p-4">
      
        <h2 class="text-xl font-bold mb-4 text-primary">Productos</h2>
        <div class="flex justify-between items-center mb-2">
          <span class="font-semibold">Lista de productos</span>
          <p-button type="button" label="Registrar producto" icon="pi pi-plus" class="p-button-primary" (click)="showDialog = true"></p-button>
        </div>
        <p-table [value]="productos" class="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th>Nombre</th>
              <th>Marca</th>
              <th>Descripción</th>
              <th>Precio unitario</th>
              <th class="text-center">Acciones</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-producto>
            <tr>
              <td>{{ producto.nombre }}</td>
              <td>{{ producto.marca }}</td>
              <td>{{ producto.descripcion }}</td>
              <td>{{ producto.precio | currency:'USD' }}</td>
              <td class="flex gap-2 justify-center">
                <p-button icon="pi pi-pencil" class="p-button-sm p-button-text p-button-info" (onClick)="editarProducto(producto)" ariaLabel="Editar"></p-button>
                <p-button icon="pi pi-trash" class="p-button-sm p-button-text p-button-danger" (onClick)="eliminarProducto(producto)" ariaLabel="Eliminar"></p-button>
              </td>
            </tr>
          </ng-template>
        </p-table>

        <p-dialog header="Registrar producto" [(visible)]="showDialog" [modal]="true" [style]="{width: '40rem'}" [closable]="true" (onHide)="resetForm()">
          <form (ngSubmit)="registrarProducto()" #form="ngForm" class="p-fluid">
            <p-card class="mb-4">
              <div class="mb-3 w-full">
                <label for="nombre" class="block mb-1 font-medium">Nombre</label>
                <input pInputText id="nombre" name="nombre" [(ngModel)]="nuevoProducto.nombre" required class="w-full" />
              </div>
              <div class="mb-3 w-full">
                <label for="marca" class="block mb-1 font-medium">Marca</label>
                <input pInputText id="marca" name="marca" [(ngModel)]="nuevoProducto.marca" required class="w-full" />
              </div>
              <div class="mb-3 w-full">
                <label for="descripcion" class="block mb-1 font-medium">Descripción</label>
                <textarea pTextarea id="descripcion" name="descripcion" [(ngModel)]="nuevoProducto.descripcion" required class="w-full" rows="3"></textarea>
              </div>
              <div class="mb-4 w-full">
                <label for="precio" class="block mb-1 font-medium">Precio unitario</label>
                <p-inputnumber [(ngModel)]="nuevoProducto.precio" inputId="currency-us" mode="currency" class="w-full" currency="USD" locale="en-US"></p-inputnumber>
              </div>
            </p-card>
            <div class="flex justify-end gap-2 mt-2">
              <p-button type="button" label="Cancelar" class="p-button-secondary" (onClick)="showDialog = false"></p-button>
              <p-button type="submit" label="Registrar" class="p-button-primary" [disabled]="form.invalid"></p-button>
            </div>
          </form>
        </p-dialog>
      </div>
  `
})
export class MainProductosComponent {
    productos: Producto[] = [
        { nombre: 'Laptop', marca: 'Dell', descripcion: 'Portátil de alto rendimiento', precio: 1200 },
        { nombre: 'Mouse', marca: 'Logitech', descripcion: 'Mouse inalámbrico', precio: 25 },
    ];

    showDialog = false;

    nuevoProducto: Producto = { nombre: '', marca: '', descripcion: '', precio: 0 };

    registrarProducto() {
        this.productos.push({ ...this.nuevoProducto });
        this.nuevoProducto = { nombre: '', marca: '', descripcion: '', precio: 0 };
        this.showDialog = false;
    }

    resetForm() {
        this.nuevoProducto = { nombre: '', marca: '', descripcion: '', precio: 0 };
    }

    // Métodos para editar y eliminar
    editarProducto(producto: Producto) {
        // Aquí puedes abrir el diálogo y cargar los datos para editar
        this.nuevoProducto = { ...producto };
        this.showDialog = true;
    }

    eliminarProducto(producto: Producto) {
        this.productos = this.productos.filter(p => p !== producto);
    }
}
