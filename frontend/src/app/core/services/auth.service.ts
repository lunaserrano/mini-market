import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, shareReplay, tap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CambiarPasswordRequest, LoginRequest, LoginResponse, UsuarioActual } from '../models/auth.models';
import { TokenStorageService } from './token-storage.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly tokenStorage = inject(TokenStorageService);
  private readonly router = inject(Router);

  // Los campos inject()ados arriba deben quedar declarados antes de este, ya que los inicializadores
  // de campo corren en orden de declaración y este lee this.tokenStorage.
  private readonly usuarioSignal = signal<UsuarioActual | null>(this.tokenStorage.obtenerUsuario());
  private refrescoEnCurso$: Observable<LoginResponse> | null = null;

  readonly usuario = computed(() => this.usuarioSignal());
  readonly estaAutenticado = computed(() => !!this.usuarioSignal());
  readonly debeCambiarPassword = computed(() => this.usuarioSignal()?.debeCambiarPassword === true);

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${environment.apiUrl}/auth/login`, request).pipe(tap((r) => this.iniciarSesion(r)));
  }

  /**
   * Renueva la sesión con el refresh token (que rota en cada uso). Si varias peticiones fallan por 401 a la
   * vez, todas comparten una única llamada: rotar dos veces el mismo token lo invalidaría.
   */
  refrescar(): Observable<LoginResponse> {
    const refreshToken = this.tokenStorage.obtenerRefreshToken();
    if (!refreshToken) return throwError(() => new Error('No hay sesión que renovar.'));

    if (!this.refrescoEnCurso$) {
      this.refrescoEnCurso$ = this.http.post<LoginResponse>(`${environment.apiUrl}/auth/refresh`, { refreshToken }).pipe(
        tap((r) => this.iniciarSesion(r)),
        catchError((error) =>
          // Otra pestaña puede haber rotado el token justo antes: si el almacenado ya es otro, se reintenta con ese.
          this.tokenStorage.obtenerRefreshToken() !== refreshToken ? this.refrescarSinCache() : throwError(() => error)
        ),
        finalize(() => (this.refrescoEnCurso$ = null)),
        shareReplay({ bufferSize: 1, refCount: false })
      );
    }
    return this.refrescoEnCurso$;
  }

  private refrescarSinCache(): Observable<LoginResponse> {
    const refreshToken = this.tokenStorage.obtenerRefreshToken();
    if (!refreshToken) return throwError(() => new Error('No hay sesión que renovar.'));
    return this.http.post<LoginResponse>(`${environment.apiUrl}/auth/refresh`, { refreshToken }).pipe(tap((r) => this.iniciarSesion(r)));
  }

  /** Cierra la sesión: avisa al backend (para revocar el refresh token; si falla no importa) y limpia lo local. */
  logout(): void {
    if (this.tokenStorage.obtenerToken()) {
      this.http.post(`${environment.apiUrl}/auth/logout`, { refreshToken: this.tokenStorage.obtenerRefreshToken() }).subscribe({ error: () => undefined });
    }
    this.cerrarSesionLocal();
  }

  cerrarSesionLocal(redirigir = true): void {
    this.tokenStorage.limpiar();
    this.usuarioSignal.set(null);
    if (redirigir) this.router.navigate(['/auth/login']);
  }

  /** Cambia la contraseña propia. El backend revoca las demás sesiones y devuelve una nueva. */
  cambiarPassword(request: CambiarPasswordRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${environment.apiUrl}/auth/cambiar-password`, request).pipe(tap((r) => this.iniciarSesion(r)));
  }

  /** True si el usuario tiene AL MENOS UNO de los permisos indicados. */
  tienePermiso(...permisos: string[]): boolean {
    const propios = this.usuarioSignal()?.permisos;
    return !!propios && permisos.some((p) => propios.includes(p));
  }

  /** True si el usuario tiene TODOS los permisos indicados. */
  tieneTodos(...permisos: string[]): boolean {
    const propios = this.usuarioSignal()?.permisos;
    return !!propios && permisos.every((p) => propios.includes(p));
  }

  private iniciarSesion(respuesta: LoginResponse): void {
    this.tokenStorage.guardarSesion(respuesta);
    this.usuarioSignal.set(respuesta.usuario);
  }
}
