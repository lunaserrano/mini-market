import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { PasswordModule } from 'primeng/password';
import { rutaInicial } from '../../core/guards/permission.guard';
import { evaluarPassword, passwordValida } from '../../core/security/password-policy';
import { AuthService } from '../../core/services/auth.service';

/** Cambio de contraseña propio. Cuando es obligatorio (contraseña temporal) no ofrece volver atrás. */
@Component({
  selector: 'app-cambiar-password',
  standalone: true,
  imports: [FormsModule, RouterLink, ButtonModule, MessageModule, PasswordModule],
  template: `
    <div class="flex items-center justify-center min-h-screen bg-surface-50 dark:bg-surface-950 px-4">
      <div class="w-full max-w-md bg-surface-0 dark:bg-surface-900 rounded-2xl shadow-lg p-8">
        <div class="text-center mb-6">
          <i class="pi pi-key text-primary text-4xl"></i>
          <h1 class="text-xl font-bold mt-2">Cambiar contraseña</h1>
          @if (forzado()) {
            <p class="text-surface-500 text-sm mt-1">Tu contraseña es temporal: debes definir una nueva para continuar.</p>
          }
        </div>

        <form (ngSubmit)="guardar()" class="flex flex-col gap-4">
          <div class="flex flex-col gap-1">
            <label for="actual" class="text-sm font-medium">Contraseña actual</label>
            <p-password id="actual" [(ngModel)]="passwordActual" name="actual" [feedback]="false" [toggleMask]="true"
              autocomplete="current-password" styleClass="w-full" inputStyleClass="w-full" />
          </div>
          <div class="flex flex-col gap-1">
            <label for="nueva" class="text-sm font-medium">Nueva contraseña</label>
            <p-password id="nueva" [(ngModel)]="passwordNueva" name="nueva" [feedback]="false" [toggleMask]="true"
              autocomplete="new-password" styleClass="w-full" inputStyleClass="w-full" />
            <ul class="list-none p-0 m-0 mt-1 text-xs flex flex-col gap-0.5">
              @for (regla of reglas(); track regla.texto) {
                <li [class]="regla.cumple ? 'text-green-600' : 'text-surface-500'">
                  <i [class]="regla.cumple ? 'pi pi-check-circle' : 'pi pi-circle'" class="mr-1 text-xs"></i>{{ regla.texto }}
                </li>
              }
            </ul>
          </div>
          <div class="flex flex-col gap-1">
            <label for="confirmar" class="text-sm font-medium">Confirmar nueva contraseña</label>
            <p-password id="confirmar" [(ngModel)]="confirmacion" name="confirmar" [feedback]="false" [toggleMask]="true"
              autocomplete="new-password" styleClass="w-full" inputStyleClass="w-full" />
            @if (confirmacion && !coincide()) {
              <small class="text-red-500">Las contraseñas no coinciden.</small>
            }
          </div>

          @if (error()) {
            <p-message severity="error" styleClass="w-full">{{ error() }}</p-message>
          }

          <p-button type="submit" label="Cambiar contraseña" icon="pi pi-check" [loading]="cargando()" [disabled]="!puedeGuardar()" styleClass="w-full" />
        </form>

        <div class="text-center mt-4 text-sm">
          @if (forzado()) {
            <a href="#" class="text-surface-500" (click)="cerrarSesion($event)">Cerrar sesión</a>
          } @else {
            <a routerLink="/" class="text-surface-500">Cancelar</a>
          }
        </div>
      </div>
    </div>
  `
})
export class CambiarPasswordComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  passwordActual = '';
  passwordNueva = '';
  confirmacion = '';

  readonly cargando = signal(false);
  readonly error = signal<string | null>(null);
  readonly forzado = computed(() => this.authService.debeCambiarPassword());

  // Los campos de ngModel no son signals: las reglas se recalculan en cada detección de cambios.
  reglas = () => evaluarPassword(this.passwordNueva);
  coincide = () => this.passwordNueva === this.confirmacion;
  puedeGuardar = () => !!this.passwordActual && passwordValida(this.passwordNueva) && this.coincide();

  guardar(): void {
    if (!this.puedeGuardar()) return;

    this.cargando.set(true);
    this.error.set(null);
    this.authService.cambiarPassword({ passwordActual: this.passwordActual, passwordNueva: this.passwordNueva }).subscribe({
      next: () => {
        this.cargando.set(false);
        this.router.navigate([rutaInicial(this.authService)]);
      },
      error: (err) => {
        this.cargando.set(false);
        this.error.set(err.error?.error ?? 'No se pudo cambiar la contraseña.');
      }
    });
  }

  cerrarSesion(evento: Event): void {
    evento.preventDefault();
    this.authService.logout();
  }
}
