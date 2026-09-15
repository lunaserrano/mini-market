import { Component, OnInit, computed, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { AuthService } from '../core/services/auth.service';
import { ConfigService } from '../core/services/config.service';
import { MENU_ITEMS } from './menu-items';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ButtonModule, ToastModule],
  template: `
    <p-toast />
    <div class="flex h-screen w-full overflow-hidden bg-surface-50 dark:bg-surface-950">
      <!-- Sidebar -->
      <aside
        class="flex flex-col shrink-0 bg-surface-0 dark:bg-surface-900 border-r border-surface-200 dark:border-surface-700 transition-all"
        [class.w-64]="sidebarAbierto()"
        [class.w-16]="!sidebarAbierto()"
      >
        <div class="h-16 flex items-center px-4 gap-2 border-b border-surface-200 dark:border-surface-700">
          <i class="pi pi-shop text-primary text-2xl"></i>
          @if (sidebarAbierto()) {
            <span class="font-bold text-lg whitespace-nowrap">Mini Market</span>
          }
        </div>
        <nav class="flex-1 overflow-y-auto py-2">
          @for (item of menuVisible(); track item.route) {
            <a
              [routerLink]="item.route"
              routerLinkActive="bg-primary-50 dark:bg-primary-900/40 text-primary"
              class="flex items-center gap-3 px-4 py-2.5 mx-2 rounded-lg text-surface-700 dark:text-surface-200 hover:bg-surface-100 dark:hover:bg-surface-800 no-underline"
            >
              <i [class]="item.icon"></i>
              @if (sidebarAbierto()) {
                <span class="whitespace-nowrap">{{ item.label }}</span>
              }
            </a>
          }
        </nav>
      </aside>

      <!-- Contenido -->
      <div class="flex flex-col flex-1 min-w-0">
        <header
          class="h-16 shrink-0 flex items-center justify-between px-4 bg-surface-0 dark:bg-surface-900 border-b border-surface-200 dark:border-surface-700"
        >
          <p-button icon="pi pi-bars" [text]="true" [rounded]="true" (onClick)="sidebarAbierto.set(!sidebarAbierto())" />
          <div class="flex items-center gap-3">
            <span class="text-sm text-surface-600 dark:text-surface-300">
              {{ usuario()?.nombreCompleto }} · <span class="capitalize">{{ usuario()?.rol }}</span>
            </span>
            <p-button icon="pi pi-sign-out" [text]="true" [rounded]="true" severity="secondary" (onClick)="authService.logout()" />
          </div>
        </header>
        <main class="flex-1 overflow-y-auto p-4">
          <router-outlet />
        </main>
      </div>
    </div>
  `
})
export class AppLayoutComponent implements OnInit {
  readonly sidebarAbierto = signal(true);
  readonly usuario = computed(() => this.authService.usuario());
  readonly menuVisible = computed(() => {
    const rol = this.usuario()?.rol;
    return MENU_ITEMS.filter((item) => !!rol && item.roles.includes(rol));
  });

  constructor(
    readonly authService: AuthService,
    private readonly configService: ConfigService
  ) {}

  ngOnInit(): void {
    // Carga la moneda/zona horaria de la empresa una vez, para toda la sesión (incluye recargas
    // de página con el token todavía válido, ya que este layout es el shell de todas las rutas autenticadas).
    this.configService.cargar();
  }
}
