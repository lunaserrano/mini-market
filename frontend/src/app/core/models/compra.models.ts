/** costoUnidadMedida es el precio pagado por TODA la unidad de medida elegida (ej. $30 la caja
 * completa), no por unidad base — el backend resuelve el factor de conversión a partir de
 * tipoPrecioId (la misma presentación que usan las Ventas), nunca se manda un factor libre. */
export interface DetalleCompraCreate {
  productoId: number;
  tipoPrecioId: number;
  cantidad: number;
  costoUnidadMedida: number;
}

export interface CompraCreate {
  proveedorId: number;
  numeroDocumentoProveedor?: string | null;
  detalles: DetalleCompraCreate[];
}

export interface DetalleCompra {
  productoId: number;
  productoNombre: string;
  /** Nullable: compras registradas antes de este cambio no tienen presentación asociada. */
  tipoPrecioId?: number | null;
  tipoPrecioNombre?: string | null;
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
