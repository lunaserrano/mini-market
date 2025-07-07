import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { Dialog } from 'primeng/dialog';
import { InputText } from 'primeng/inputtext';
import { Button } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DropdownModule } from 'primeng/dropdown';
import { CalendarModule } from 'primeng/calendar';
import { SpinnerService } from '../../shared/spinner.service';

interface MovimientoCaja {
    cod_movimiento: string;
    cod_caja: string;
    tipo: 'ingreso' | 'egreso';
    descripcion: string;
    monto: number;
    fecha: Date;
    usuario: string;
}

interface Caja {
    cod_caja: string;
    usuario_apertura: string;
    fecha_apertura: Date;
    monto_inicial: number;
    fecha_cierre?: Date;
    usuario_cierre?: string;
    monto_final?: number;
    movimientos: MovimientoCaja[];
}

// Servicio singleton para caja actual
export class CajaService {
    static instancia: CajaService;
    cajaActual: Caja | null = null;
    cajas: Caja[] = [];
    constructor() { CajaService.instancia = this; }
    static getInstance(): CajaService {
        return CajaService.instancia || new CajaService();
    }
    agregarIngreso(descripcion: string, monto: number, usuario: string) {
        if (!this.cajaActual || this.cajaActual.fecha_cierre) return;
        const cod_movimiento = 'M' + (this.cajaActual.movimientos.length + 1).toString().padStart(5, '0');
        this.cajaActual.movimientos.push({
            cod_movimiento,
            cod_caja: this.cajaActual.cod_caja,
            tipo: 'ingreso',
            descripcion,
            monto,
            fecha: new Date(),
            usuario
        });
    }
    agregarEgreso(descripcion: string, monto: number, usuario: string) {
        if (!this.cajaActual || this.cajaActual.fecha_cierre) return;
        const cod_movimiento = 'M' + (this.cajaActual.movimientos.length + 1).toString().padStart(5, '0');
        this.cajaActual.movimientos.push({
            cod_movimiento,
            cod_caja: this.cajaActual.cod_caja,
            tipo: 'egreso',
            descripcion,
            monto,
            fecha: new Date(),
            usuario
        });
    }
}

