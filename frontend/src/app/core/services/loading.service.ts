import { Injectable, computed, signal } from '@angular/core';

/** Cuenta las peticiones HTTP en curso (ver loadingInterceptor) para mostrar el espiner global de la app. */
@Injectable({ providedIn: 'root' })
export class LoadingService {
  private readonly contador = signal(0);

  readonly cargando = computed(() => this.contador() > 0);

  iniciar(): void {
    this.contador.set(this.contador() + 1);
  }

  finalizar(): void {
    this.contador.set(Math.max(0, this.contador() - 1));
  }
}
