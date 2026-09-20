export interface DetalleVentaCreate {
  productoId: number;
  tipoPrecioId: number;
  cantidad: number;
  descuento: number;
}

export interface PagoVentaCreate {
  metodo: 'EFECTIVO' | 'TARJETA' | 'TRANSFERENCIA';
  monto: number;
  referencia?: string | null;
}

export interface VentaCreate {
  clienteId?: number | null;
  detalles: DetalleVentaCreate[];
  /** Con `alCredito`, puede venir vacío o cubrir solo una parte: lo que falte queda como deuda del cliente. */
  pagos: PagoVentaCreate[];
  alCredito?: boolean;
  /** Día límite de pago (yyyy-MM-dd). Solo aplica con `alCredito`. */
  fechaVencimiento?: string | null;
}

export interface DetalleVenta {
  productoId: number;
  productoNombre: string;
  tipoPrecioId: number;
  tipoPrecioNombre: string;
  cantidad: number;
  cantidadBaseCalculada: number;
  precioUnitario: number;
  descuento: number;
  subtotal: number;
}

export interface Venta {
  id: number;
  folio: number;
  fecha: string;
  clienteId?: number | null;
  estado: 'COMPLETADA' | 'ANULADA';
  subtotal: number;
  descuentoTotal: number;
  impuestoTotal: number;
  total: number;
  detalles: DetalleVenta[];
  pagos: PagoVentaCreate[];
}

export interface VentaResumen {
  id: number;
  folio: number;
  fecha: string;
  clienteNombre?: string | null;
  total: number;
  estado: 'COMPLETADA' | 'ANULADA';
  /** Deuda vigente si la venta fue a crédito y aún está pendiente; null en cualquier otro caso. */
  saldoCredito?: number | null;
}

/** Línea de carrito del POS, en memoria hasta que se confirma la venta. */
export interface LineaCarrito {
  productoId: number;
  productoNombre: string;
  tipoPrecioId: number;
  tipoPrecioNombre: string;
  cantidad: number;
  precioUnitario: number;
  descuento: number;
  subtotal: number;
}
