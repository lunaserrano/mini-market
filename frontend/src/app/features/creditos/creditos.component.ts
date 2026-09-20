import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { ProgressBarModule } from 'primeng/progressbar';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { MessageService } from 'primeng/api';
import { HasPermissionDirective } from '../../core/directives/has-permission.directive';
import { AbonoCreate, Credito, CreditoResumen, EstadoCredito } from '../../core/models/credito.models';
import { MonedaPipe } from '../../core/pipes/moneda.pipe';
import { PERMISOS } from '../../core/security/permisos';
import { ConfigService } from '../../core/services/config.service';
import { CreditoService } from '../../core/services/credito.service';

type FiltroEstado = EstadoCredito | 'TODOS';

const FILTROS_ESTADO: { label: string; value: FiltroEstado }[] = [
  { label: 'Pendientes', value: 'PENDIENTE' },
  { label: 'Pagados', value: 'PAGADO' },
  { label: 'Anulados', value: 'ANULADO' },
  { label: 'Todos', value: 'TODOS' }
];

const METODOS_PAGO = [
  { label: 'Efectivo', value: 'EFECTIVO' },
  { label: 'Tarjeta', value: 'TARJETA' },
  { label: 'Transferencia', value: 'TRANSFERENCIA' }
];

/** Diferencias menores a un centavo se consideran cero (los montos son DECIMAL(18,2) en el backend). */
const EPSILON = 0.005;

@Component({
  selector: 'app-creditos',
  standalone: true,
  imports: [
    DatePipe,
    MonedaPipe,
    FormsModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    ProgressBarModule,
    SelectModule,
    TableModule,
    TagModule,
    HasPermissionDirective
  ],
  templateUrl: './creditos.component.html'
})
export class CreditosComponent implements OnInit {
  private readonly creditoService = inject(CreditoService);
  private readonly messageService = inject(MessageService);
  private readonly config = inject(ConfigService);

  readonly permisos = PERMISOS;
  readonly filtrosEstado = FILTROS_ESTADO;
  readonly metodosPago = METODOS_PAGO;

  readonly creditos = signal<CreditoResumen[]>([]);
  readonly cargando = signal(false);

  readonly filtroEstado = signal<FiltroEstado>('PENDIENTE');
  readonly filtroCliente = signal<number | null>(null);

  /** Clientes que tienen al menos un crédito, para el filtro (evita cargar todo el catálogo de clientes). */
  readonly opcionesCliente = computed(() => {
    const porId = new Map<number, string>();
    for (const c of this.creditos()) porId.set(c.clienteId, c.clienteNombre);
    return [...porId].map(([value, label]) => ({ value, label })).sort((a, b) => a.label.localeCompare(b.label));
  });

  readonly visibles = computed(() => {
    const estado = this.filtroEstado();
    const cliente = this.filtroCliente();
    return this.creditos().filter((c) => (estado === 'TODOS' || c.estado === estado) && (cliente === null || c.clienteId === cliente));
  });

  // Los indicadores resumen SIEMPRE cuentan lo pendiente de todos los clientes, sin importar el filtro activo.
  private readonly pendientes = computed(() => this.creditos().filter((c) => c.estado === 'PENDIENTE'));
  readonly totalPorCobrar = computed(() => this.pendientes().reduce((acc, c) => acc + c.saldoPendiente, 0));
  readonly cantidadPendientes = computed(() => this.pendientes().length);
  private readonly vencidos = computed(() => this.pendientes().filter((c) => c.vencido));
  readonly cantidadVencidos = computed(() => this.vencidos().length);
  readonly totalVencido = computed(() => this.vencidos().reduce((acc, c) => acc + c.saldoPendiente, 0));

  // Detalle
  readonly mostrarDetalle = signal(false);
  readonly detalle = signal<Credito | null>(null);
  readonly cargandoDetalle = signal(false);

