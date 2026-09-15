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
  rol: 'admin' | 'supervisor' | 'cajero';
}

export interface LoginResponse {
  token: string;
  expiraUtc: string;
  usuario: UsuarioActual;
}
