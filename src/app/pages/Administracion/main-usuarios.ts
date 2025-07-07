import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { Dialog } from 'primeng/dialog';
import { InputText } from 'primeng/inputtext';
import { Button } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DropdownModule } from 'primeng/dropdown';
import { SpinnerService } from '../../shared/spinner.service';

interface Usuario {
    cod_usuario: string;
    nombre_usuario: string;
    password_hash: string;
    nombre_completo: string;
    rol: 'admin' | 'cajero' | 'supervisor';
    activo: boolean;
}

@Component({
    selector: 'app-main-usuarios',
    standalone: true,
    imports: [CommonModule, FormsModule, TableModule, Dialog, InputText, Button, CardModule, DropdownModule],
    template: `
    <div class="p-4">
      <h2 class="text-xl font-bold mb-4 text-primary">Usuarios</h2>
      <div class="flex justify-between items-center mb-2">
        <span class="font-semibold">Gestión de usuarios</span>
        <p-button type="button" label="Registrar usuario" icon="pi pi-plus" class="p-button-primary" (click)="abrirDialogoNuevo()"></p-button>
      </div>
      <p-table [value]="usuarios" class="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th>Código</th>
            <th>Usuario</th>
            <th>Nombre completo</th>
            <th>Rol</th>
            <th>Activo</th>
            <th class="text-center">Acciones</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-usuario>
          <tr>
            <td>{{ usuario.cod_usuario }}</td>
            <td>{{ usuario.nombre_usuario }}</td>
            <td>{{ usuario.nombre_completo }}</td>
            <td>{{ usuario.rol | titlecase }}</td>
            <td>
              <span [ngClass]="usuario.activo ? 'text-green-600' : 'text-red-600'">{{ usuario.activo ? 'Sí' : 'No' }}</span>
            </td>
            <td class="flex gap-2 justify-center">
              <p-button icon="pi pi-pencil" class="p-button-sm p-button-text p-button-info" (onClick)="editarUsuario(usuario)" ariaLabel="Editar"></p-button>
              <p-button icon="pi pi-trash" class="p-button-sm p-button-text p-button-danger" (onClick)="confirmarEliminar(usuario)" ariaLabel="Eliminar"></p-button>
            </td>
          </tr>
        </ng-template>
      </p-table>

      <p-dialog header="{{editando ? 'Editar usuario' : 'Registrar usuario'}}" [(visible)]="showDialog" [modal]="true" [style]="{width: '30rem'}" [closable]="true" (onHide)="resetForm()">
        <form (ngSubmit)="guardarUsuario()" #form="ngForm" class="p-fluid">
          <p-card class="mb-4">
            <div class="mb-3 w-full">
              <label for="nombre_usuario" class="block mb-1 font-medium">Usuario</label>
              <input pInputText id="nombre_usuario" name="nombre_usuario" [(ngModel)]="nuevoUsuario.nombre_usuario" required class="w-full" />
            </div>
            <div class="mb-3 w-full">
              <label for="password_hash" class="block mb-1 font-medium">Contraseña</label>
              <input pInputText id="password_hash" name="password_hash" [(ngModel)]="nuevoUsuario.password_hash" [type]="editando ? 'password' : 'password'" required class="w-full" />
            </div>
            <div class="mb-3 w-full">
              <label for="nombre_completo" class="block mb-1 font-medium">Nombre completo</label>
              <input pInputText id="nombre_completo" name="nombre_completo" [(ngModel)]="nuevoUsuario.nombre_completo" required class="w-full" />
            </div>
            <div class="mb-3 w-full">
              <label for="rol" class="block mb-1 font-medium">Rol</label>
              <p-dropdown [options]="roles" [(ngModel)]="nuevoUsuario.rol" name="rol" optionLabel="label" optionValue="value" placeholder="Seleccione rol" class="w-full"></p-dropdown>
            </div>
            <div class="mb-3 w-full">
              <label for="activo" class="block mb-1 font-medium">Activo</label>
              <input type="checkbox" id="activo" name="activo" [(ngModel)]="nuevoUsuario.activo" />
            </div>
          </p-card>
          <div class="flex justify-end gap-2 mt-2">
            <p-button type="button" label="Cancelar" class="p-button-secondary" (onClick)="showDialog = false"></p-button>
            <p-button type="submit" label="{{editando ? 'Actualizar' : 'Registrar'}}" class="p-button-primary" [disabled]="form.invalid"></p-button>
          </div>
        </form>
      </p-dialog>

      <p-dialog header="Confirmar eliminación" [(visible)]="showDialogEliminar" [modal]="true" [style]="{width: '25rem'}">
        <div>¿Está seguro de que desea eliminar el usuario <b>{{usuarioAEliminar?.nombre_usuario}}</b>?</div>
        <div class="flex justify-end gap-2 mt-4">
          <p-button type="button" label="Cancelar" class="p-button-secondary" (onClick)="showDialogEliminar = false"></p-button>
          <p-button type="button" label="Eliminar" class="p-button-danger" (onClick)="eliminarUsuarioConfirmado()"></p-button>
        </div>
      </p-dialog>
    </div>
  `
})
export class MainUsuariosComponent {
    usuarios: Usuario[] = [];
    showDialog = false;
    showDialogEliminar = false;
    editando = false;
    usuarioEditandoIndex: number | null = null;
    usuarioAEliminar: Usuario | null = null;
    nuevoUsuario: Usuario = { cod_usuario: '', nombre_usuario: '', password_hash: '', nombre_completo: '', rol: 'cajero', activo: true };
    roles = [
        { label: 'Administrador', value: 'admin' },
        { label: 'Cajero', value: 'cajero' },
        { label: 'Supervisor', value: 'supervisor' }
    ];

    constructor(private spinner: SpinnerService) { }

    ngOnInit() {
        this.spinner.show();
        setTimeout(() => {
            this.spinner.hide();
        }, 500);
    }

    abrirDialogoNuevo() {
        this.editando = false;
        this.nuevoUsuario = { cod_usuario: '', nombre_usuario: '', password_hash: '', nombre_completo: '', rol: 'cajero', activo: true };
        this.showDialog = true;
    }

    guardarUsuario() {
        this.spinner.show();
        setTimeout(() => {
            if (this.editando && this.usuarioEditandoIndex !== null) {
                this.usuarios[this.usuarioEditandoIndex] = { ...this.nuevoUsuario };
            } else {
                this.nuevoUsuario.cod_usuario = 'U' + (this.usuarios.length + 1).toString().padStart(4, '0');
                this.usuarios.push({ ...this.nuevoUsuario });
            }
            this.showDialog = false;
            this.resetForm();
            this.spinner.hide();
        }, 500);
    }

    resetForm() {
        this.nuevoUsuario = { cod_usuario: '', nombre_usuario: '', password_hash: '', nombre_completo: '', rol: 'cajero', activo: true };
        this.editando = false;
        this.usuarioEditandoIndex = null;
    }

    editarUsuario(usuario: Usuario) {
        this.editando = true;
        this.usuarioEditandoIndex = this.usuarios.indexOf(usuario);
        this.nuevoUsuario = { ...usuario };
        this.showDialog = true;
    }

    confirmarEliminar(usuario: Usuario) {
        this.usuarioAEliminar = usuario;
        this.showDialogEliminar = true;
    }

    eliminarUsuarioConfirmado() {
        if (this.usuarioAEliminar) {
            this.usuarios = this.usuarios.filter(u => u !== this.usuarioAEliminar);
            this.usuarioAEliminar = null;
            this.showDialogEliminar = false;
        }
    }
}
