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
import { HistorialInventarioComponent } from './historial-inventario';
import { SpinnerService } from '../../shared/spinner.service';

interface MovimientoInventario {
    tipo: 'entrada' | 'salida' | 'ajuste';
    cod_producto: string;
    cantidad: number;
    motivo: string;
    fecha: Date;
    usuario: string;
}

@Component({
    selector: 'app-main-inventario',
    standalone: true,
    imports: [CommonModule, FormsModule, TableModule, Dialog, InputText, Button, CardModule, DropdownModule, CalendarModule, HistorialInventarioComponent],
    template: `
    <div class="p-4">
      <h2 class="text-xl font-bold mb-4 text-primary">Inventario</h2>
      <div class="flex justify-between items-center mb-2">
        <span class="font-semibold">Movimientos de inventario</span>
        <p-button type="button" label="Registrar movimiento" icon="pi pi-plus" class="p-button-primary" (click)="abrirDialogoNuevo()"></p-button>
      </div>
      <div class="mb-4">
        <label for="productoHistorial" class="block mb-1 font-medium">Ver historial por producto</label>
        <p-dropdown [options]="productosOptions" [(ngModel)]="productoSeleccionado" optionLabel="nombre" optionValue="nombre" placeholder="Seleccione producto" class="w-full"></p-dropdown>
      </div>
      <div class="flex gap-2 mb-2">
        <div class="w-1/2">
          <label class="block mb-1 font-medium">Desde</label>
          <p-calendar [(ngModel)]="fechaFiltroInicio" dateFormat="yy-mm-dd" class="w-full" [styleClass]="'w-full'"></p-calendar>
        </div>
        <div class="w-1/2">
          <label class="block mb-1 font-medium">Hasta</label>
          <p-calendar [(ngModel)]="fechaFiltroFin" dateFormat="yy-mm-dd" class="w-full" [styleClass]="'w-full'"></p-calendar>
        </div>
        <div class="flex items-end">
          <p-button type="button" label="Exportar historial" icon="pi pi-download" class="p-button-success" (onClick)="exportarHistorial()"></p-button>
          <p-button type="button" label="Excel" icon="pi pi-file-excel" class="p-button-success ml-2" (onClick)="exportarHistorialExcel()"></p-button>
        </div>
      </div>
      <app-historial-inventario *ngIf="productoSeleccionado" [producto]="productoSeleccionado" [movimientos]="movimientosFiltradosPorProducto"></app-historial-inventario>
      <p-table [value]="movimientos" class="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th>Tipo</th>
            <th>Producto</th>
            <th>Cantidad</th>
            <th>Motivo</th>
            <th>Fecha</th>
            <th>Usuario</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-mov>
          <tr>
            <td>{{ mov.tipo | titlecase }}</td>
            <td>{{ obtenerNombreProducto(mov.cod_producto) }}</td>
            <td>{{ mov.cantidad }}</td>
            <td>{{ mov.motivo }}</td>
            <td>{{ mov.fecha | date:'short' }}</td>
            <td>{{ mov.usuario }}</td>
          </tr>
        </ng-template>
      </p-table>

      <p-dialog header="Registrar movimiento" [(visible)]="showDialog" [modal]="true" [style]="{width: '30rem'}" [closable]="true" (onHide)="resetForm()">
        <form (ngSubmit)="guardarMovimiento()" #form="ngForm" class="p-fluid">
          <p-card class="mb-4">
            <div class="mb-3 w-full">
              <label for="tipo" class="block mb-1 font-medium">Tipo</label>
              <p-dropdown [options]="tipos" [(ngModel)]="nuevoMovimiento.tipo" name="tipo" optionLabel="label" optionValue="value" placeholder="Seleccione tipo" class="w-full"></p-dropdown>
            </div>
            <div class="mb-3 w-full">
              <label for="cod_producto" class="block mb-1 font-medium">Producto</label>
              <p-dropdown [options]="productosOptions" [(ngModel)]="nuevoMovimiento.cod_producto" name="cod_producto" optionLabel="nombre" optionValue="nombre" placeholder="Seleccione producto" class="w-full"></p-dropdown>
            </div>
            <div class="mb-3 w-full">
              <label for="cantidad" class="block mb-1 font-medium">Cantidad</label>
              <input pInputText id="cantidad" name="cantidad" type="number" [(ngModel)]="nuevoMovimiento.cantidad" required class="w-full" min="1" />
            </div>
            <div class="mb-3 w-full">
              <label for="motivo" class="block mb-1 font-medium">Motivo</label>
              <input pInputText id="motivo" name="motivo" [(ngModel)]="nuevoMovimiento.motivo" required class="w-full" />
            </div>
            <div class="mb-3 w-full">
              <label for="fecha" class="block mb-1 font-medium">Fecha</label>
              <p-calendar [(ngModel)]="nuevoMovimiento.fecha" name="fecha" inputId="fecha" [showTime]="true" class="w-full"></p-calendar>
            </div>
            <div class="mb-3 w-full">
              <label for="usuario" class="block mb-1 font-medium">Usuario</label>
              <input pInputText id="usuario" name="usuario" [(ngModel)]="nuevoMovimiento.usuario" required class="w-full" />
            </div>
          </p-card>
          <div class="flex justify-end gap-2 mt-2">
            <p-button type="button" label="Cancelar" class="p-button-secondary" (onClick)="showDialog = false"></p-button>
            <p-button type="submit" label="Registrar" class="p-button-primary" [disabled]="form.invalid"></p-button>
          </div>
        </form>
      </p-dialog>

      <div class="flex items-end mt-2">
        <p-button type="button" label="Exportar todos (CSV)" icon="pi pi-download" class="p-button-secondary" (onClick)="exportarTodosMovimientosCSV()"></p-button>
        <p-button type="button" label="Todos Excel" icon="pi pi-file-excel" class="p-button-secondary ml-2" (onClick)="exportarTodosMovimientosExcel()"></p-button>
      </div>
    </div>
  `
})
export class MainInventarioComponent {
    movimientos: MovimientoInventario[] = [];

