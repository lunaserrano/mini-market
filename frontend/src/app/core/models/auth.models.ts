export interface LoginRequest {
  username: string;
  password: string;
}

export interface UsuarioActual {
  id: number;
  empresaId: number;
  sucursalId: number | null;
  nombreCompleto: string;
  username: string;
  /** Código del rol (informativo: los accesos se deciden por `permisos`). */
  rol: string;
  rolNombre: string;
  permisos: string[];
  debeCambiarPassword: boolean;
}

export interface LoginResponse {
  token: string;
  expiraUtc: string;
  refreshToken: string;
  refreshExpiraUtc: string;
  usuario: UsuarioActual;
}

export interface CambiarPasswordRequest {
  passwordActual: string;
  passwordNueva: string;
}
