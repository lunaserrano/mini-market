export interface Inventario {
  productoId: number;
  productoNombre: string;
  sucursalId: number;
  stockActual: number;
  stockMinimo: number;
}

export interface AjusteInventarioRequest {
  productoId: number;
  sucursalId: number;
  cantidadAjuste: number;
  observacion: string;
}

export interface MovimientoInventario {
  id: number;
  productoId: number;
  productoNombre: string;
  sucursalId: number;
  tipoMovimiento: string;
  cantidad: number;
  stockResultante: number;
  documentoOrigenTipo?: string | null;
  documentoOrigenId?: number | null;
  observacion?: string | null;
  fechaMovimiento: string;
}
