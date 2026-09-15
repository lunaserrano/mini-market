import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { MessageService } from 'primeng/api';
import { Subject, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { CajaService } from '../../core/services/caja.service';
import { ConfigService } from '../../core/services/config.service';
import { ProductoService } from '../../core/services/producto.service';
import { VentaService } from '../../core/services/venta.service';
import { Caja } from '../../core/models/caja.models';
import { ProductoPos, TipoPrecio } from '../../core/models/producto.models';
import { LineaCarrito, PagoVentaCreate } from '../../core/models/venta.models';
import { MonedaPipe } from '../../core/pipes/moneda.pipe';

const METODOS_PAGO = [
  { label: 'Efectivo', value: 'EFECTIVO' },
  { label: 'Tarjeta', value: 'TARJETA' },
  { label: 'Transferencia', value: 'TRANSFERENCIA' }
];

@Component({
  selector: 'app-pos-ventas',
  standalone: true,
  imports: [MonedaPipe, FormsModule, ButtonModule, DialogModule, InputTextModule, SelectModule, TableModule],
  templateUrl: './pos-ventas.component.html'
})
export class PosVentasComponent implements OnInit {
  readonly caja = signal<Caja | null>(null);
  readonly cajaVerificada = signal(false);

  termino = '';
  readonly resultados = signal<ProductoPos[]>([]);
  private readonly busqueda$ = new Subject<string>();

  readonly seleccionandoTipoPrecio = signal(false);
  productoSeleccionado: ProductoPos | null = null;
  tipoPrecioSeleccionado: TipoPrecio | null = null;

  readonly carrito = signal<LineaCarrito[]>([]);
  readonly subtotal = computed(() => this.carrito().reduce((acc, l) => acc + l.precioUnitario * l.cantidad, 0));
  readonly descuentoTotal = computed(() => this.carrito().reduce((acc, l) => acc + l.descuento, 0));
  readonly total = computed(() => this.subtotal() - this.descuentoTotal());

  readonly metodosPago = METODOS_PAGO;
  readonly mostrarPago = signal(false);
  pagos: PagoVentaCreate[] = [];
  readonly procesandoVenta = signal(false);

  /**
   * Método normal, no computed(): "pagos" es un array mutable plano (no un signal), y un
   * computed() sin dependencias de signal se memoiza una sola vez para siempre — con eso el botón
   * de "Confirmar venta" quedaba deshabilitado permanentemente aunque se completaran los pagos.
   * Un método se re-evalúa en cada ciclo de detección de cambios, así que sí refleja los montos
   * actuales de "pagos" al escribir en los inputs.
   */
  totalPagado(): number {
    return this.pagos.reduce((acc, p) => acc + (p.monto || 0), 0);
  }

  constructor(
    private readonly cajaService: CajaService,
    private readonly productoService: ProductoService,
    private readonly ventaService: VentaService,
    private readonly configService: ConfigService,
    private readonly messageService: MessageService,
    private readonly router: Router
  ) {
    this.busqueda$
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((termino) => (termino.trim().length > 0 ? this.productoService.buscarParaPos(termino) : []))
      )
      .subscribe((resultados) => this.resultados.set(resultados ?? []));
  }

  ngOnInit(): void {
    this.cajaService.obtenerActual().subscribe({
      next: (caja) => {
        this.caja.set(caja);
        this.cajaVerificada.set(true);
      },
      error: () => this.cajaVerificada.set(true)
    });
  }

  onBuscar(): void {
    this.busqueda$.next(this.termino);
  }

  seleccionarProducto(producto: ProductoPos): void {
    if (producto.tiposPrecio.length === 1) {
      this.agregarAlCarrito(producto, producto.tiposPrecio[0]);
      return;
    }
    this.productoSeleccionado = producto;
    this.tipoPrecioSeleccionado = producto.tiposPrecio.find((t) => t.esDefault) ?? producto.tiposPrecio[0] ?? null;
    this.seleccionandoTipoPrecio.set(true);
  }

  confirmarTipoPrecio(): void {
    if (!this.productoSeleccionado || !this.tipoPrecioSeleccionado) return;
    this.agregarAlCarrito(this.productoSeleccionado, this.tipoPrecioSeleccionado);
    this.seleccionandoTipoPrecio.set(false);
  }

  private agregarAlCarrito(producto: ProductoPos, tipoPrecio: TipoPrecio): void {
    const existente = this.carrito().find((l) => l.productoId === producto.productoId && l.tipoPrecioId === tipoPrecio.id);
    if (existente) {
      this.actualizarCantidad(existente, existente.cantidad + 1);
      return;
    }

    this.carrito.update((lineas) => [
      ...lineas,
      {
        productoId: producto.productoId,
        productoNombre: producto.nombre,
        tipoPrecioId: tipoPrecio.id,
        tipoPrecioNombre: tipoPrecio.nombre,
        cantidad: 1,
        precioUnitario: tipoPrecio.precioVenta,
        descuento: 0,
        subtotal: tipoPrecio.precioVenta
      }
    ]);
    this.termino = '';
    this.resultados.set([]);
  }

  actualizarCantidad(linea: LineaCarrito, cantidad: number): void {
    if (cantidad <= 0) {
      this.quitarLinea(linea);
      return;
    }
    this.carrito.update((lineas) =>
      lineas.map((l) => (l === linea ? { ...l, cantidad, subtotal: l.precioUnitario * cantidad - l.descuento } : l))
    );
  }

  quitarLinea(linea: LineaCarrito): void {
    this.carrito.update((lineas) => lineas.filter((l) => l !== linea));
  }

  abrirPago(): void {
    if (this.carrito().length === 0) return;
    this.pagos = [{ metodo: 'EFECTIVO', monto: this.total(), referencia: null }];
    this.mostrarPago.set(true);
  }

  agregarLineaPago(): void {
    this.pagos = [...this.pagos, { metodo: 'EFECTIVO', monto: 0, referencia: null }];
  }

  quitarLineaPago(index: number): void {
    this.pagos = this.pagos.filter((_, i) => i !== index);
  }

  confirmarVenta(): void {
    if (!this.caja()) return;

    // Validación explícita además del [disabled] del botón: por si el total cambia entre que se
    // renderiza y se hace clic (o el usuario logra saltarse el disabled), nunca se manda una venta
    // con pagos insuficientes — se avisa con un toast en vez de dejar que el backend tire una excepción.
    const faltante = this.total() - this.totalPagado();
    if (faltante > 0.01) {
      this.messageService.add({
        severity: 'warn',
        summary: 'Pago incompleto',
        detail: `Faltan ${this.configService.simboloMoneda()}${faltante.toFixed(2)} para cubrir el total.`,
        life: 5000
      });
      return;
    }

    this.procesandoVenta.set(true);
    this.ventaService
      .crear({
        clienteId: null,
        detalles: this.carrito().map((l) => ({
          productoId: l.productoId,
          tipoPrecioId: l.tipoPrecioId,
          cantidad: l.cantidad,
          descuento: l.descuento
        })),
        pagos: this.pagos
      })
      .subscribe({
        next: (venta) => {
          this.procesandoVenta.set(false);
          this.mostrarPago.set(false);
          this.carrito.set([]);
          this.messageService.add({
            severity: 'success',
            summary: 'Venta registrada',
            detail: `Folio #${venta.folio} · Total ${this.configService.simboloMoneda()}${venta.total.toFixed(2)}`,
            life: 6000
          });
        },
        error: () => this.procesandoVenta.set(false)
      });
  }

  irAAbrirCaja(): void {
    this.router.navigate(['/caja']);
  }
}
