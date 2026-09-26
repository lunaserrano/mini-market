import { Component, OnInit, computed, signal } from '@angular/core';
import { animate, style, transition, trigger } from '@angular/animations';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs/operators';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { AuthService } from '../core/services/auth.service';
import { ConfigService } from '../core/services/config.service';
import { MENU_ITEMS } from './menu-items';

/** Breakpoint 'lg' de Tailwind: desde aquí el sidebar es un riel fijo; debajo, un cajón (drawer) superpuesto. */
const ANCHO_ESCRITORIO = 1024;

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ButtonModule, ToastModule],
  animations: [
    // Reinicia la animación en cada navegación porque rutaActual() cambia de valor (URL completa).
    trigger('fadeRuta', [
      transition('* <=> *', [
        style({ opacity: 0, transform: 'translateY(6px)' }),
        animate('220ms ease-out', style({ opacity: 1, transform: 'translateY(0)' }))
      ])
    ])
  ],
  template: `
    <p-toast />

    <!-- Fondo oscuro detrás del menú lateral cuando está abierto como cajón (móvil/tablet) -->
    @if (sidebarAbierto()) {
      <div class="fixed inset-0 z-40 bg-black/50 lg:hidden" (click)="cerrarEnMovil()"></div>
    }

    <div class="flex h-[100dvh] w-full overflow-hidden bg-surface-50 dark:bg-surface-950">
      <!-- Sidebar: cajón a pantalla completa en móvil/tablet, riel fijo (expandido/colapsado) en escritorio -->
      <aside
        class="fixed inset-y-0 left-0 z-50 flex flex-col w-72 shrink-0 bg-surface-0 dark:bg-surface-900 border-r border-surface-200 dark:border-surface-700 transition-transform duration-300 ease-in-out lg:static lg:translate-x-0 lg:transition-[width] lg:duration-300"
        [class.translate-x-0]="sidebarAbierto()"
        [class.-translate-x-full]="!sidebarAbierto()"
        [class.lg:w-64]="sidebarAbierto()"
        [class.lg:w-16]="!sidebarAbierto()"
      >
        <div class="h-16 shrink-0 flex items-center px-4 gap-2 border-b border-surface-200 dark:border-surface-700">
          <i class="pi pi-shop text-primary text-2xl"></i>
          @if (sidebarAbierto()) {
            <span class="font-bold text-lg whitespace-nowrap">Mini Market</span>
          }
          <p-button class="ml-auto lg:hidden" icon="pi pi-times" [text]="true" [rounded]="true" ariaLabel="Cerrar menú" (onClick)="cerrarEnMovil()" />
        </div>
        <nav class="flex-1 overflow-y-auto py-2">
          @for (entrada of menuVisible(); track entrada.item.route) {
            @if (entrada.encabezado && sidebarAbierto()) {
              <p class="px-6 pt-4 pb-1 m-0 text-xs font-semibold uppercase tracking-wide text-surface-400">{{ entrada.encabezado }}</p>
            }
            <a
              [routerLink]="entrada.item.route"
              routerLinkActive="bg-primary-50 dark:bg-primary-900/40 text-primary"
              class="flex items-center gap-3 px-4 py-2.5 mx-2 rounded-lg text-surface-700 dark:text-surface-200 hover:bg-surface-100 dark:hover:bg-surface-800 active:scale-[0.98] transition-all no-underline"
              (click)="cerrarEnMovil()"
            >
              <i [class]="entrada.item.icon" class="text-lg"></i>
              @if (sidebarAbierto()) {
                <span class="whitespace-nowrap">{{ entrada.item.label }}</span>
              }
            </a>
          }
        </nav>
      </aside>

      <!-- Contenido -->
      <div class="flex flex-col flex-1 min-w-0">
        <header class="h-16 shrink-0 flex items-center justify-between gap-2 px-3 sm:px-4 bg-surface-0 dark:bg-surface-900 border-b border-surface-200 dark:border-surface-700">
          <div class="flex items-center gap-2 min-w-0">
            <p-button icon="pi pi-bars" [text]="true" [rounded]="true" ariaLabel="Abrir menú" (onClick)="alternarSidebar()" />
            <span class="lg:hidden font-bold whitespace-nowrap">Mini Market</span>
          </div>
          <div class="flex items-center gap-1 sm:gap-3 min-w-0">
            <span class="hidden sm:inline text-sm text-surface-600 dark:text-surface-300 truncate max-w-[16rem]">
              {{ usuario()?.nombreCompleto }} · {{ usuario()?.rolNombre }}
            </span>
            <a routerLink="/auth/cambiar-password" title="Cambiar contraseña">
              <p-button icon="pi pi-key" [text]="true" [rounded]="true" severity="secondary" />
            </a>
            <p-button icon="pi pi-sign-out" title="Cerrar sesión" [text]="true" [rounded]="true" severity="secondary" (onClick)="authService.logout()" />
          </div>
        </header>
        <main class="flex-1 overflow-y-auto overflow-x-hidden p-3 sm:p-4 lg:p-6" [@fadeRuta]="rutaActual()">
          <router-outlet />
        </main>
      </div>
    </div>
  `
})
export class AppLayoutComponent implements OnInit {
  readonly sidebarAbierto = signal(this.esEscritorio());
  /** URL completa actual; cambia en cada navegación y así reinicia la animación de entrada del contenido. */
  readonly rutaActual = signal('');
  readonly usuario = computed(() => this.authService.usuario());
  /** Entradas del menú que el usuario puede ver según sus permisos, con el encabezado de sección donde empieza cada una. */
  readonly menuVisible = computed(() => {
    const visibles = MENU_ITEMS.filter((item) => this.authService.tienePermiso(...item.permisos));
    return visibles.map((item, i) => ({
      item,
      encabezado: item.seccion && item.seccion !== visibles[i - 1]?.seccion ? item.seccion : null
    }));
  });

  constructor(
    readonly authService: AuthService,
    private readonly configService: ConfigService,
    private readonly router: Router
  ) {
    this.router.events.pipe(filter((evento) => evento instanceof NavigationEnd)).subscribe((evento) => {
      this.rutaActual.set((evento as NavigationEnd).urlAfterRedirects);
    });
  }

  ngOnInit(): void {
    // Carga la moneda/zona horaria de la empresa una vez, para toda la sesión (incluye recargas
    // de página con el token todavía válido, ya que este layout es el shell de todas las rutas autenticadas).
    this.configService.cargar();
  }

  alternarSidebar(): void {
    this.sidebarAbierto.set(!this.sidebarAbierto());
  }

  /** En móvil/tablet el sidebar es un cajón superpuesto: se cierra al navegar o al tocar el fondo oscuro. */
  cerrarEnMovil(): void {
    if (!this.esEscritorio()) {
      this.sidebarAbierto.set(false);
    }
  }

  private esEscritorio(): boolean {
    return typeof window !== 'undefined' && window.innerWidth >= ANCHO_ESCRITORIO;
  }
}
