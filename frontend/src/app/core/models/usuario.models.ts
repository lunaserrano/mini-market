export interface Usuario {
  id: number;
  sucursalId?: number | null;
  nombreCompleto: string;
  username: string;
  rol: 'admin' | 'supervisor' | 'cajero';
  estado: 'A' | 'I';
}

export interface UsuarioCreate {
  sucursalId?: number | null;
  nombreCompleto: string;
  username: string;
  password: string;
  rol: 'admin' | 'supervisor' | 'cajero';
}

export interface UsuarioUpdate {
  sucursalId?: number | null;
  nombreCompleto: string;
  rol: 'admin' | 'supervisor' | 'cajero';
}
