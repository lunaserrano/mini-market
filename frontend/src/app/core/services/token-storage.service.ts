import { Injectable } from '@angular/core';
import { UsuarioActual } from '../models/auth.models';

const TOKEN_KEY = 'mm_token';
const USUARIO_KEY = 'mm_usuario';

/**
 * Guarda únicamente el JWT real y el usuario decodificado a partir de la respuesta de login
 * (no un objeto de sesión inventado como en el mock anterior). El token es la única fuente de
 * verdad: si expira o el backend lo rechaza, el interceptor de errores fuerza logout.
 */
@Injectable({ providedIn: 'root' })
export class TokenStorageService {
  guardar(token: string, usuario: UsuarioActual): void {
    localStorage.setItem(TOKEN_KEY, token);
    localStorage.setItem(USUARIO_KEY, JSON.stringify(usuario));
  }

  obtenerToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  obtenerUsuario(): UsuarioActual | null {
    const raw = localStorage.getItem(USUARIO_KEY);
    return raw ? (JSON.parse(raw) as UsuarioActual) : null;
  }

  limpiar(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USUARIO_KEY);
  }
}
