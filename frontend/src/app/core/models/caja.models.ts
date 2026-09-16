export interface Caja {
  id: number;
  sucursalId: number;
  usuarioAperturaId: number;
  usuarioAperturaNombre: string;
  fechaApertura: string;
  montoInicial: number;
  usuarioCierreId?: number | null;
  usuarioCierreNombre?: string | null;
  fechaCierre?: string | null;
  montoFinalDeclarado?: number | null;
  montoFinalSistema?: number | null;
  diferencia?: number | null;
  estado: 'ABIERTA' | 'CERRADA';
}

export interface MovimientoCaja {
  id: number;
  cajaId: number;
  tipo: 'INGRESO' | 'EGRESO';
  concepto: string;
  monto: number;
  fecha: string;
}

export type MovimientoCajaCreate = Omit<MovimientoCaja, 'id' | 'cajaId' | 'fecha'>;
