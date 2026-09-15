import { Component, OnInit, signal } from '@angular/core';
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
import { UsuarioService } from '../../core/services/usuario.service';
import { Usuario, UsuarioCreate } from '../../core/models/usuario.models';

const ROLES = [
  { label: 'Administrador', value: 'admin' },
  { label: 'Supervisor', value: 'supervisor' },
  { label: 'Cajero', value: 'cajero' }
];

@Component({
  selector: 'app-usuarios',
  standalone: true,
  imports: [FormsModule, ButtonModule, DialogModule, InputTextModule, PasswordModule, SelectModule, TableModule, TagModule, ToggleSwitchModule],
  templateUrl: './usuarios.component.html'
})
export class UsuariosComponent implements OnInit {
  readonly usuarios = signal<Usuario[]>([]);
  readonly cargando = signal(false);
  readonly roles = ROLES;

  readonly mostrarFormulario = signal(false);
  editandoId: number | null = null;
  formulario: UsuarioCreate = this.vacio();

  readonly mostrarReset = signal(false);
  usuarioResetId: number | null = null;
  nuevaPassword = '';

  constructor(
    private readonly usuarioService: UsuarioService,
    private readonly messageService: MessageService
  ) {}

  ngOnInit(): void {
    this.cargar();
  }

  private vacio(): UsuarioCreate {
    return { sucursalId: null, nombreCompleto: '', username: '', password: '', rol: 'cajero' };
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
    this.formulario = { sucursalId: usuario.sucursalId ?? null, nombreCompleto: usuario.nombreCompleto, username: usuario.username, password: '', rol: usuario.rol };
    this.mostrarFormulario.set(true);
  }

  guardar(): void {
    if (!this.formulario.nombreCompleto || !this.formulario.username) return;

    if (this.editandoId) {
      this.usuarioService
        .actualizar(this.editandoId, { sucursalId: this.formulario.sucursalId, nombreCompleto: this.formulario.nombreCompleto, rol: this.formulario.rol })
        .subscribe(() => this.alGuardar('Usuario actualizado'));
      return;
    }

    if (!this.formulario.password) return;
    this.usuarioService.crear(this.formulario).subscribe(() => this.alGuardar('Usuario creado'));
  }

  private alGuardar(mensaje: string): void {
    this.mostrarFormulario.set(false);
    this.cargar();
    this.messageService.add({ severity: 'success', summary: mensaje });
  }

  cambiarEstado(usuario: Usuario): void {
    this.usuarioService.cambiarEstado(usuario.id, usuario.estado !== 'A').subscribe(() => this.cargar());
  }

  abrirReset(usuario: Usuario): void {
    this.usuarioResetId = usuario.id;
    this.nuevaPassword = '';
    this.mostrarReset.set(true);
  }

  confirmarReset(): void {
    if (!this.usuarioResetId || !this.nuevaPassword) return;
    this.usuarioService.resetPassword(this.usuarioResetId, this.nuevaPassword).subscribe(() => {
      this.mostrarReset.set(false);
      this.messageService.add({ severity: 'success', summary: 'Contraseña restablecida' });
    });
  }
}
