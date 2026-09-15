export interface DetalleCompraCreate {
  productoId: number;
  cantidad: number;
  cantidadBase: number;
  costoUnitario: number;
}

export interface CompraCreate {
  proveedorId: number;
  numeroDocumentoProveedor?: string | null;
  detalles: DetalleCompraCreate[];
}

export interface DetalleCompra {
  productoId: number;
  productoNombre: string;
  cantidad: number;
  cantidadBaseCalculada: number;
  costoUnitario: number;
  subtotal: number;
}

export interface Compra {
  id: number;
  fecha: string;
  proveedorId: number;
  proveedorNombre: string;
  numeroDocumentoProveedor?: string | null;
  subtotal: number;
  impuestoTotal: number;
  total: number;
  estado: 'COMPLETADA' | 'ANULADA';
  detalles: DetalleCompra[];
}

export interface CompraResumen {
  id: number;
  fecha: string;
  proveedorNombre: string;
  total: number;
  estado: 'COMPLETADA' | 'ANULADA';
}
