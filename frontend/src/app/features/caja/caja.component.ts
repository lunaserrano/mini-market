import { DatePipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { MessageService } from 'primeng/api';
import { CajaService } from '../../core/services/caja.service';
import { Caja, MovimientoCaja } from '../../core/models/caja.models';
import { MonedaPipe } from '../../core/pipes/moneda.pipe';

@Component({
  selector: 'app-caja',
  standalone: true,
  imports: [DatePipe, MonedaPipe, FormsModule, ButtonModule, DialogModule, InputTextModule, SelectModule, TableModule],
  templateUrl: './caja.component.html'
})
export class CajaComponent implements OnInit {
  readonly caja = signal<Caja | null>(null);
  readonly movimientos = signal<MovimientoCaja[]>([]);
  readonly cargando = signal(false);

  readonly mostrarApertura = signal(false);
  montoInicial = 0;

  readonly mostrarCierre = signal(false);
  montoFinalDeclarado = 0;

  readonly mostrarMovimiento = signal(false);
  nuevoMovimiento = { tipo: 'INGRESO' as 'INGRESO' | 'EGRESO', concepto: '', monto: 0 };
  readonly tiposMovimiento = [
    { label: 'Ingreso', value: 'INGRESO' },
    { label: 'Egreso', value: 'EGRESO' }
  ];

  constructor(
    private readonly cajaService: CajaService,
    private readonly messageService: MessageService
  ) {}

  ngOnInit(): void {
    this.cargar();
  }

  private cargar(): void {
    this.cargando.set(true);
    this.cajaService.obtenerActual().subscribe({
      next: (caja) => {
        this.caja.set(caja);
        this.cargando.set(false);
        if (caja) this.cargarMovimientos(caja.id);
      },
      error: () => {
        this.caja.set(null);
        this.cargando.set(false);
      }
    });
  }

  private cargarMovimientos(cajaId: number): void {
    this.cajaService.listarMovimientos(cajaId).subscribe((movimientos) => this.movimientos.set(movimientos));
  }

  abrirDialogoApertura(): void {
    this.montoInicial = 0;
    this.mostrarApertura.set(true);
  }

  confirmarApertura(): void {
    this.cajaService.abrir(this.montoInicial).subscribe((caja) => {
      this.caja.set(caja);
      this.movimientos.set([]);
      this.mostrarApertura.set(false);
      this.messageService.add({ severity: 'success', summary: 'Caja abierta', detail: `Monto inicial: Q${this.montoInicial.toFixed(2)}` });
    });
  }

  abrirDialogoCierre(): void {
    this.montoFinalDeclarado = 0;
    this.mostrarCierre.set(true);
  }

  confirmarCierre(): void {
    const cajaActual = this.caja();
    if (!cajaActual) return;

    this.cajaService.cerrar(cajaActual.id, this.montoFinalDeclarado).subscribe((caja) => {
      this.caja.set(caja);
      this.mostrarCierre.set(false);
      this.messageService.add({
        severity: caja.diferencia === 0 ? 'success' : 'warn',
        summary: 'Caja cerrada',
        detail: `Diferencia: Q${caja.diferencia?.toFixed(2)}`,
        life: 6000
      });
    });
  }

  abrirDialogoMovimiento(): void {
    this.nuevoMovimiento = { tipo: 'INGRESO', concepto: '', monto: 0 };
    this.mostrarMovimiento.set(true);
  }

  confirmarMovimiento(): void {
    const cajaActual = this.caja();
    if (!cajaActual || !this.nuevoMovimiento.concepto || this.nuevoMovimiento.monto <= 0) return;

    this.cajaService.registrarMovimiento(cajaActual.id, this.nuevoMovimiento).subscribe(() => {
      this.mostrarMovimiento.set(false);
      this.cargarMovimientos(cajaActual.id);
    });
  }
}
