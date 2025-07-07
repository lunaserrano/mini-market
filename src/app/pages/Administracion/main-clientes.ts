import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { Dialog } from 'primeng/dialog';
import { InputText } from 'primeng/inputtext';
import { Button } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { SpinnerService } from '../../shared/spinner.service';

interface Cliente {
    nombre: string;
    telefono: string;
    direccion: string;
    correo: string;
    puntos?: number;
}

@Component({
    selector: 'app-main-clientes',
    standalone: true,
    imports: [CommonModule, FormsModule, TableModule, Dialog, InputText, Button, CardModule],
    template: `
    <div class="p-4">
      <h2 class="text-xl font-bold mb-4 text-primary">Clientes</h2>
      <div class="flex justify-between items-center mb-2">
        <span class="font-semibold">Lista de clientes</span>
        <p-button type="button" label="Registrar cliente" icon="pi pi-plus" class="p-button-primary" (click)="abrirDialogoNuevo()"></p-button>
      </div>
      <p-table [value]="clientes" class="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th>Nombre</th>
            <th>Teléfono</th>
            <th>Dirección</th>
            <th>Correo</th>
            <th>Puntos</th>
            <th class="text-center">Acciones</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-cliente>
          <tr>
            <td>{{ cliente.nombre }}</td>
            <td>{{ cliente.telefono }}</td>
            <td>{{ cliente.direccion }}</td>
            <td>{{ cliente.correo }}</td>
            <td>{{ cliente.puntos || 0 }}</td>
            <td class="flex gap-2 justify-center">
              <p-button icon="pi pi-eye" class="p-button-sm p-button-text p-button-info" (onClick)="verHistorial(cliente)" ariaLabel="Historial"></p-button>
              <p-button icon="pi pi-pencil" class="p-button-sm p-button-text p-button-info" (onClick)="editarCliente(cliente)" ariaLabel="Editar"></p-button>
              <p-button icon="pi pi-trash" class="p-button-sm p-button-text p-button-danger" (onClick)="confirmarEliminar(cliente)" ariaLabel="Eliminar"></p-button>
            </td>
          </tr>
        </ng-template>
      </p-table>

      <p-dialog header="{{editando ? 'Editar cliente' : 'Registrar cliente'}}" [(visible)]="showDialog" [modal]="true" [style]="{width: '30rem'}" [closable]="true" (onHide)="resetForm()">
        <form (ngSubmit)="guardarCliente()" #form="ngForm" class="p-fluid">
          <p-card class="mb-4">
            <div class="mb-3 w-full">
              <label for="nombre" class="block mb-1 font-medium">Nombre</label>
              <input pInputText id="nombre" name="nombre" [(ngModel)]="nuevoCliente.nombre" required class="w-full" />
            </div>
            <div class="mb-3 w-full">
              <label for="telefono" class="block mb-1 font-medium">Teléfono</label>
              <input pInputText id="telefono" name="telefono" [(ngModel)]="nuevoCliente.telefono" required class="w-full" />
            </div>
            <div class="mb-3 w-full">
              <label for="direccion" class="block mb-1 font-medium">Dirección</label>
              <input pInputText id="direccion" name="direccion" [(ngModel)]="nuevoCliente.direccion" required class="w-full" />
            </div>
            <div class="mb-3 w-full">
              <label for="correo" class="block mb-1 font-medium">Correo</label>
              <input pInputText id="correo" name="correo" [(ngModel)]="nuevoCliente.correo" required class="w-full" />
            </div>
          </p-card>
          <div class="flex justify-end gap-2 mt-2">
            <p-button type="button" label="Cancelar" class="p-button-secondary" (onClick)="showDialog = false"></p-button>
            <p-button type="submit" label="{{editando ? 'Actualizar' : 'Registrar'}}" class="p-button-primary" [disabled]="form.invalid"></p-button>
          </div>
        </form>
      </p-dialog>

      <p-dialog header="Confirmar eliminación" [(visible)]="showDialogEliminar" [modal]="true" [style]="{width: '25rem'}">
        <div>¿Está seguro de que desea eliminar el cliente <b>{{clienteAEliminar?.nombre}}</b>?</div>
        <div class="flex justify-end gap-2 mt-4">
          <p-button type="button" label="Cancelar" class="p-button-secondary" (onClick)="showDialogEliminar = false"></p-button>
          <p-button type="button" label="Eliminar" class="p-button-danger" (onClick)="eliminarClienteConfirmado()"></p-button>
        </div>
      </p-dialog>

      <p-dialog header="Historial de compras" [(visible)]="showDialogHistorial" [modal]="true" [style]="{width: '40rem'}">
        <div *ngIf="clienteHistorial">
          <b>Cliente:</b> {{ clienteHistorial.nombre }}<br>
          <b>Correo:</b> {{ clienteHistorial.correo }}<br>
          <b>Teléfono:</b> {{ clienteHistorial.telefono }}<br>
          <b>Dirección:</b> {{ clienteHistorial.direccion }}
        </div>
        <p-table [value]="historialCompras" class="p-datatable-sm mt-3">
          <ng-template pTemplate="header">
            <tr>
              <th>Fecha</th>
              <th>Total</th>
              <th>Detalle</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-compra>
            <tr>
              <td>{{ compra.fecha | date:'short' }}</td>
              <td>{{ compra.total | currency:'USD' }}</td>
              <td>
                <ul>
                  <li *ngFor="let det of compra.detalles">{{ det.cod_producto }} x{{ det.cantidad }} ({{ det.subtotal | currency:'USD' }})</li>
                </ul>
              </td>
            </tr>
          </ng-template>
        </p-table>
      </p-dialog>
    </div>
  `
})
export class MainClientesComponent {
    clientes: Cliente[] = [];
    showDialog = false;
    showDialogEliminar = false;
    showDialogHistorial = false;
    editando = false;
    clienteEditandoIndex: number | null = null;
    clienteAEliminar: Cliente | null = null;
    nuevoCliente: Cliente = { nombre: '', telefono: '', direccion: '', correo: '' };
    clienteHistorial: Cliente | null = null;
    historialCompras: any[] = [];

