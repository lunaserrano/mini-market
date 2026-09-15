export interface Categoria {
  id: number;
  nombre: string;
  descripcion?: string | null;
  estado: 'A' | 'I';
}
export type CategoriaCreate = Omit<Categoria, 'id' | 'estado'>;

export interface Proveedor {
  id: number;
  nombre: string;
  contacto?: string | null;
  telefono?: string | null;
  email?: string | null;
  direccion?: string | null;
  identificacionFiscal?: string | null;
  estado: 'A' | 'I';
}
export type ProveedorCreate = Omit<Proveedor, 'id' | 'estado'>;

export interface Cliente {
  id: number;
  nombre: string;
  identificacionFiscal?: string | null;
  telefono?: string | null;
  email?: string | null;
  direccion?: string | null;
  estado: 'A' | 'I';
}
export type ClienteCreate = Omit<Cliente, 'id' | 'estado'>;
