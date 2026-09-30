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

export interface StockMinimoRequest {
  productoId: number;
  sucursalId: number;
  stockMinimo: number;
}

/** Stock en o por debajo del mínimo configurado (un mínimo de 0 significa "sin alerta"). */
export function esStockBajo(item: Pick<Inventario, 'stockActual' | 'stockMinimo'>): boolean {
  return item.stockMinimo > 0 && item.stockActual <= item.stockMinimo;
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
