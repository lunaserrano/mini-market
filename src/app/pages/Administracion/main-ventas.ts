import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { Dialog } from 'primeng/dialog';
import { InputText } from 'primeng/inputtext';
import { Button } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DropdownModule } from 'primeng/dropdown';
import { CalendarModule } from 'primeng/calendar';
import { CajaService } from './main-cajas';
import { SpinnerService } from '../../shared/spinner.service';

interface DetalleVenta {
    cod_producto: string;
    cantidad: number;
    precio_unitario: number;
    subtotal: number;
}

interface Venta {
    cod_venta: string;
    fecha: Date;
    total: number;
    cod_cliente?: string;
    cod_usuario: string;
    cod_caja: string;
    metodo_pago: 'efectivo' | 'tarjeta' | 'mixto';
    detalles: DetalleVenta[];
}

@Component({
    selector: 'app-main-ventas',
    standalone: true,
    imports: [CommonModule, FormsModule, TableModule, Dialog, InputText, Button, CardModule, DropdownModule, CalendarModule],
    template: `
    <div class="p-4">
      <h2 class="text-xl font-bold mb-4 text-primary">Ventas</h2>
      <div class="flex justify-between items-center mb-2">
        <span class="font-semibold">Registro de ventas</span>
        <p-button type="button" label="Nueva venta" icon="pi pi-plus" class="p-button-primary" (click)="abrirDialogoNuevaVenta()"></p-button>
      </div>
      <div class="mb-4 flex gap-2">
        <div class="w-1/2">
          <label class="block mb-1 font-medium">Filtrar por cliente</label>
          <input pInputText [(ngModel)]="filtroCliente" placeholder="Cliente" class="w-full" />
        </div>
        <div class="w-1/2">
          <label class="block mb-1 font-medium">Filtrar por fecha</label>
          <p-calendar [(ngModel)]="filtroFecha" dateFormat="yy-mm-dd" class="w-full"></p-calendar>
        </div>
      </div>
      <p-table [value]="ventasFiltradas" class="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th>Código</th>
            <th>Fecha</th>
            <th>Cliente</th>
            <th>Total</th>
            <th>Método de pago</th>
            <th>Usuario</th>
            <th>Caja</th>
            <th>Acciones</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-venta>
          <tr>
            <td>{{ venta.cod_venta }}</td>
            <td>{{ venta.fecha | date:'short' }}</td>
            <td>{{ venta.cod_cliente || '-' }}</td>
            <td>{{ venta.total | currency:'USD' }}</td>
            <td>{{ venta.metodo_pago | titlecase }}</td>
            <td>{{ venta.cod_usuario }}</td>
            <td>{{ venta.cod_caja }}</td>
            <td>
              <p-button icon="pi pi-eye" class="p-button-sm p-button-text" (onClick)="verDetalleVenta(venta)"></p-button>
              <p-button icon="pi pi-print" class="p-button-sm p-button-text" (onClick)="imprimirTicket(venta)"></p-button>
            </td>
          </tr>
        </ng-template>
      </p-table>

      <p-dialog header="Nueva venta" [(visible)]="showDialogVenta" [modal]="true" [style]="{width: '50rem'}" [closable]="true" (onHide)="resetFormVenta()">
        <form (ngSubmit)="guardarVenta()" #form="ngForm" class="p-fluid">
          <p-card class="mb-4">
            <div class="grid grid-cols-2 gap-4">
              <div>
                <label class="block mb-1 font-medium">Cliente (opcional)</label>
                <input pInputText [(ngModel)]="nuevaVenta.cod_cliente" name="cod_cliente" class="w-full" />
              </div>
              <div>
                <label class="block mb-1 font-medium">Usuario</label>
                <input pInputText [(ngModel)]="nuevaVenta.cod_usuario" name="cod_usuario" required class="w-full" />
              </div>
              <div>
                <label class="block mb-1 font-medium">Caja</label>
                <input pInputText [(ngModel)]="nuevaVenta.cod_caja" name="cod_caja" required class="w-full" />
              </div>
              <div>
                <label class="block mb-1 font-medium">Método de pago</label>
                <p-dropdown [options]="metodosPago" [(ngModel)]="nuevaVenta.metodo_pago" name="metodo_pago" optionLabel="label" optionValue="value" placeholder="Seleccione método" class="w-full"></p-dropdown>
              </div>
            </div>
            <div class="mt-4">
              <h3 class="font-semibold mb-2">Detalle de venta</h3>
              <div class="flex gap-2 mb-2 items-end">
                <p-dropdown [options]="productosOptions" [(ngModel)]="productoSeleccionado" name="productoSeleccionado" optionLabel="nombre" optionValue="nombre" placeholder="Producto" class="w-1/3"></p-dropdown>
                <input pInputText [(ngModel)]="cantidadSeleccionada" name="cantidadSeleccionada" type="number" min="1" placeholder="Cantidad" class="w-1/4" />
                <p-button type="button" label="Agregar" icon="pi pi-plus" class="p-button-success"
                  (onClick)="agregarDetalle()"
                  [disabled]="!productoSeleccionado || cantidadSeleccionada < 1 || detalleYaAgregado()"
                ></p-button>
              </div>
              <div *ngIf="detalleError" class="text-red-600 text-xs mb-2">{{ detalleError }}</div>
              <p-table [value]="nuevaVenta.detalles" class="p-datatable-sm">
                <ng-template pTemplate="header">
                  <tr>
                    <th>Producto</th>
                    <th>Cantidad</th>
                    <th>Precio unitario</th>
                    <th>Subtotal</th>
                    <th>Acciones</th>
                  </tr>
                </ng-template>
                <ng-template pTemplate="body" let-detalle let-i="rowIndex">
                  <tr>
                    <td>{{ detalle.cod_producto }}</td>
                    <td>{{ detalle.cantidad }}</td>
                    <td>{{ detalle.precio_unitario | currency:'USD' }}</td>
                    <td>{{ detalle.subtotal | currency:'USD' }}</td>
                    <td>
                      <p-button icon="pi pi-trash" class="p-button-sm p-button-text p-button-danger" (onClick)="eliminarDetalle(i)"></p-button>
                    </td>
                  </tr>
                </ng-template>
              </p-table>
              <div class="text-right font-bold mt-2">Total: {{ calcularTotalVenta() | currency:'USD' }}</div>
            </div>
          </p-card>
          <div class="flex justify-end gap-2 mt-2">
            <p-button type="button" label="Cancelar" class="p-button-secondary" (onClick)="showDialogVenta = false"></p-button>
            <p-button type="submit" label="Registrar venta" class="p-button-primary" [disabled]="nuevaVenta.detalles.length === 0 || form.invalid"></p-button>
          </div>
        </form>
      </p-dialog>

      <p-dialog header="Detalle de venta" [(visible)]="showDialogDetalle" [modal]="true" [style]="{width: '40rem'}">
        <p-table [value]="ventaDetalleSeleccionada?.detalles || []" class="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th>Producto</th>
              <th>Cantidad</th>
              <th>Precio unitario</th>
              <th>Subtotal</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-detalle>
            <tr>
              <td>{{ detalle.cod_producto }}</td>
              <td>{{ detalle.cantidad }}</td>
              <td>{{ detalle.precio_unitario | currency:'USD' }}</td>
              <td>{{ detalle.subtotal | currency:'USD' }}</td>
            </tr>
          </ng-template>
        </p-table>
        <div class="text-right font-bold mt-2">Total: {{ ventaDetalleSeleccionada?.total | currency:'USD' }}</div>
      </p-dialog>
    </div>
  `
})
export class MainVentasComponent {
    ventas: Venta[] = [];
    productosOptions = [
        { nombre: 'Laptop', precio: 1200 },
        { nombre: 'Mouse', precio: 25 }
    ];
    metodosPago = [
        { label: 'Efectivo', value: 'efectivo' },
        { label: 'Tarjeta', value: 'tarjeta' },
        { label: 'Mixto', value: 'mixto' }
    ];
    filtroCliente = '';
    filtroFecha: Date | null = null;
    showDialogVenta = false;
    showDialogDetalle = false;
    nuevaVenta: Venta = this.getVentaDefault();
    productoSeleccionado: string = '';
    cantidadSeleccionada: number = 1;
    ventaDetalleSeleccionada: Venta | null = null;
    detalleError: string = '';

