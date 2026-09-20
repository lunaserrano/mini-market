import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { MessageService } from 'primeng/api';
import { Subject, catchError, debounceTime, of, switchMap } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { CajaService } from '../../core/services/caja.service';
import { ClienteService } from '../../core/services/catalogo.service';
import { ConfigService } from '../../core/services/config.service';
import { ProductoService } from '../../core/services/producto.service';
import { VentaService } from '../../core/services/venta.service';
import { Caja } from '../../core/models/caja.models';
import { Cliente } from '../../core/models/catalogo.models';
import { ProductoPos, TipoPrecio } from '../../core/models/producto.models';
import { LineaCarrito, PagoVentaCreate } from '../../core/models/venta.models';
import { DosDecimalesDirective } from '../../core/directives/dos-decimales.directive';
import { MonedaPipe } from '../../core/pipes/moneda.pipe';
import { PERMISOS } from '../../core/security/permisos';

/** Diferencias menores a un centavo se consideran cero (los montos son DECIMAL(18,2) en el backend). */
const EPSILON = 0.005;

const METODOS_PAGO = [
  { label: 'Efectivo', value: 'EFECTIVO' },
  { label: 'Tarjeta', value: 'TARJETA' },
  { label: 'Transferencia', value: 'TRANSFERENCIA' }
];

@Component({
  selector: 'app-pos-ventas',
  standalone: true,
  imports: [MonedaPipe, DosDecimalesDirective, FormsModule, ButtonModule, DialogModule, InputTextModule, SelectModule, TableModule],
  templateUrl: './pos-ventas.component.html'
})
export class PosVentasComponent implements OnInit {
  private readonly authService = inject(AuthService);

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

  // Cliente y venta a crédito. Solo quien puede listar clientes ve el selector; solo quien puede
  // otorgar crédito ve la opción "a crédito" (el backend valida ambos permisos de nuevo).
  readonly puedeElegirCliente = this.authService.tienePermiso(PERMISOS.ClientesVer, PERMISOS.CreditosOtorgar);
  readonly puedeVenderACredito = this.authService.tienePermiso(PERMISOS.CreditosOtorgar);
  readonly clientes = signal<Cliente[]>([]);
  clienteId: number | null = null;
  alCredito = false;
  /** yyyy-MM-dd; vacío = sin fecha de vencimiento. */
  fechaVencimiento = '';
  readonly hoy = new Date().toLocaleDateString('en-CA'); // yyyy-MM-dd en hora local

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

  /** Lo que queda a deber el cliente si se vende a crédito con los pagos actuales. */
  saldoCredito(): number {
    return Math.max(0, this.total() - this.totalPagado());
  }

  /** Método (no computed) por la misma razón que totalPagado(): depende de "pagos" y "clienteId", que no son signals. */
  puedeConfirmar(): boolean {
    if (this.alCredito) return this.clienteId !== null && this.saldoCredito() > EPSILON;
    return this.totalPagado() >= this.total();
  }

  cambiarCliente(clienteId: number | null): void {
    this.clienteId = clienteId;
    if (clienteId === null && this.alCredito) this.alternarCredito(false);
  }

  /** Al pasar a crédito el pago inicial arranca en 0 (todo a deber); al volver a contado, el pago cubre el total. */
  alternarCredito(activo: boolean): void {
    this.alCredito = activo;
    this.fechaVencimiento = '';
    this.pagos = [{ metodo: 'EFECTIVO', monto: activo ? 0 : this.total(), referencia: null }];
  }

  constructor(
    private readonly cajaService: CajaService,
    private readonly productoService: ProductoService,
    private readonly ventaService: VentaService,
    private readonly configService: ConfigService,
    private readonly messageService: MessageService,
    private readonly router: Router,
    private readonly clienteService: ClienteService
  ) {
    this.busqueda$
      .pipe(
        debounceTime(300),
        // Sin distinctUntilChanged: al agregar un producto el campo se limpia por código (sin emitir),
        // y volver a buscar el mismo texto quedaba bloqueado. Además el error se captura por búsqueda
        // para que un fallo de red no mate la suscripción y deje el buscador muerto.
        switchMap((termino) =>
          termino.trim().length > 0
            ? this.productoService.buscarParaPos(termino).pipe(catchError(() => of<ProductoPos[]>([])))
            : of<ProductoPos[]>([])
        )
      )
      .subscribe((resultados) => {
        this.resultados.set(resultados ?? []);
        // Un único producto con una única presentación: no hay nada que elegir, se agrega directo.
        if (resultados?.length === 1 && resultados[0].tiposPrecio.length === 1) {
          this.agregarAlCarrito(resultados[0], resultados[0].tiposPrecio[0]);
        }
      });
  }

