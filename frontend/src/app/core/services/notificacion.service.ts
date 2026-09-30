import { Injectable, computed, inject, signal } from '@angular/core';
import { MessageService } from 'primeng/api';
import { Subscription } from 'rxjs';
import { Inventario, esStockBajo } from '../models/inventario.models';
import { PERMISOS } from '../security/permisos';
import { AuthService } from './auth.service';
import { InventarioService } from './inventario.service';

export interface Notificacion {
  id: string;
  tipo: 'stock-bajo' | 'sin-stock';
  titulo: string;
  mensaje: string;
  fecha: Date;
  leida: boolean;
  ruta: string;
}

/** Estado persistido por notificación: cuándo se detectó por primera vez y si ya se leyó. */
type EstadoGuardado = Record<string, { fecha: string; leida: boolean }>;

/**
 * Notificaciones de la campanita del encabezado (estilo Facebook). Por ahora genera alertas de
 * stock mínimo a partir de GET /api/inventario: no hay tabla de notificaciones en el backend, así
 * que el estado leída/no leída se guarda en localStorage por usuario. Cuando un producto se
 * repone por encima del mínimo, su notificación desaparece; si vuelve a bajar, aparece como nueva.
 *
 * Sin sondeo periódico: se consulta al entrar, al abrir la campanita y después de cada operación
 * que mueve stock (venta, anulación de venta, compra, anulación de compra, ajuste), que llaman a
 * refrescar(). Abrir la campanita cubre los movimientos hechos por otros usuarios/cajas.
 */
@Injectable({ providedIn: 'root' })
export class NotificacionService {
  private readonly authService = inject(AuthService);
  private readonly inventarioService = inject(InventarioService);
  private readonly messageService = inject(MessageService);

  private readonly lista = signal<Notificacion[]>([]);
  private consulta: Subscription | null = null;
  private primeraCarga = true;

  readonly notificaciones = this.lista.asReadonly();
  readonly noLeidas = computed(() => this.lista().filter((n) => !n.leida).length);

  /** Primera consulta de la sesión; la llama el layout al entrar autenticado. */
  iniciar(): void {
    this.primeraCarga = true;
    this.refrescar();
  }

  detener(): void {
    this.consulta?.unsubscribe();
    this.consulta = null;
    this.lista.set([]);
  }

  /** Vuelve a revisar el stock mínimo; se llama después de cada operación que mueve stock. */
  refrescar(): void {
    if (!this.authService.tienePermiso(PERMISOS.InventarioVer)) return;
    const sucursalId = this.authService.usuario()?.sucursalId ?? undefined;
    // Si llegan dos refrescos seguidos (ej. venta + abrir campanita), solo cuenta la respuesta más reciente.
    this.consulta?.unsubscribe();
    this.consulta = this.inventarioService.listar(sucursalId).subscribe({
      next: (inventario) => this.procesar(inventario),
      error: () => {
        /* un fallo de red no debe interrumpir al usuario: se reintenta en la siguiente operación */
      }
    });
  }

  marcarLeida(id: string): void {
    this.actualizarLeidas((n) => n.id === id);
  }

  marcarTodasLeidas(): void {
    this.actualizarLeidas(() => true);
  }

  private procesar(inventario: Inventario[]): void {
    const guardado = this.leerEstado();
    const nuevoEstado: EstadoGuardado = {};
    const nuevas: Notificacion[] = [];

    const lista = inventario.filter(esStockBajo).map((item) => {
      const id = `stock-${item.productoId}-${item.sucursalId}`;
      const previo = guardado[id];
      const sinStock = item.stockActual <= 0;
      const notificacion: Notificacion = {
        id,
        tipo: sinStock ? 'sin-stock' : 'stock-bajo',
        titulo: item.productoNombre,
        mensaje: sinStock
          ? `se agotó (mínimo ${item.stockMinimo}).`
          : `llegó a su stock mínimo: quedan ${item.stockActual} (mínimo ${item.stockMinimo}).`,
        fecha: previo ? new Date(previo.fecha) : new Date(),
        leida: previo?.leida ?? false,
        ruta: '/inventario'
      };
      nuevoEstado[id] = { fecha: notificacion.fecha.toISOString(), leida: notificacion.leida };
      if (!previo) nuevas.push(notificacion);
      return notificacion;
    });

    lista.sort((a, b) => b.fecha.getTime() - a.fecha.getTime());
    this.lista.set(lista);
    this.guardarEstado(nuevoEstado);
    this.avisar(nuevas);
    this.primeraCarga = false;
  }

  /** Toast de alerta para las notificaciones recién detectadas (agrupado si son varias). */
  private avisar(nuevas: Notificacion[]): void {
    if (nuevas.length === 0) return;
    if (nuevas.length === 1) {
      const n = nuevas[0];
      this.messageService.add({ severity: 'warn', summary: 'Stock mínimo', detail: `${n.titulo} ${n.mensaje}`, life: 6000 });
      return;
    }
    this.messageService.add({
      severity: 'warn',
      summary: 'Stock mínimo',
      detail: `${nuevas.length} productos ${this.primeraCarga ? 'están' : 'llegaron'} en su stock mínimo. Revisa las notificaciones.`,
      life: 6000
    });
  }

  private actualizarLeidas(debeMarcar: (n: Notificacion) => boolean): void {
    this.lista.update((lista) => lista.map((n) => (debeMarcar(n) ? { ...n, leida: true } : n)));
    const estado: EstadoGuardado = {};
    for (const n of this.lista()) estado[n.id] = { fecha: n.fecha.toISOString(), leida: n.leida };
    this.guardarEstado(estado);
  }

  private get claveAlmacenamiento(): string {
    return `mm.notificaciones.${this.authService.usuario()?.id ?? 'anonimo'}`;
  }

  private leerEstado(): EstadoGuardado {
    try {
      return JSON.parse(localStorage.getItem(this.claveAlmacenamiento) ?? '{}') as EstadoGuardado;
    } catch {
      return {};
    }
  }

  private guardarEstado(estado: EstadoGuardado): void {
    try {
      localStorage.setItem(this.claveAlmacenamiento, JSON.stringify(estado));
    } catch {
      /* almacenamiento no disponible (modo privado): las notificaciones siguen funcionando en memoria */
    }
  }
}
