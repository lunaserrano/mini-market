import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { TagModule } from 'primeng/tag';
import { MessageService } from 'primeng/api';
import { Observable, forkJoin } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { InventarioService } from '../../core/services/inventario.service';
import { NotificacionService } from '../../core/services/notificacion.service';
import { ProductoService } from '../../core/services/producto.service';
import { Inventario, MovimientoInventario, esStockBajo } from '../../core/models/inventario.models';
import { Producto } from '../../core/models/producto.models';
import { PERMISOS } from '../../core/security/permisos';

@Component({
  selector: 'app-inventario',
  standalone: true,
  imports: [DatePipe, FormsModule, ButtonModule, DialogModule, InputTextModule, SelectModule, TableModule, TabsModule, TagModule],
  templateUrl: './inventario.component.html'
})
export class InventarioComponent implements OnInit {
  readonly inventario = signal<Inventario[]>([]);
  readonly movimientos = signal<MovimientoInventario[]>([]);
  readonly productos = signal<Producto[]>([]);
  readonly cargando = signal(false);
  readonly guardando = signal(false);
  readonly totalStockBajo = computed(() => this.inventario().filter(esStockBajo).length);

  readonly esStockBajo = esStockBajo;
  readonly puedeAjustar: boolean;

  readonly mostrarAjuste = signal(false);
  /** Fila desde la que se abrió el ajuste (doble clic); null = se eligió el producto en el selector. */
  readonly itemSeleccionado = signal<Inventario | null>(null);
  ajuste = { productoId: null as number | null, cantidadAjuste: 0, stockMinimo: 0, observacion: '' };

  constructor(
    private readonly inventarioService: InventarioService,
    private readonly productoService: ProductoService,
    private readonly authService: AuthService,
    private readonly notificacionService: NotificacionService,
    private readonly messageService: MessageService
  ) {
    this.puedeAjustar = this.authService.tienePermiso(PERMISOS.InventarioAjustar);
  }

  ngOnInit(): void {
    this.productoService.listar().subscribe((productos) => this.productos.set(productos));
    this.cargar();
  }

  private get sucursalId(): number | null {
    return this.authService.usuario()?.sucursalId ?? null;
  }

  private cargar(): void {
    this.cargando.set(true);
    this.inventarioService.listar(this.sucursalId ?? undefined).subscribe({
      next: (inventario) => {
        this.inventario.set(inventario);
        this.cargando.set(false);
      },
      error: () => this.cargando.set(false)
    });
    this.inventarioService.listarMovimientos().subscribe((movimientos) => this.movimientos.set(movimientos));
  }

  abrirAjuste(item: Inventario | null = null): void {
    if (!this.puedeAjustar) return;
    this.itemSeleccionado.set(item);
    this.ajuste = { productoId: item?.productoId ?? null, cantidadAjuste: 0, stockMinimo: item?.stockMinimo ?? 0, observacion: '' };
    this.mostrarAjuste.set(true);
  }

  /** Al elegir un producto en el selector, precarga su stock mínimo actual (si ya tiene inventario). */
  cambiarProducto(productoId: number | null): void {
    this.ajuste.productoId = productoId;
    const item = this.inventario().find((i) => i.productoId === productoId) ?? null;
    this.ajuste.stockMinimo = item?.stockMinimo ?? 0;
  }

  /** Stock que quedará después del ajuste, para mostrarlo en vivo en el diálogo. */
  stockResultante(): number | null {
    const item = this.itemSeleccionado() ?? this.inventario().find((i) => i.productoId === this.ajuste.productoId);
    return item ? item.stockActual + (Number(this.ajuste.cantidadAjuste) || 0) : null;
  }

  private stockMinimoOriginal(): number {
    return this.inventario().find((i) => i.productoId === this.ajuste.productoId)?.stockMinimo ?? 0;
  }

  puedeGuardar(): boolean {
    const minimo = Number(this.ajuste.stockMinimo);
    const cambiaMinimo = Number.isFinite(minimo) && minimo >= 0 && minimo !== this.stockMinimoOriginal();
    return !!this.ajuste.productoId && (Number(this.ajuste.cantidadAjuste) !== 0 || cambiaMinimo);
  }

  confirmarAjuste(): void {
    const productoId = this.ajuste.productoId;
    const sucursalId = this.sucursalId;
    if (!productoId || !sucursalId || !this.puedeGuardar()) return;

    const cantidad = Number(this.ajuste.cantidadAjuste) || 0;
    const minimo = Number(this.ajuste.stockMinimo);
    const cambiaMinimo = minimo !== this.stockMinimoOriginal();
    if (minimo < 0) {
      this.messageService.add({ severity: 'warn', summary: 'Stock mínimo no válido', detail: 'El stock mínimo no puede ser negativo.' });
      return;
    }

    const operaciones: Observable<void>[] = [];
    if (cambiaMinimo) operaciones.push(this.inventarioService.actualizarStockMinimo({ productoId, sucursalId, stockMinimo: minimo }));
    if (cantidad !== 0) {
      operaciones.push(
        this.inventarioService.ajustar({ productoId, sucursalId, cantidadAjuste: cantidad, observacion: this.ajuste.observacion })
      );
    }

    this.guardando.set(true);
    forkJoin(operaciones).subscribe({
      next: () => {
        this.guardando.set(false);
        this.mostrarAjuste.set(false);
        this.cargar();
        this.notificacionService.refrescar();
        this.messageService.add({
          severity: 'success',
          summary: cantidad !== 0 ? 'Inventario ajustado' : 'Stock mínimo actualizado'
        });
      },
      error: () => {
        this.guardando.set(false);
        this.cargar();
      }
    });
  }
}
