import { Injectable } from '@angular/core';
import { LoginResponse, UsuarioActual } from '../models/auth.models';

const TOKEN_KEY = 'mm_token';
const REFRESH_KEY = 'mm_refresh';
const EXPIRA_KEY = 'mm_expira';
const USUARIO_KEY = 'mm_usuario';

/** Margen para renovar el access token un poco antes de que venza y no fallar a mitad de una petición. */
const MARGEN_RENOVACION_MS = 30_000;

/**
 * Guarda la sesión emitida por el backend: access token (JWT de vida corta), refresh token rotativo
 * y el usuario con sus permisos. Está en localStorage, igual que antes: es un compromiso frente a XSS
 * (cualquier script de la página puede leerlo), por eso el access token dura pocos minutos y el
 * refresh token se rota en cada uso y se revoca al detectar reutilización.
 */
@Injectable({ providedIn: 'root' })
export class TokenStorageService {
  guardarSesion(respuesta: LoginResponse): void {
    localStorage.setItem(TOKEN_KEY, respuesta.token);
    localStorage.setItem(REFRESH_KEY, respuesta.refreshToken);
    localStorage.setItem(EXPIRA_KEY, respuesta.expiraUtc);
    localStorage.setItem(USUARIO_KEY, JSON.stringify(respuesta.usuario));
  }

  obtenerToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  obtenerRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_KEY);
  }

  obtenerUsuario(): UsuarioActual | null {
    const raw = localStorage.getItem(USUARIO_KEY);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as UsuarioActual;
    } catch {
      return null;
    }
  }

  /** True si el access token ya venció o vence en instantes (o si no se sabe cuándo vence). */
  tokenPorVencer(): boolean {
    const expira = Date.parse(localStorage.getItem(EXPIRA_KEY) ?? '');
    return Number.isNaN(expira) || expira - Date.now() < MARGEN_RENOVACION_MS;
  }

  limpiar(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_KEY);
    localStorage.removeItem(EXPIRA_KEY);
    localStorage.removeItem(USUARIO_KEY);
  }
}