    tipos = [
        { label: 'Entrada', value: 'entrada' },
        { label: 'Salida', value: 'salida' },
        { label: 'Ajuste', value: 'ajuste' }
    ];

    productosOptions = [
        { nombre: 'Laptop' },
        { nombre: 'Mouse' }
    ];

    showDialog = false;
    nuevoMovimiento: MovimientoInventario = this.getMovimientoDefault();
    productoSeleccionado: string = '';
    fechaFiltroInicio: Date | null = null;
    fechaFiltroFin: Date | null = null;

    constructor(private spinner: SpinnerService) { }

    ngOnInit() {
        this.spinner.show();
        setTimeout(() => {
            this.spinner.hide();
        }, 500);
    }

    abrirDialogoNuevo() {
        this.nuevoMovimiento = this.getMovimientoDefault();
        this.showDialog = true;
    }

    guardarMovimiento() {
        this.spinner.show();
        setTimeout(() => {
            this.movimientos.push({ ...this.nuevoMovimiento });
            this.showDialog = false;
            this.resetForm();
            this.spinner.hide();
        }, 500);
    }

    resetForm() {
        this.nuevoMovimiento = this.getMovimientoDefault();
    }

    obtenerNombreProducto(nombre: string) {
        const prod = this.productosOptions.find(p => p.nombre === nombre);
        return prod ? prod.nombre : nombre;
    }

    getMovimientoDefault(): MovimientoInventario {
        return {
            tipo: 'entrada',
            cod_producto: '',
            cantidad: 1,
            motivo: '',
            fecha: new Date(),
            usuario: ''
        };
    }

    // Agrega un getter para filtrar movimientos por producto
    get movimientosFiltradosPorProducto() {
        let filtrados = this.movimientos;
        if (this.productoSeleccionado) {
            filtrados = filtrados.filter(m => m.cod_producto === this.productoSeleccionado);
        }
        if (this.fechaFiltroInicio) {
            filtrados = filtrados.filter(m => m.fecha >= this.fechaFiltroInicio!);
        }
        if (this.fechaFiltroFin) {
            filtrados = filtrados.filter(m => m.fecha <= this.fechaFiltroFin!);
        }
        return filtrados;
    }