@Component({
    selector: 'app-main-cajas',
    standalone: true,
    imports: [CommonModule, FormsModule, TableModule, Dialog, InputText, Button, CardModule, DropdownModule, CalendarModule],
    template: `
    <div class="p-4">
      <h2 class="text-xl font-bold mb-4 text-primary">Cajas</h2>
      <div class="flex justify-between items-center mb-2">
        <span class="font-semibold">Gestión de cajas</span>
        <p-button type="button" label="Abrir caja" icon="pi pi-plus" class="p-button-primary" (click)="abrirDialogoApertura()" [disabled]="cajaAbierta"></p-button>
      </div>
      <div *ngIf="cajaAbierta" class="mb-4 p-3 border-l-4 border-green-500 bg-green-50">
        <b>Caja abierta:</b> {{ cajaActual?.cod_caja }} | Usuario: {{ cajaActual?.usuario_apertura }} | Fecha: {{ cajaActual?.fecha_apertura | date:'short' }} | Monto inicial: {{ cajaActual?.monto_inicial | currency:'USD' }}
        <p-button type="button" label="Cerrar caja" icon="pi pi-lock" class="p-button-danger ml-4" (click)="abrirDialogoCierre()"></p-button>
      </div>
      <p-table [value]="cajas" class="p-datatable-sm mb-4">
        <ng-template pTemplate="header">
          <tr>
            <th>Código</th>
            <th>Usuario apertura</th>
            <th>Fecha apertura</th>
            <th>Monto inicial</th>
            <th>Usuario cierre</th>
            <th>Fecha cierre</th>
            <th>Monto final</th>
            <th>Movimientos</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-caja>
          <tr>
            <td>{{ caja.cod_caja }}</td>
            <td>{{ caja.usuario_apertura }}</td>
            <td>{{ caja.fecha_apertura | date:'short' }}</td>
            <td>{{ caja.monto_inicial | currency:'USD' }}</td>
            <td>{{ caja.usuario_cierre || '-' }}</td>
            <td>{{ caja.fecha_cierre ? (caja.fecha_cierre | date:'short') : '-' }}</td>
            <td>{{ caja.monto_final !== undefined ? (caja.monto_final | currency:'USD') : '-' }}</td>
            <td>
              <p-button icon="pi pi-eye" class="p-button-sm p-button-text" (click)="verMovimientos(caja)"></p-button>
            </td>
          </tr>
        </ng-template>
      </p-table>

      <p-dialog header="Apertura de caja" [(visible)]="showDialogApertura" [modal]="true" [style]="{width: '30rem'}" [closable]="true" (onHide)="resetFormApertura()">
        <form (ngSubmit)="guardarApertura()" #form="ngForm" class="p-fluid">
          <p-card class="mb-4">
            <div class="mb-3 w-full">
              <label class="block mb-1 font-medium">Usuario apertura</label>
              <input pInputText [(ngModel)]="apertura.usuario_apertura" name="usuario_apertura" required class="w-full" />
            </div>
            <div class="mb-3 w-full">
              <label class="block mb-1 font-medium">Monto inicial</label>
              <input pInputText type="number" [(ngModel)]="apertura.monto_inicial" name="monto_inicial" required min="0" class="w-full" />
            </div>
          </p-card>
          <div class="flex justify-end gap-2 mt-2">
            <p-button type="button" label="Cancelar" class="p-button-secondary" (onClick)="showDialogApertura = false"></p-button>
            <p-button type="submit" label="Abrir caja" class="p-button-primary" [disabled]="form.invalid"></p-button>
          </div>
        </form>
      </p-dialog>

      <p-dialog header="Registrar movimiento" [(visible)]="showDialogMovimiento" [modal]="true" [style]="{width: '30rem'}" [closable]="true" (onHide)="resetFormMovimiento()">
        <form (ngSubmit)="guardarMovimiento()" #form="ngForm" class="p-fluid">
          <p-card class="mb-4">
            <div class="mb-3 w-full">
              <label class="block mb-1 font-medium">Tipo</label>
              <p-dropdown [options]="tiposMovimiento" [(ngModel)]="movimiento.tipo" name="tipo" optionLabel="label" optionValue="value" placeholder="Seleccione tipo" class="w-full"></p-dropdown>
            </div>
            <div class="mb-3 w-full">
              <label class="block mb-1 font-medium">Descripción</label>
              <input pInputText [(ngModel)]="movimiento.descripcion" name="descripcion" required class="w-full" />
            </div>
            <div class="mb-3 w-full">
              <label class="block mb-1 font-medium">Monto</label>
              <input pInputText type="number" [(ngModel)]="movimiento.monto" name="monto" required min="0.01" step="0.01" class="w-full" />
            </div>
            <div class="mb-3 w-full">
              <label class="block mb-1 font-medium">Usuario</label>
              <input pInputText [(ngModel)]="movimiento.usuario" name="usuario" required class="w-full" />
            </div>
          </p-card>
          <div class="flex justify-end gap-2 mt-2">
            <p-button type="button" label="Cancelar" class="p-button-secondary" (onClick)="showDialogMovimiento = false"></p-button>
            <p-button type="submit" label="Registrar" class="p-button-primary" [disabled]="form.invalid"></p-button>
          </div>
        </form>
      </p-dialog>

      <p-dialog header="Cierre de caja" [(visible)]="showDialogCierre" [modal]="true" [style]="{width: '30rem'}" [closable]="true" (onHide)="resetFormCierre()">
        <form (ngSubmit)="guardarCierre()" #form="ngForm" class="p-fluid">
          <p-card class="mb-4">
            <div class="mb-3 w-full">
              <label class="block mb-1 font-medium">Usuario cierre</label>
              <input pInputText [(ngModel)]="cierre.usuario_cierre" name="usuario_cierre" required class="w-full" />
            </div>
          </p-card>
          <div class="flex justify-end gap-2 mt-2">
            <p-button type="button" label="Cancelar" class="p-button-secondary" (onClick)="showDialogCierre = false"></p-button>
            <p-button type="submit" label="Cerrar caja" class="p-button-danger" [disabled]="form.invalid"></p-button>
          </div>
        </form>
      </p-dialog>

      <p-dialog header="Movimientos de caja" [(visible)]="showDialogMovimientos" [modal]="true" [style]="{width: '40rem'}">
        <p-table [value]="cajaMovimientosSeleccionada?.movimientos || []" class="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th>Código</th>
              <th>Tipo</th>
              <th>Descripción</th>
              <th>Monto</th>
              <th>Fecha</th>
              <th>Usuario</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-mov>
            <tr>
              <td>{{ mov.cod_movimiento }}</td>
              <td>{{ mov.tipo | titlecase }}</td>
              <td>{{ mov.descripcion }}</td>
              <td>{{ mov.monto | currency:'USD' }}</td>
              <td>{{ mov.fecha | date:'short' }}</td>
              <td>{{ mov.usuario }}</td>
            </tr>
          </ng-template>
        </p-table>
        <div class="text-right font-bold mt-2">
          <span>Ingresos: {{ calcularIngresos(cajaMovimientosSeleccionada) | currency:'USD' }} | </span>
          <span>Egresos: {{ calcularEgresos(cajaMovimientosSeleccionada) | currency:'USD' }} | </span>
          <span>Balance: {{ calcularBalance(cajaMovimientosSeleccionada) | currency:'USD' }}</span>
        </div>
      </p-dialog>

      <div *ngIf="cajaAbierta" class="mt-4">
        <p-button type="button" label="Registrar movimiento" icon="pi pi-plus" class="p-button-success" (click)="abrirDialogoMovimiento()"></p-button>
      </div>
    </div>
  `
})
export class MainCajasComponent implements OnInit {
    cajas: Caja[] = [];
    cajaActual: Caja | null = null;
    showDialogApertura = false;
    showDialogMovimiento = false;
    showDialogCierre = false;
    showDialogMovimientos = false;
    cajaMovimientosSeleccionada: Caja | null = null;

