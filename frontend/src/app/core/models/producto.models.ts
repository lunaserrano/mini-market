export interface TipoPrecio {
  id: number;
  nombre: string;
  cantidadBase: number;
  precioVenta: number;
  precioCompra?: number | null;
  esDefault: boolean;
  estado: 'A' | 'I';
}
export type TipoPrecioCreate = Omit<TipoPrecio, 'id' | 'estado'>;

export interface Producto {
  id: number;
  categoriaId: number;
  categoriaNombre?: string | null;
  proveedorId?: number | null;
  nombre: string;
  descripcion?: string | null;
  codigoBarras?: string | null;
  codigoInterno?: string | null;
  imagenPath?: string | null;
  unidadBase: string;
  estado: 'A' | 'I';
  tiposPrecio: TipoPrecio[];
}

export type ProductoCreate = Omit<Producto, 'id' | 'estado' | 'categoriaNombre' | 'tiposPrecio'> & {
  tiposPrecio: TipoPrecioCreate[];
};

export type ProductoUpdate = Omit<ProductoCreate, 'tiposPrecio'>;

export interface ProductoPos {
  productoId: number;
  nombre: string;
  codigoBarras?: string | null;
  unidadBase: string;
  stockActual: number;
  tiposPrecio: TipoPrecio[];
}