    exportarHistorial() {
        const movimientos = this.movimientosFiltradosPorProducto;
        if (!movimientos.length) return;
        const encabezado = 'Tipo,Producto,Cantidad,Motivo,Fecha,Usuario';
        const filas = movimientos.map(m => [
            m.tipo,
            this.obtenerNombreProducto(m.cod_producto),
            m.cantidad,
            m.motivo,
            m.fecha instanceof Date ? m.fecha.toLocaleString() : m.fecha,
            m.usuario
        ].join(','));
        const csv = [encabezado, ...filas].join('\n');
        const blob = new Blob([csv], { type: 'text/csv' });
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `historial_${this.productoSeleccionado || 'todos'}.csv`;
        a.click();
        window.URL.revokeObjectURL(url);
    }

    exportarHistorialExcel() {
        const movimientos = this.movimientosFiltradosPorProducto;
        if (!movimientos.length) return;
        // Generar datos para Excel
        const encabezado = ['Tipo', 'Producto', 'Cantidad', 'Motivo', 'Fecha', 'Usuario'];
        const filas = movimientos.map(m => [
            m.tipo,
            this.obtenerNombreProducto(m.cod_producto),
            m.cantidad,
            m.motivo,
            m.fecha instanceof Date ? m.fecha.toLocaleString() : m.fecha,
            m.usuario
        ]);
        // Construir hoja Excel en formato XML Spreadsheet (compatible con Excel)
        let xml = '<?xml version="1.0"?><?mso-application progid="Excel.Sheet"?>';
        xml += '<Workbook xmlns="urn:schemas-microsoft-com:office:spreadsheet" xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:x="urn:schemas-microsoft-com:office:excel" xmlns:ss="urn:schemas-microsoft-com:office:spreadsheet">';
        xml += '<Worksheet ss:Name="Historial"><Table>';
        xml += '<Row>' + encabezado.map(h => `<Cell><Data ss:Type="String">${h}</Data></Cell>`).join('') + '</Row>';
        filas.forEach(fila => {
            xml += '<Row>' + fila.map(d => `<Cell><Data ss:Type="String">${d}</Data></Cell>`).join('') + '</Row>';
        });
        xml += '</Table></Worksheet></Workbook>';
        const blob = new Blob([xml], { type: 'application/vnd.ms-excel' });
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `historial_${this.productoSeleccionado || 'todos'}.xls`;
        a.click();
        window.URL.revokeObjectURL(url);
    }

    exportarTodosMovimientosCSV() {
        if (!this.movimientos.length) return;
        const encabezado = 'Tipo,Producto,Cantidad,Motivo,Fecha,Usuario';
        const filas = this.movimientos.map(m => [
            m.tipo,
            this.obtenerNombreProducto(m.cod_producto),
            m.cantidad,
            m.motivo,
            m.fecha instanceof Date ? m.fecha.toLocaleString() : m.fecha,
            m.usuario
        ].join(','));
        const csv = [encabezado, ...filas].join('\n');
        const blob = new Blob([csv], { type: 'text/csv' });
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'movimientos_todos.csv';
        a.click();
        window.URL.revokeObjectURL(url);
    }

    exportarTodosMovimientosExcel() {
        if (!this.movimientos.length) return;
        const encabezado = ['Tipo', 'Producto', 'Cantidad', 'Motivo', 'Fecha', 'Usuario'];
        const filas = this.movimientos.map(m => [
            m.tipo,
            this.obtenerNombreProducto(m.cod_producto),
            m.cantidad,
            m.motivo,
            m.fecha instanceof Date ? m.fecha.toLocaleString() : m.fecha,
            m.usuario
        ]);
        let xml = '<?xml version="1.0"?><?mso-application progid="Excel.Sheet"?>';
        xml += '<Workbook xmlns="urn:schemas-microsoft-com:office:spreadsheet" xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:x="urn:schemas-microsoft-com:office:excel" xmlns:ss="urn:schemas-microsoft-com:office:spreadsheet">';
        xml += '<Worksheet ss:Name="Movimientos"><Table>';
        xml += '<Row>' + encabezado.map(h => `<Cell><Data ss:Type="String">${h}</Data></Cell>`).join('') + '</Row>';
        filas.forEach(fila => {
            xml += '<Row>' + fila.map(d => `<Cell><Data ss:Type="String">${d}</Data></Cell>`).join('') + '</Row>';
        });
        xml += '</Table></Worksheet></Workbook>';
        const blob = new Blob([xml], { type: 'application/vnd.ms-excel' });
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'movimientos_todos.xls';
        a.click();
        window.URL.revokeObjectURL(url);
    }
}
