import { Component, computed, inject, signal, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { Popover, PopoverModule } from 'primeng/popover';
import { Notificacion, NotificacionService } from '../../../core/services/notificacion.service';

/**
 * Campanita del encabezado con la lista de notificaciones, al estilo de Facebook: contador rojo
 * sobre el ícono, panel flotante con filtro "Todas / No leídas", fondo tenue y punto azul en las
 * no leídas, y "Marcar todas como leídas".
 */
@Component({
  selector: 'app-notificaciones',
  standalone: true,
  imports: [ButtonModule, PopoverModule],
  template: `
    <div class="relative inline-flex">
      <p-button
        icon="pi pi-bell"
        [text]="true"
        [rounded]="true"
        severity="secondary"
        title="Notificaciones"
        ariaLabel="Notificaciones"
        (onClick)="abrirPanel(panel, $event)"
      />
      @if (servicio.noLeidas() > 0) {
        <span
          class="pointer-events-none absolute top-0.5 right-0.5 min-w-[1.15rem] h-[1.15rem] px-1 rounded-full bg-red-500 text-white text-[0.68rem] font-bold leading-[1.15rem] text-center ring-2 ring-surface-0 dark:ring-surface-900"
        >
          {{ servicio.noLeidas() > 9 ? '9+' : servicio.noLeidas() }}
        </span>
      }
    </div>

    <p-popover #panel styleClass="!p-0">
      <div class="w-[22rem] max-w-[calc(100vw-2rem)] -m-3">
        <div class="flex items-center justify-between px-4 pt-3 pb-1">
          <h3 class="m-0 text-lg font-bold">Notificaciones</h3>
          @if (servicio.noLeidas() > 0) {
            <button type="button" class="text-xs text-primary hover:underline bg-transparent border-0 cursor-pointer p-0" (click)="servicio.marcarTodasLeidas()">
              Marcar todas como leídas
            </button>
          }
        </div>

        <div class="flex gap-1 px-3 pb-2">
          <button type="button" [class]="claseFiltro(!soloNoLeidas())" (click)="soloNoLeidas.set(false)">Todas</button>
          <button type="button" [class]="claseFiltro(soloNoLeidas())" (click)="soloNoLeidas.set(true)">No leídas</button>
        </div>

        <div class="max-h-[26rem] overflow-y-auto px-2 pb-2">
          @for (n of visibles(); track n.id) {
            <button
              type="button"
              [class]="claseItem(n)"
              (click)="abrir(n)"
            >
              <span
                class="shrink-0 w-11 h-11 rounded-full flex items-center justify-center"
                [class.bg-red-100]="n.tipo === 'sin-stock'"
                [class.text-red-600]="n.tipo === 'sin-stock'"
                [class.bg-amber-100]="n.tipo === 'stock-bajo'"
                [class.text-amber-600]="n.tipo === 'stock-bajo'"
              >
                <i class="pi text-lg" [class.pi-box]="n.tipo === 'stock-bajo'" [class.pi-exclamation-circle]="n.tipo === 'sin-stock'"></i>
              </span>
              <span class="flex-1 min-w-0">
                <span class="block text-sm text-surface-700 dark:text-surface-200 leading-snug">
                  <strong class="text-surface-900 dark:text-surface-0">{{ n.titulo }}</strong> {{ n.mensaje }}
                </span>
                <span class="block text-xs mt-0.5 font-semibold" [class.text-red-600]="n.tipo === 'sin-stock'" [class.text-amber-600]="n.tipo === 'stock-bajo'">
                  {{ n.tipo === 'sin-stock' ? 'Agotado' : 'Stock mínimo' }}
                </span>
              </span>
              @if (!n.leida) {
                <span class="shrink-0 w-2.5 h-2.5 rounded-full bg-primary"></span>
              }
            </button>
          } @empty {
            <div class="flex flex-col items-center gap-2 py-10 text-surface-400">
              <i class="pi pi-bell-slash text-3xl"></i>
              <span class="text-sm">{{ soloNoLeidas() ? 'No tienes notificaciones sin leer.' : 'No tienes notificaciones.' }}</span>
            </div>
          }
        </div>
      </div>
    </p-popover>
  `
})
export class NotificacionesComponent {
  readonly servicio = inject(NotificacionService);
  private readonly router = inject(Router);

  readonly panel = viewChild.required<Popover>('panel');
  readonly soloNoLeidas = signal(false);
  readonly visibles = computed(() =>
    this.soloNoLeidas() ? this.servicio.notificaciones().filter((n) => !n.leida) : this.servicio.notificaciones()
  );

  abrir(notificacion: Notificacion): void {
    this.servicio.marcarLeida(notificacion.id);
    this.panel().hide();
    this.router.navigateByUrl(notificacion.ruta);
  }

  claseFiltro(activo: boolean): string {
    const base = 'px-3 py-1.5 rounded-full text-sm font-semibold border-0 cursor-pointer transition-colors';
    return activo
      ? `${base} bg-primary-100 text-primary-700 dark:bg-primary-900/40 dark:text-primary-300`
      : `${base} bg-transparent text-surface-600 dark:text-surface-300 hover:bg-surface-100 dark:hover:bg-surface-800`;
  }

  claseItem(n: Notificacion): string {
    const base = 'w-full flex items-center gap-3 p-2 rounded-lg text-left border-0 cursor-pointer transition-colors hover:bg-surface-100 dark:hover:bg-surface-800';
    return n.leida ? `${base} bg-transparent` : `${base} bg-primary-50 dark:bg-primary-900/20`;
  }

  /** Al abrir la campanita se revisa el stock de nuevo, para incluir movimientos hechos por otros usuarios. */
  abrirPanel(panel: Popover, evento: Event): void {
    if (!panel.overlayVisible) this.servicio.refrescar();
    panel.toggle(evento);
  }
}
