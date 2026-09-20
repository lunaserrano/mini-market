import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { MessageService } from 'primeng/api';
import { HasPermissionDirective } from '../../core/directives/has-permission.directive';
import { Rol } from '../../core/models/seguridad.models';
import { Usuario, UsuarioCreate } from '../../core/models/usuario.models';
import { PERMISOS } from '../../core/security/permisos';
import { evaluarPassword, passwordValida } from '../../core/security/password-policy';
import { AuthService } from '../../core/services/auth.service';
import { RolService } from '../../core/services/rol.service';
import { UsuarioService } from '../../core/services/usuario.service';

@Component({
  selector: 'app-usuarios',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    ButtonModule,
    DialogModule,
    HasPermissionDirective,
    InputTextModule,
    PasswordModule,
    SelectModule,
    TableModule,
    TagModule,
    ToggleSwitchModule
  ],
  templateUrl: './usuarios.component.html'
})
export class UsuariosComponent implements OnInit {
  private readonly usuarioService = inject(UsuarioService);
  private readonly rolService = inject(RolService);
  private readonly messageService = inject(MessageService);
  readonly authService = inject(AuthService);
  readonly permisos = PERMISOS;

  readonly usuarios = signal<Usuario[]>([]);
  readonly roles = signal<Rol[]>([]);
  readonly cargando = signal(false);
  readonly puedeCambiarEstado = computed(() => this.authService.tienePermiso(PERMISOS.UsuariosCambiarEstado));

  readonly mostrarFormulario = signal(false);
  editandoId: number | null = null;
  formulario: UsuarioCreate = this.vacio();

  readonly mostrarReset = signal(false);
  usuarioReset: Usuario | null = null;
  nuevaPassword = '';
  resetDebeCambiar = true;

  ngOnInit(): void {
    this.cargar();
    this.rolService.listar().subscribe((roles) => this.roles.set(roles));
  }

  private vacio(): UsuarioCreate {
    return { sucursalId: null, nombreCompleto: '', username: '', password: '', rolId: null, debeCambiarPassword: true };
  }

  private cargar(): void {
    this.cargando.set(true);
    this.usuarioService.listar().subscribe({
      next: (usuarios) => {
        this.usuarios.set(usuarios);
        this.cargando.set(false);
      },
      error: () => this.cargando.set(false)
    });
  }

  nuevo(): void {
    this.editandoId = null;
    this.formulario = this.vacio();
    this.mostrarFormulario.set(true);
  }

  editar(usuario: Usuario): void {
    this.editandoId = usuario.id;
    this.formulario = {
      sucursalId: usuario.sucursalId ?? null,
      nombreCompleto: usuario.nombreCompleto,
      username: usuario.username,
      password: '',
      rolId: usuario.rolId,
      debeCambiarPassword: false
    };
    this.mostrarFormulario.set(true);
  }

  /** Reglas de la contraseña inicial mientras se escribe (el backend las valida de nuevo). */
  reglasFormulario = () => evaluarPassword(this.formulario.password);

  formularioValido(): boolean {
    const f = this.formulario;
    if (!f.nombreCompleto.trim() || !f.username.trim() || !f.rolId) return false;
    return this.editandoId ? true : passwordValida(f.password);
  }

  guardar(): void {
    if (!this.formularioValido()) return;
    const f = this.formulario;

    if (this.editandoId) {
      this.usuarioService
        .actualizar(this.editandoId, { sucursalId: f.sucursalId, nombreCompleto: f.nombreCompleto, rolId: f.rolId! })
        .subscribe(() => this.alGuardar('Usuario actualizado'));
      return;
    }

    this.usuarioService.crear(f).subscribe(() => this.alGuardar('Usuario creado'));
  }

  private alGuardar(mensaje: string): void {
    this.mostrarFormulario.set(false);
    this.cargar();
    this.messageService.add({ severity: 'success', summary: mensaje });
  }

  cambiarEstado(usuario: Usuario): void {
    this.usuarioService.cambiarEstado(usuario.id, usuario.estado !== 'A').subscribe({
      next: () => this.cargar(),
      error: () => this.cargar() // revierte el interruptor si el backend lo rechazó (p. ej. último administrador)
    });
  }

  desbloquear(usuario: Usuario): void {
    this.usuarioService.desbloquear(usuario.id).subscribe(() => {
      this.cargar();
      this.messageService.add({ severity: 'success', summary: `${usuario.username} desbloqueado` });
    });
  }

  revocarSesiones(usuario: Usuario): void {
    this.usuarioService.revocarSesiones(usuario.id).subscribe(() =>
      this.messageService.add({ severity: 'success', summary: `Sesiones de ${usuario.username} cerradas`, detail: 'Deberá iniciar sesión de nuevo.' })
    );
  }

  abrirReset(usuario: Usuario): void {
    this.usuarioReset = usuario;
    this.nuevaPassword = '';
    this.resetDebeCambiar = true;
    this.mostrarReset.set(true);
  }

  reglasReset = () => evaluarPassword(this.nuevaPassword);
  resetValido = () => passwordValida(this.nuevaPassword);

  confirmarReset(): void {
    if (!this.usuarioReset || !passwordValida(this.nuevaPassword)) return;
    this.usuarioService.resetPassword(this.usuarioReset.id, this.nuevaPassword, this.resetDebeCambiar).subscribe(() => {
      this.mostrarReset.set(false);
      this.cargar();
      this.messageService.add({ severity: 'success', summary: 'Contraseña restablecida' });
    });
  }
}
