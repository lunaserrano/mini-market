export interface Usuario {
  id: number;
  sucursalId?: number | null;
  nombreCompleto: string;
  username: string;
  rolId: number;
  rol: string;
  rolNombre: string;
  estado: 'A' | 'I';
  bloqueado: boolean;
  bloqueadoHasta: string | null;
  debeCambiarPassword: boolean;
  ultimoLoginUtc: string | null;
}

export interface UsuarioCreate {
  sucursalId?: number | null;
  nombreCompleto: string;
  username: string;
  password: string;
  rolId: number | null;
  debeCambiarPassword: boolean;
}

export interface UsuarioUpdate {
  sucursalId?: number | null;
  nombreCompleto: string;
  rolId: number;
}
