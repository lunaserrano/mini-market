export interface Empresa {
  id: number;
  nombre: string;
  razonSocial?: string | null;
  identificacionFiscal?: string | null;
  zonaHoraria: string;
  /** Código ISO 4217 (USD, GTQ, HNL, NIO, CRC, MXN...). Configurable por país desde Configuración. */
  codigoMoneda: string;
  /** Símbolo mostrado en toda la app (ej. "$", "Q", "L"). */
  simboloMoneda: string;
  /** Tasa de IVA (%) aplicada a todas las ventas — mantenimiento único, ya no se ingresa por producto. */
  tasaImpuesto: number;
}

export type EmpresaUpdate = Omit<Empresa, 'id'>;

/** Catálogo de monedas comunes en Centroamérica + México, con su símbolo sugerido. El usuario
 * puede escribir cualquier otro código/símbolo manualmente si su país no está en la lista. */
export const MONEDAS_SUGERIDAS: { codigo: string; nombre: string; simbolo: string }[] = [
  { codigo: 'USD', nombre: 'Dólar estadounidense', simbolo: '$' },
  { codigo: 'GTQ', nombre: 'Quetzal guatemalteco', simbolo: 'Q' },
  { codigo: 'HNL', nombre: 'Lempira hondureño', simbolo: 'L' },
  { codigo: 'NIO', nombre: 'Córdoba nicaragüense', simbolo: 'C$' },
  { codigo: 'CRC', nombre: 'Colón costarricense', simbolo: '₡' },
  { codigo: 'PAB', nombre: 'Balboa panameño', simbolo: 'B/.' },
  { codigo: 'MXN', nombre: 'Peso mexicano', simbolo: '$' }
];
