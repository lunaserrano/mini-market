import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { EventoSeguridad } from '../../core/models/seguridad.models';
import { Usuario } from '../../core/models/usuario.models';
import { PERMISOS } from '../../core/security/permisos';
import { AuditoriaService } from '../../core/services/auditoria.service';
import { AuthService } from '../../core/services/auth.service';
import { UsuarioService } from '../../core/services/usuario.service';

type Severidad = 'success' | 'info' | 'warn' | 'danger' | 'secondary';

const ETIQUETAS: Record<string, { texto: string; severidad: Severidad }> = {
  LOGIN_OK: { texto: 'Inicio de sesión', severidad: 'success' },
  LOGIN_FALLIDO: { texto: 'Inicio fallido', severidad: 'warn' },
  CUENTA_BLOQUEADA: { texto: 'Cuenta bloqueada', severidad: 'danger' },
  DESBLOQUEO: { texto: 'Desbloqueo', severidad: 'info' },
  LOGOUT: { texto: 'Cierre de sesión', severidad: 'secondary' },
  PASSWORD_CAMBIADA: { texto: 'Contraseña cambiada', severidad: 'info' },
  PASSWORD_RESET: { texto: 'Contraseña restablecida', severidad: 'warn' },
  SESIONES_REVOCADAS: { texto: 'Sesiones revocadas', severidad: 'warn' },
  USUARIO_CREADO: { texto: 'Usuario creado', severidad: 'success' },
  USUARIO_EDITADO: { texto: 'Usuario editado', severidad: 'info' },
  USUARIO_ESTADO: { texto: 'Estado de usuario', severidad: 'info' },
  ROL_CREADO: { texto: 'Rol creado', severidad: 'success' },
  ROL_EDITADO: { texto: 'Rol editado', severidad: 'info' },
  ROL_ELIMINADO: { texto: 'Rol eliminado', severidad: 'danger' },
  REFRESH_REUSO: { texto: 'Reuso de sesión', severidad: 'danger' },
  ACCION_API: { texto: 'Petición', severidad: 'info' },
  CLIC_UI: { texto: 'Clic', severidad: 'secondary' },
  NAVEGACION_UI: { texto: 'Navegación', severidad: 'secondary' }
};

const ORIGENES = [
  { valor: 'SEG', etiqueta: 'Seguridad' },
  { valor: 'API', etiqueta: 'Peticiones al servidor' },
  { valor: 'UI', etiqueta: 'Clics y navegación' }
];

@Component({
  selector: 'app-auditoria',
  standalone: true,
  imports: [DatePipe, FormsModule, ButtonModule, DatePickerModule, SelectModule, TableModule, TagModule],
  templateUrl: './auditoria.component.html'
})
export class AuditoriaComponent implements OnInit {
  private readonly auditoriaService = inject(AuditoriaService);
  private readonly usuarioService = inject(UsuarioService);
  private readonly authService = inject(AuthService);

  readonly eventos = signal<EventoSeguridad[]>([]);
  readonly total = signal(0);
  readonly cargando = signal(false);
  readonly tipos = signal<{ valor: string; etiqueta: string }[]>([]);
  readonly usuarios = signal<Usuario[]>([]);

  readonly origenes = ORIGENES;
  readonly opcionesTamanoPagina = [25, 50, 100];
  tamanoPagina = 25;
  private pagina = 1;
  /** Filas con el detalle desplegado, por id de evento (lo usa p-table con dataKey="id"). */
  expandidos: Record<number, boolean> = {};

  desde: Date | null = null;
  hasta: Date | null = null;
  tipo: string | null = null;
  origen: string | null = null;
  usuarioId: number | null = null;

  ngOnInit(): void {
    this.auditoriaService
      .tipos()
      .subscribe((tipos) => this.tipos.set(tipos.map((valor) => ({ valor, etiqueta: this.etiqueta(valor).texto }))));
    // El filtro por usuario solo se ofrece a quien puede listar usuarios.
    if (this.authService.tienePermiso(PERMISOS.UsuariosVer)) {
      this.usuarioService.listar().subscribe((usuarios) => this.usuarios.set(usuarios));
    }
  }

  etiqueta(tipo: string): { texto: string; severidad: Severidad } {
    return ETIQUETAS[tipo] ?? { texto: tipo, severidad: 'secondary' };
  }

  /** Color del código de estado HTTP de una petición. */
  severidadEstado(status: number | null): Severidad {
    if (status === null) return 'secondary';
    if (status >= 500) return 'danger';
    if (status >= 400) return 'warn';
    return 'success';
  }

  /** `datos` llega como texto JSON: se muestra indentado (o tal cual si vino truncado y ya no es JSON válido). */
  datosFormateados(evento: EventoSeguridad): string {
    if (!evento.datos) return '';
    try {
      return JSON.stringify(JSON.parse(evento.datos), null, 2);
    } catch {
      return evento.datos;
    }
  }

  /** Paginación lazy: la tabla avisa qué página quiere y se pide solo esa al backend. */
  alCargarPagina(evento: TableLazyLoadEvent): void {
    this.tamanoPagina = evento.rows ?? this.tamanoPagina;
    this.pagina = Math.floor((evento.first ?? 0) / this.tamanoPagina) + 1;
    this.consultar();
  }

  buscar(): void {
    this.pagina = 1;
    this.consultar();
  }

  limpiar(): void {
    this.desde = this.hasta = this.tipo = this.origen = this.usuarioId = null;
    this.buscar();
  }

  private consultar(): void {
    this.cargando.set(true);
    this.auditoriaService
      .listar({
        desde: this.desde ? this.inicioDelDia(this.desde).toISOString() : null,
        hasta: this.hasta ? this.finDelDia(this.hasta).toISOString() : null,
        usuarioId: this.usuarioId,
        tipo: this.tipo,
        origen: this.origen,
        pagina: this.pagina,
        tamanoPagina: this.tamanoPagina
      })
      .subscribe({
        next: (resultado) => {
          this.expandidos = {};
          this.eventos.set(resultado.items);
          this.total.set(resultado.total);
          this.cargando.set(false);
        },
        error: () => this.cargando.set(false)
      });
  }

  private inicioDelDia(fecha: Date): Date {
    return new Date(fecha.getFullYear(), fecha.getMonth(), fecha.getDate(), 0, 0, 0, 0);
  }

  /** "Hasta" es inclusivo: llega hasta el último instante de ese día (hora local del navegador). */
  private finDelDia(fecha: Date): Date {
    return new Date(fecha.getFullYear(), fecha.getMonth(), fecha.getDate(), 23, 59, 59, 999);
  }
}