    constructor(private spinner: SpinnerService) { }

    abrirDialogoNuevaVenta() {
        this.nuevaVenta = this.getVentaDefault();
        this.productoSeleccionado = '';
        this.cantidadSeleccionada = 1;
        this.detalleError = '';
        this.showDialogVenta = true;
        setTimeout(() => {
            const input = document.querySelector('input[name=cod_cliente]') as HTMLInputElement;
            if (input) input.focus();
        }, 100);
    }

    ngOnInit() {
        this.spinner.show();
        setTimeout(() => {
            // Simulación de carga de ventas
            this.ventas = [
                {
                    cod_venta: 'V00001',
                    fecha: new Date(),
                    total: 1200,
                    cod_cliente: 'C0001',
                    cod_usuario: 'U0001',
                    cod_caja: 'CAJA1',
                    metodo_pago: 'efectivo',
                    detalles: [
                        { cod_producto: 'Laptop', cantidad: 1, precio_unitario: 1200, subtotal: 1200 }
                    ]
                },
                {
                    cod_venta: 'V00002',
                    fecha: new Date(),
                    total: 250,
                    cod_cliente: 'C0002',
                    cod_usuario: 'U0002',
                    cod_caja: 'CAJA2',
                    metodo_pago: 'tarjeta',
                    detalles: [
                        { cod_producto: 'Mouse', cantidad: 10, precio_unitario: 25, subtotal: 250 }
                    ]
                }
            ];
            this.spinner.hide();
        }, 500);
    }

