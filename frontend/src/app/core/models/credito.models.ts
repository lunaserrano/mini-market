export type EstadoCredito = 'PENDIENTE' | 'PAGADO' | 'ANULADO';
export type MetodoAbono = 'EFECTIVO' | 'TARJETA' | 'TRANSFERENCIA';

/** Fila del listado. `vencido` = PENDIENTE con fecha de vencimiento ya pasada (lo calcula el backend). */
export interface CreditoResumen {
  id: number;
  ventaId: number;
  ventaFolio: number;
  clienteId: number;
  clienteNombre: string;
  fechaCreacion: string;
  fechaVencimiento?: string | null;
  montoOriginal: number;
  saldoPendiente: number;
  estado: EstadoCredito;
  vencido: boolean;
}

export interface AbonoCredito {
  id: number;
  fecha: string;
  metodo: MetodoAbono;
  monto: number;
  referencia?: string | null;
  usuarioNombre: string;
}

/** `totalVenta - montoOriginal` es lo que el cliente pagó en el momento de la venta. */
export interface Credito extends CreditoResumen {
  fechaCancelacion?: string | null;
  totalVenta: number;
  abonos: AbonoCredito[];
}

export interface AbonoCreate {
  metodo: MetodoAbono;
  monto: number;
  referencia?: string | null;
}
