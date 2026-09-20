import { LoginResponse, UsuarioActual } from '../models/auth.models';

/** Datos de sesión falsos para los specs de seguridad. */
export function usuarioFake(permisos: string[] = [], extra: Partial<UsuarioActual> = {}): UsuarioActual {
  return {
    id: 1,
    empresaId: 1,
    sucursalId: null,
    nombreCompleto: 'Ana Pérez',
    username: 'ana',
    rol: 'rol_x',
    rolNombre: 'Rol X',
    permisos,
    debeCambiarPassword: false,
    ...extra
  };
}

export function sesionFake(permisos: string[] = [], token = 'jwt-nuevo', refresh = 'refresh-nuevo', extra: Partial<UsuarioActual> = {}): LoginResponse {
  return {
    token,
    expiraUtc: new Date(Date.now() + 15 * 60_000).toISOString(),
    refreshToken: refresh,
    refreshExpiraUtc: new Date(Date.now() + 7 * 24 * 3_600_000).toISOString(),
    usuario: usuarioFake(permisos, extra)
  };
}

/** Deja en localStorage una sesión vigente, tal como la guardaría TokenStorageService. */
export function guardarSesionFake(permisos: string[] = [], token = 'jwt-viejo', refresh = 'refresh-viejo', minutosParaVencer = 10, extra: Partial<UsuarioActual> = {}): void {
  localStorage.setItem('mm_token', token);
  localStorage.setItem('mm_refresh', refresh);
  localStorage.setItem('mm_expira', new Date(Date.now() + minutosParaVencer * 60_000).toISOString());
  localStorage.setItem('mm_usuario', JSON.stringify(usuarioFake(permisos, extra)));
}
