import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { MessageModule } from 'primeng/message';
import { rutaInicial } from '../../core/guards/permission.guard';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, ButtonModule, InputTextModule, PasswordModule, MessageModule],
  template: `
    <div class="flex items-center justify-center min-h-screen bg-surface-50 dark:bg-surface-950 px-4">
      <div class="w-full max-w-sm bg-surface-0 dark:bg-surface-900 rounded-2xl shadow-lg p-8">
        <div class="text-center mb-6">
          <i class="pi pi-shop text-primary text-4xl"></i>
          <h1 class="text-xl font-bold mt-2">Mini Market</h1>
          <p class="text-surface-500 text-sm">Ingresa a tu cuenta</p>
        </div>

        <form (ngSubmit)="ingresar()" class="flex flex-col gap-4">
          <div class="flex flex-col gap-1">
            <label for="username" class="text-sm font-medium">Usuario</label>
            <input id="username" pInputText [(ngModel)]="username" name="username" autocomplete="username" required />
          </div>
          <div class="flex flex-col gap-1">
            <label for="password" class="text-sm font-medium">Contraseña</label>
            <p-password
              id="password"
              [(ngModel)]="password"
              name="password"
              [feedback]="false"
              [toggleMask]="true"
              autocomplete="current-password"
              styleClass="w-full"
              inputStyleClass="w-full"
              required
            />
          </div>

          @if (error()) {
            <p-message severity="error" styleClass="w-full">{{ error() }}</p-message>
          }

          <p-button type="submit" label="Ingresar" [loading]="cargando()" styleClass="w-full mt-2" />
        </form>
      </div>
    </div>
  `
})
export class LoginComponent {
  username = '';
  password = '';
  readonly cargando = signal(false);
  readonly error = signal<string | null>(null);

  constructor(
    private readonly authService: AuthService,
    private readonly router: Router
  ) {}

  ingresar(): void {
    if (!this.username || !this.password) return;

    this.cargando.set(true);
    this.error.set(null);
    this.authService.login({ username: this.username, password: this.password }).subscribe({
      next: (respuesta) => {
        this.cargando.set(false);
        // Con contraseña temporal (alta o restablecimiento por un admin) primero debe cambiarla.
        this.router.navigate([respuesta.usuario.debeCambiarPassword ? '/auth/cambiar-password' : rutaInicial(this.authService)]);
      },
      error: (err) => {
        this.cargando.set(false);
        this.error.set(err.error?.error ?? 'No se pudo iniciar sesión. Intente nuevamente.');
      }
    });
  }
}
