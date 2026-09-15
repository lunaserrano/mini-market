import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LoginRequest, LoginResponse, UsuarioActual } from '../models/auth.models';
import { TokenStorageService } from './token-storage.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly tokenStorage = inject(TokenStorageService);
  private readonly router = inject(Router);

  // Los campos inject()ados arriba deben quedar declarados antes de este, ya que los inicializadores
  // de campo corren en orden de declaración y este lee this.tokenStorage.
  private readonly usuarioSignal = signal<UsuarioActual | null>(this.tokenStorage.obtenerUsuario());

  readonly usuario = computed(() => this.usuarioSignal());
  readonly estaAutenticado = computed(() => !!this.usuarioSignal());

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${environment.apiUrl}/auth/login`, request).pipe(
      tap((respuesta) => {
        this.tokenStorage.guardar(respuesta.token, respuesta.usuario);
        this.usuarioSignal.set(respuesta.usuario);
      })
    );
  }

  logout(): void {
    this.tokenStorage.limpiar();
    this.usuarioSignal.set(null);
    this.router.navigate(['/auth/login']);
  }

  tienePermiso(...roles: string[]): boolean {
    const rol = this.usuarioSignal()?.rol;
    return !!rol && roles.includes(rol);
  }
}