    constructor(private spinner: SpinnerService) { }

    ngOnInit() {
        this.spinner.show();
        setTimeout(() => {
            // Simulación de carga de clientes
            this.clientes = [
                { nombre: 'Juan Pérez', telefono: '123456789', direccion: 'Calle Falsa 123', correo: 'juanperez@example.com', puntos: 10 },
                { nombre: 'María Gómez', telefono: '987654321', direccion: 'Avenida Siempre Viva 742', correo: 'mariagomez@example.com', puntos: 20 }
            ];
            this.spinner.hide();
        }, 500);
    }

    abrirDialogoNuevo() {
        this.editando = false;
        this.nuevoCliente = { nombre: '', telefono: '', direccion: '', correo: '' };
        this.showDialog = true;
    }

    guardarCliente() {
        this.spinner.show();
        setTimeout(() => {
            if (this.editando && this.clienteEditandoIndex !== null) {
                this.clientes[this.clienteEditandoIndex] = { ...this.nuevoCliente };
            } else {
                this.clientes.push({ ...this.nuevoCliente });
            }
            this.showDialog = false;
            this.resetForm();
            this.spinner.hide();
        }, 500);
    }

    resetForm() {
        this.nuevoCliente = { nombre: '', telefono: '', direccion: '', correo: '' };
        this.editando = false;
        this.clienteEditandoIndex = null;
    }

    editarCliente(cliente: Cliente) {
        this.editando = true;
        this.clienteEditandoIndex = this.clientes.indexOf(cliente);
        this.nuevoCliente = { ...cliente };
        this.showDialog = true;
    }

    confirmarEliminar(cliente: Cliente) {
        this.clienteAEliminar = cliente;
        this.showDialogEliminar = true;
    }

    eliminarClienteConfirmado() {
        if (this.clienteAEliminar) {
            this.clientes = this.clientes.filter(c => c !== this.clienteAEliminar);
            this.clienteAEliminar = null;
            this.showDialogEliminar = false;
        }
    }

    verHistorial(cliente: Cliente) {
        this.clienteHistorial = cliente;
        // Aquí deberías obtener el historial real de compras del cliente
        this.historialCompras = [];
        this.showDialogHistorial = true;
    }
}
