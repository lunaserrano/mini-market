export interface Rol {
  id: number;
  codigo: string;
  nombre: string;
  descripcion: string | null;
  esSistema: boolean;
  totalUsuarios: number;
  totalPermisos: number;
}

export interface RolDetalle {
  id: number;
  codigo: string;
  nombre: string;
  descripcion: string | null;
  esSistema: boolean;
  totalUsuarios: number;
  permisos: string[];
}

export interface RolInput {
  nombre: string;
  descripcion: string | null;
  permisos: string[];
}

export interface Permiso {
  codigo: string;
  modulo: string;
  nombre: string;
}

export interface PermisoModulo {
  modulo: string;
  permisos: Permiso[];
}

export interface EventoSeguridad {
  id: number;
  fechaUtc: string;
  tipo: string;
  detalle: string | null;
  actorUsuarioId: number | null;
  actorNombre: string | null;
  usuarioObjetivoId: number | null;
  objetivoNombre: string | null;
  ip: string | null;
  /** SEG (seguridad), API (petición al backend) o UI (clic/navegación). */
  origen: 'SEG' | 'API' | 'UI';
  metodo: string | null;
  ruta: string | null;
  statusCode: number | null;
  duracionMs: number | null;
  /** JSON: cuerpo de la petición (sin secretos) o descriptor del elemento clicado. */
  datos: string | null;
}

/** Acción del usuario en el navegador, tal como la recibe POST /auditoria/cliente. */
export interface EventoCliente {
  tipo: 'CLIC_UI' | 'NAVEGACION_UI';
  fechaUtc: string;
  ruta: string | null;
  detalle: string | null;
  datos: Record<string, string | null> | null;
}

export interface PaginaResultado<T> {
  items: T[];
  total: number;
  pagina: number;
  tamanoPagina: number;
}

export interface AuditoriaFiltro {
  desde?: string | null;
  hasta?: string | null;
  usuarioId?: number | null;
  tipo?: string | null;
  origen?: string | null;
  pagina: number;
  tamanoPagina: number;
}
