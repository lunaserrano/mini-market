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

interface DetalleCompra {
    cod_producto: string;
    cantidad: number;
    precio_unitario: number;
    subtotal: number;
}

interface Compra {
    cod_compra: string;
    fecha: Date;
    proveedor: string;
    usuario: string;
    total: number;
    detalles: DetalleCompra[];
}

@Component({
    selector: 'app-main-compras',
    standalone: true,
    imports: [CommonModule, FormsModule, TableModule, Dialog, InputText, Button, CardModule, DropdownModule, CalendarModule],
    template: `
    <div class="p-4">
      <h2 class="text-xl font-bold mb-4 text-primary">Compras</h2>
      <div class="flex justify-between items-center mb-2">
        <span class="font-semibold">Registro de compras</span>
        <p-button type="button" label="Nueva compra" icon="pi pi-plus" class="p-button-primary" (click)="abrirDialogoNuevaCompra()"></p-button>
      </div>
      <p-table [value]="compras" class="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th>Código</th>
            <th>Fecha</th>
            <th>Proveedor</th>
            <th>Usuario</th>
            <th>Total</th>
            <th>Acciones</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-compra>
          <tr>
            <td>{{ compra.cod_compra }}</td>
            <td>{{ compra.fecha | date:'short' }}</td>
            <td>{{ compra.proveedor }}</td>
            <td>{{ compra.usuario }}</td>
            <td>{{ compra.total | currency:'USD' }}</td>
            <td>
              <p-button icon="pi pi-eye" class="p-button-sm p-button-text" (click)="verDetalleCompra(compra)"></p-button>
            </td>
          </tr>
        </ng-template>
      </p-table>

      <p-dialog header="Registrar compra" [(visible)]="showDialog" [modal]="true" [style]="{width: '40rem'}" [closable]="true" (onHide)="resetForm()">
        <form (ngSubmit)="guardarCompra()" #form="ngForm" class="p-fluid">
          <p-card class="mb-4">
            <div class="mb-3 w-full">
              <label class="block mb-1 font-medium">Proveedor</label>
              <input pInputText [(ngModel)]="nuevaCompra.proveedor" name="proveedor" required class="w-full" />
            </div>
            <div class="mb-3 w-full">
              <label class="block mb-1 font-medium">Usuario</label>
              <input pInputText [(ngModel)]="nuevaCompra.usuario" name="usuario" required class="w-full" />
            </div>
            <div class="mb-3 w-full">
              <label class="block mb-1 font-medium">Fecha</label>
              <p-calendar [(ngModel)]="nuevaCompra.fecha" name="fecha" inputId="fecha" [showTime]="true" class="w-full"></p-calendar>
            </div>
            <div class="mb-3 w-full">
              <label class="block mb-1 font-medium">Detalle de productos</label>
              <div *ngFor="let det of nuevaCompra.detalles; let i = index" class="flex gap-2 mb-2 items-center">
                <input pInputText [(ngModel)]="det.cod_producto" name="cod_producto{{i}}" placeholder="Producto" class="w-1/3" />
                <input pInputText type="number" [(ngModel)]="det.cantidad" name="cantidad{{i}}" min="1" class="w-1/6" (ngModelChange)="actualizarSubtotal(det)" />
                <input pInputText type="number" [(ngModel)]="det.precio_unitario" name="precio_unitario{{i}}" min="0.01" step="0.01" class="w-1/6" (ngModelChange)="actualizarSubtotal(det)" />
                <span class="w-1/6">Subtotal: {{ det.subtotal | currency:'USD' }}</span>
                <p-button icon="pi pi-trash" class="p-button-danger p-button-sm" (click)="eliminarDetalle(i)" type="button"></p-button>
              </div>
              <p-button icon="pi pi-plus" class="p-button-success p-button-sm mt-2" (click)="agregarDetalle()" type="button" label="Agregar producto"></p-button>
            </div>
            <div class="mb-3 w-full text-right font-bold">
              Total: {{ calcularTotal() | currency:'USD' }}
            </div>
          </p-card>
          <div class="flex justify-end gap-2 mt-2">
            <p-button type="button" label="Cancelar" class="p-button-secondary" (onClick)="showDialog = false"></p-button>
            <p-button type="submit" label="Registrar compra" class="p-button-primary" [disabled]="form.invalid || !nuevaCompra.detalles.length"></p-button>
          </div>
        </form>
      </p-dialog>

      <p-dialog header="Detalle de compra" [(visible)]="showDialogDetalle" [modal]="true" [style]="{width: '40rem'}">
        <p-table [value]="compraDetalleSeleccionada?.detalles || []" class="p-datatable-sm">
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
        <div class="text-right font-bold mt-2">Total: {{ compraDetalleSeleccionada?.total | currency:'USD' }}</div>
      </p-dialog>
    </div>
  `
})
export class MainComprasComponent {
    compras: Compra[] = [];
    showDialog = false;
    showDialogDetalle = false;
    nuevaCompra: Compra = this.getCompraDefault();
    compraDetalleSeleccionada: Compra | null = null;

    constructor(private spinner: SpinnerService) { }

    ngOnInit() {
        this.spinner.show();
        setTimeout(() => {
            // ...carga de compras...
            this.spinner.hide();
        }, 500);
    }

    abrirDialogoNuevaCompra() {
        this.nuevaCompra = this.getCompraDefault();
        this.showDialog = true;
    }

    agregarDetalle() {
        this.nuevaCompra.detalles.push({ cod_producto: '', cantidad: 1, precio_unitario: 0, subtotal: 0 });
    }

    eliminarDetalle(idx: number) {
        this.nuevaCompra.detalles.splice(idx, 1);
    }

    actualizarSubtotal(det: DetalleCompra) {
        det.subtotal = (det.cantidad || 0) * (det.precio_unitario || 0);
    }

    calcularTotal() {
        return this.nuevaCompra.detalles.reduce((acc, d) => acc + (d.subtotal || 0), 0);
    }

    guardarCompra() {
        this.spinner.show();
        setTimeout(() => {
            this.nuevaCompra.total = this.calcularTotal();
            this.nuevaCompra.cod_compra = 'C' + (this.compras.length + 1).toString().padStart(5, '0');
            this.compras.push({ ...this.nuevaCompra, detalles: [...this.nuevaCompra.detalles] });
            // Integración automática con caja
            const cajaService = CajaService.getInstance();
            cajaService.agregarEgreso('Compra ' + this.nuevaCompra.cod_compra, this.nuevaCompra.total, this.nuevaCompra.usuario);
            this.showDialog = false;
            this.resetForm();
            this.spinner.hide();
        }, 500);
    }

    resetForm() {
        this.nuevaCompra = this.getCompraDefault();
    }

    verDetalleCompra(compra: Compra) {
        this.compraDetalleSeleccionada = compra;
        this.showDialogDetalle = true;
    }

    getCompraDefault(): Compra {
        return {
            cod_compra: '',
            fecha: new Date(),
            proveedor: '',
            usuario: '',
            total: 0,
            detalles: []
        };
    }
}