    apertura = { usuario_apertura: '', monto_inicial: 0 };
    movimiento = { tipo: '', descripcion: '', monto: 0, usuario: '' } as any;
    cierre = { usuario_cierre: '' };

    tiposMovimiento = [
        { label: 'Ingreso', value: 'ingreso' },
        { label: 'Egreso', value: 'egreso' }
    ];

    get cajaAbierta() {
        return !!this.cajaActual && !this.cajaActual.fecha_cierre;
    }

    abrirDialogoApertura() {
        this.apertura = { usuario_apertura: '', monto_inicial: 0 };
        this.showDialogApertura = true;
    }

    guardarApertura() {
        this.spinner.show();
        setTimeout(() => {
            const cod_caja = 'C' + (this.cajas.length + 1).toString().padStart(4, '0');
            const nuevaCaja: Caja = {
                cod_caja,
                usuario_apertura: this.apertura.usuario_apertura,
                fecha_apertura: new Date(),
                monto_inicial: this.apertura.monto_inicial,
                movimientos: []
            };
            this.cajas.push(nuevaCaja);
            this.cajaActual = nuevaCaja;
            this.cajaService.cajaActual = nuevaCaja;
            this.cajaService.cajas = this.cajas;
            this.showDialogApertura = false;
            this.apertura = { usuario_apertura: '', monto_inicial: 0 };
            this.spinner.hide();
        }, 500);
    }

    abrirDialogoMovimiento() {
        this.movimiento = { tipo: '', descripcion: '', monto: 0, usuario: '' };
        this.showDialogMovimiento = true;
    }

    guardarMovimiento() {
        if (!this.cajaActual) return;
        const cod_movimiento = 'M' + (this.cajaActual.movimientos.length + 1).toString().padStart(5, '0');
        const nuevoMov: MovimientoCaja = {
            cod_movimiento,
            cod_caja: this.cajaActual.cod_caja,
            tipo: this.movimiento.tipo,
            descripcion: this.movimiento.descripcion,
            monto: this.movimiento.monto,
            fecha: new Date(),
            usuario: this.movimiento.usuario
        };
        this.cajaActual.movimientos.push(nuevoMov);
        this.showDialogMovimiento = false;
        this.movimiento = { tipo: '', descripcion: '', monto: 0, usuario: '' };
    }

    abrirDialogoCierre() {
        this.cierre = { usuario_cierre: '' };
        this.showDialogCierre = true;
    }

    guardarCierre() {
        if (!this.cajaActual) return;
        this.cajaActual.fecha_cierre = new Date();
        this.cajaActual.usuario_cierre = this.cierre.usuario_cierre;
        this.cajaActual.monto_final = this.cajaActual.monto_inicial + this.calcularIngresos(this.cajaActual) - this.calcularEgresos(this.cajaActual);
        this.cajaActual = null;
        this.showDialogCierre = false;
        this.cierre = { usuario_cierre: '' };
    }

    verMovimientos(caja: Caja) {
        this.cajaMovimientosSeleccionada = caja;
        this.showDialogMovimientos = true;
    }

    calcularIngresos(caja: Caja | null) {
        if (!caja) return 0;
        return caja.movimientos.filter(m => m.tipo === 'ingreso').reduce((acc, m) => acc + m.monto, 0);
    }

    calcularEgresos(caja: Caja | null) {
        if (!caja) return 0;
        return caja.movimientos.filter(m => m.tipo === 'egreso').reduce((acc, m) => acc + m.monto, 0);
    }

    calcularBalance(caja: Caja | null) {
        if (!caja) return 0;
        return this.calcularIngresos(caja) - this.calcularEgresos(caja);
    }

    resetFormApertura() {
        this.apertura = { usuario_apertura: '', monto_inicial: 0 };
    }
    resetFormMovimiento() {
        this.movimiento = { tipo: '', descripcion: '', monto: 0, usuario: '' };
    }
    resetFormCierre() {
        this.cierre = { usuario_cierre: '' };
    }

    constructor(private spinner: SpinnerService) {
        CajaService.instancia = this.cajaService = CajaService.getInstance();
    }
    cajaService: CajaService;

    ngOnInit() {
        this.spinner.show();
        setTimeout(() => {
            this.spinner.hide();
        }, 500);
    }
}