  ngOnInit(): void {
    this.cajaService.obtenerActual().subscribe({
      next: (caja) => {
        this.caja.set(caja);
        this.cajaVerificada.set(true);
      },
      error: () => this.cajaVerificada.set(true)
    });

    if (this.puedeElegirCliente) {
      this.clienteService.listar().subscribe((clientes) => this.clientes.set(clientes.filter((c) => c.estado === 'A')));
    }
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
    } else {
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
    }
    this.termino = '';
    this.resultados.set([]);
  }

  /**
   * Un campo vacío o en 0 (mientras se borra para escribir otra cantidad) no debe quitar la fila:
   * se ignora y se conserva la última cantidad válida. Para quitar una línea está el botón de basura.
   */
  actualizarCantidad(linea: LineaCarrito, cantidad: number | null): void {
    if (cantidad === null || !Number.isFinite(cantidad) || cantidad <= 0) return;
    this.carrito.update((lineas) =>
      lineas.map((l) => (l === linea ? { ...l, cantidad, subtotal: l.precioUnitario * cantidad - l.descuento } : l))
    );
  }

  /** Al salir del campo con un valor inválido, vuelve a mostrar la última cantidad válida. */
  restaurarCantidad(linea: LineaCarrito, evento: Event): void {
    const input = evento.target as HTMLInputElement;
    const valor = Number(input.value);
    if (input.value === '' || !Number.isFinite(valor) || valor <= 0) {
      const actual = this.carrito().find((l) => l.productoId === linea.productoId && l.tipoPrecioId === linea.tipoPrecioId);
      input.value = String(actual?.cantidad ?? 1);
    }
  }

  quitarLinea(linea: LineaCarrito): void {
    this.carrito.update((lineas) => lineas.filter((l) => l !== linea));
  }

  abrirPago(): void {
    if (this.carrito().length === 0) return;
    this.alCredito = false;
    this.fechaVencimiento = '';
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
    const simbolo = this.configService.simboloMoneda();
    if (this.alCredito) {
      if (this.clienteId === null || this.saldoCredito() <= EPSILON) {
        this.messageService.add({
          severity: 'warn',
          summary: 'Crédito no válido',
          detail:
            this.clienteId === null
              ? 'Selecciona un cliente para vender a crédito.'
              : 'Los pagos ya cubren el total: no queda saldo para dejar a crédito.',
          life: 5000
        });
        return;
      }
    } else {
      const faltante = this.total() - this.totalPagado();
      if (faltante > 0.01) {
        this.messageService.add({
          severity: 'warn',
          summary: 'Pago incompleto',
          detail: `Faltan ${simbolo}${faltante.toFixed(2)} para cubrir el total.`,
          life: 5000
        });
        return;
      }
    }

    const alCredito = this.alCredito;
    const saldoCredito = this.saldoCredito();

    this.procesandoVenta.set(true);
    this.ventaService
      .crear({
        clienteId: this.clienteId,
        detalles: this.carrito().map((l) => ({
          productoId: l.productoId,
          tipoPrecioId: l.tipoPrecioId,
          cantidad: l.cantidad,
          descuento: l.descuento
        })),
        // Un pago en 0 no se envía (el backend exige monto > 0); en una venta a crédito la lista puede quedar vacía.
        pagos: this.pagos.filter((p) => p.monto > 0),
        alCredito,
        fechaVencimiento: alCredito && this.fechaVencimiento ? this.fechaVencimiento : null
      })
      .subscribe({
        next: (venta) => {
          this.procesandoVenta.set(false);
          this.mostrarPago.set(false);
          this.carrito.set([]);
          this.clienteId = null;
          this.alCredito = false;
          this.messageService.add({
            severity: 'success',
            summary: alCredito ? 'Venta a crédito registrada' : 'Venta registrada',
            detail: alCredito
              ? `Folio #${venta.folio} · Total ${simbolo}${venta.total.toFixed(2)} · A crédito ${simbolo}${saldoCredito.toFixed(2)}`
              : `Folio #${venta.folio} · Total ${simbolo}${venta.total.toFixed(2)}`,
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