  // Abono
  readonly mostrarAbono = signal(false);
  readonly creditoAbono = signal<CreditoResumen | null>(null);
  readonly abonando = signal(false);
  abono: AbonoCreate = this.abonoVacio();

  ngOnInit(): void {
    this.cargar();
  }

  private abonoVacio(): AbonoCreate {
    return { metodo: 'EFECTIVO', monto: 0, referencia: null };
  }

  private cargar(): void {
    this.cargando.set(true);
    this.creditoService.listar().subscribe({
      next: (creditos) => {
        this.creditos.set(creditos);
        this.cargando.set(false);
      },
      error: () => this.cargando.set(false)
    });
  }

  porcentajePagado(credito: CreditoResumen): number {
    if (credito.montoOriginal <= 0) return 0;
    return Math.round(((credito.montoOriginal - credito.saldoPendiente) / credito.montoOriginal) * 100);
  }

  totalAbonado(credito: Credito): number {
    return credito.montoOriginal - credito.saldoPendiente;
  }

  /** Lo que el cliente pagó al momento de la venta, antes de que naciera el crédito. */
  pagoInicial(credito: Credito): number {
    return credito.totalVenta - credito.montoOriginal;
  }

  severidadEstado(estado: EstadoCredito): 'warn' | 'success' | 'danger' {
    return estado === 'PENDIENTE' ? 'warn' : estado === 'PAGADO' ? 'success' : 'danger';
  }

  verDetalle(credito: CreditoResumen): void {
    this.detalle.set(null);
    this.cargandoDetalle.set(true);
    this.mostrarDetalle.set(true);
    this.creditoService.obtener(credito.id).subscribe({
      next: (detalle) => {
        this.detalle.set(detalle);
        this.cargandoDetalle.set(false);
      },
      error: () => {
        this.cargandoDetalle.set(false);
        this.mostrarDetalle.set(false);
      }
    });
  }

  abrirAbono(credito: CreditoResumen): void {
    this.creditoAbono.set(credito);
    this.abono = { ...this.abonoVacio(), monto: credito.saldoPendiente };
    this.mostrarAbono.set(true);
  }

  pagarSaldoCompleto(): void {
    const credito = this.creditoAbono();
    if (credito) this.abono.monto = credito.saldoPendiente;
  }

  /** Método (no computed): "abono" es un objeto mutable enlazado con ngModel, no un signal. */
  abonoValido(): boolean {
    const credito = this.creditoAbono();
    const monto = this.abono.monto;
    return !!credito && monto > 0 && monto <= credito.saldoPendiente + EPSILON;
  }

  saldoTrasAbono(): number {
    const credito = this.creditoAbono();
    return credito ? Math.max(0, credito.saldoPendiente - (this.abono.monto || 0)) : 0;
  }

  confirmarAbono(): void {
    const credito = this.creditoAbono();
    if (!credito || !this.abonoValido()) return;

    this.abonando.set(true);
    this.creditoService
      .abonar(credito.id, { ...this.abono, referencia: this.abono.referencia?.trim() || null })
      .subscribe({
        next: (actualizado) => {
          this.abonando.set(false);
          this.mostrarAbono.set(false);
          // El detalle abierto (si lo hay) se refresca con la respuesta; el listado se recarga para el saldo/estado.
          if (this.mostrarDetalle()) this.detalle.set(actualizado);
          this.cargar();
          const saldado = actualizado.estado === 'PAGADO';
          this.messageService.add({
            severity: 'success',
            summary: saldado ? 'Crédito saldado' : 'Abono registrado',
            detail: saldado
              ? `${actualizado.clienteNombre} canceló por completo el crédito de la venta #${actualizado.ventaFolio}.`
              : `Saldo pendiente: ${this.config.simboloMoneda()}${actualizado.saldoPendiente.toFixed(2)}`,
            life: 6000
          });
        },
        error: () => this.abonando.set(false)
      });
  }
}