    agregarDetalle() {
        this.detalleError = '';
        if (!this.productoSeleccionado) {
            this.detalleError = 'Seleccione un producto.';
            return;
        }
        if (this.cantidadSeleccionada < 1) {
            this.detalleError = 'La cantidad debe ser mayor a 0.';
            return;
        }
        if (this.detalleYaAgregado()) {
            this.detalleError = 'Este producto ya está en el detalle.';
            return;
        }
        const prod = this.productosOptions.find(p => p.nombre === this.productoSeleccionado);
        if (!prod) {
            this.detalleError = 'Producto inválido.';
            return;
        }
        const detalle: DetalleVenta = {
            cod_producto: prod.nombre,
            cantidad: this.cantidadSeleccionada,
            precio_unitario: prod.precio,
            subtotal: prod.precio * this.cantidadSeleccionada
        };
        this.nuevaVenta.detalles.push(detalle);
        this.productoSeleccionado = '';
        this.cantidadSeleccionada = 1;
    }

    detalleYaAgregado(): boolean {
        return this.nuevaVenta.detalles.some(d => d.cod_producto === this.productoSeleccionado);
    }

    eliminarDetalle(index: number) {
        this.nuevaVenta.detalles.splice(index, 1);
        this.detalleError = '';
    }

    calcularTotalVenta() {
        return this.nuevaVenta.detalles.reduce((acc, d) => acc + d.subtotal, 0);
    }

    guardarVenta() {
        this.spinner.show();
        setTimeout(() => {
            this.nuevaVenta.total = this.calcularTotalVenta();
            this.nuevaVenta.cod_venta = 'V' + (this.ventas.length + 1).toString().padStart(5, '0');
            this.nuevaVenta.fecha = new Date();
            this.ventas.push({ ...this.nuevaVenta });
            // Integración automática con caja
            const cajaService = CajaService.getInstance();
            cajaService.agregarIngreso('Venta ' + this.nuevaVenta.cod_venta, this.nuevaVenta.total, this.nuevaVenta.cod_usuario);
            this.showDialogVenta = false;
            this.resetFormVenta();
            this.spinner.hide();
        }, 500);
    }

    resetFormVenta() {
        this.nuevaVenta = this.getVentaDefault();
        this.productoSeleccionado = '';
        this.cantidadSeleccionada = 1;
        this.detalleError = '';
    }

    verDetalleVenta(venta: Venta) {
        this.ventaDetalleSeleccionada = venta;
        this.showDialogDetalle = true;
    }

    imprimirTicket(venta: Venta) {
        // Aquí puedes integrar una librería como jsPDF para generar el PDF del ticket
        alert('Funcionalidad de impresión de ticket pendiente de implementar.');
    }

    getVentaDefault(): Venta {
        return {
            cod_venta: '',
            fecha: new Date(),
            total: 0,
            cod_cliente: '',
            cod_usuario: '',
            cod_caja: '',
            metodo_pago: 'efectivo',
            detalles: []
        };
    }

    get ventasFiltradas() {
        return this.ventas.filter(v =>
            (!this.filtroCliente || (v.cod_cliente && v.cod_cliente.toLowerCase().includes(this.filtroCliente.toLowerCase()))) &&
            (!this.filtroFecha || (v.fecha && v.fecha.toDateString() === this.filtroFecha.toDateString()))
        );
    }
}
