import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { Dialog } from 'primeng/dialog';
import { InputText } from 'primeng/inputtext';
import { Button } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { MultiSelectModule } from 'primeng/multiselect';
import { SpinnerService } from '../../shared/spinner.service';

interface Proveedor {
    nombre: string;
    telefono: string;
    direccion: string;
    correo: string;
    productos: string[];
    categorias: string[];
}

@Component({
    selector: 'app-main-proveedores',
    standalone: true,
    imports: [CommonModule, FormsModule, TableModule, Dialog, InputText, Button, CardModule, MultiSelectModule],
    template: `
    <div class="p-4">
      <h2 class="text-xl font-bold mb-4 text-primary">Proveedores</h2>
      <div class="flex justify-between items-center mb-2">
        <span class="font-semibold">Lista de proveedores</span>
        <p-button type="button" label="Registrar proveedor" icon="pi pi-plus" class="p-button-primary" (click)="abrirDialogoNuevo()"></p-button>
      </div>
      <p-table [value]="proveedores" class="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th>Nombre</th>
            <th>Teléfono</th>
            <th>Dirección</th>
            <th>Correo</th>
            <th>Productos</th>
            <th>Categorías</th>
            <th class="text-center">Acciones</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-proveedor>
          <tr>
            <td>{{ proveedor.nombre }}</td>
            <td>{{ proveedor.telefono }}</td>
            <td>{{ proveedor.direccion }}</td>
            <td>{{ proveedor.correo }}</td>
            <td>{{ proveedor.productos.join(', ') }}</td>
            <td>{{ proveedor.categorias.join(', ') }}</td>
            <td class="flex gap-2 justify-center">
              <p-button icon="pi pi-pencil" class="p-button-sm p-button-text p-button-info" (onClick)="editarProveedor(proveedor)" ariaLabel="Editar"></p-button>
              <p-button icon="pi pi-trash" class="p-button-sm p-button-text p-button-danger" (onClick)="confirmarEliminar(proveedor)" ariaLabel="Eliminar"></p-button>
            </td>
          </tr>
        </ng-template>
      </p-table>

      <p-dialog header="{{editando ? 'Editar proveedor' : 'Registrar proveedor'}}" [(visible)]="showDialog" [modal]="true" [style]="{width: '40rem'}" [closable]="true" (onHide)="resetForm()">
        <form (ngSubmit)="guardarProveedor()" #form="ngForm" class="p-fluid">
          <p-card class="mb-4">
            <div class="mb-3 w-full">
              <label for="nombre" class="block mb-1 font-medium">Nombre</label>
              <input pInputText id="nombre" name="nombre" [(ngModel)]="nuevoProveedor.nombre" required class="w-full" />
            </div>
            <div class="mb-3 w-full">
              <label for="telefono" class="block mb-1 font-medium">Teléfono</label>
              <input pInputText id="telefono" name="telefono" [(ngModel)]="nuevoProveedor.telefono" required class="w-full" />
            </div>
            <div class="mb-3 w-full">
              <label for="direccion" class="block mb-1 font-medium">Dirección</label>
              <input pInputText id="direccion" name="direccion" [(ngModel)]="nuevoProveedor.direccion" required class="w-full" />
            </div>
            <div class="mb-3 w-full">
              <label for="correo" class="block mb-1 font-medium">Correo</label>
              <input pInputText id="correo" name="correo" [(ngModel)]="nuevoProveedor.correo" required class="w-full" />
            </div>
            <div class="mb-3 w-full">
              <label for="productos" class="block mb-1 font-medium">Productos</label>
              <p-multiSelect [options]="productosOptions" [(ngModel)]="nuevoProveedor.productos" name="productos" optionLabel="nombre" display="chip" class="w-full"></p-multiSelect>
            </div>
            <div class="mb-3 w-full">
              <label for="categorias" class="block mb-1 font-medium">Categorías</label>
              <p-multiSelect [options]="categoriasOptions" [(ngModel)]="nuevoProveedor.categorias" name="categorias" optionLabel="nombre" display="chip" class="w-full"></p-multiSelect>
            </div>
          </p-card>
          <div class="flex justify-end gap-2 mt-2">
            <p-button type="button" label="Cancelar" class="p-button-secondary" (onClick)="showDialog = false"></p-button>
            <p-button type="submit" label="{{editando ? 'Actualizar' : 'Registrar'}}" class="p-button-primary" [disabled]="form.invalid"></p-button>
          </div>
        </form>
      </p-dialog>

      <p-dialog header="Confirmar eliminación" [(visible)]="showDialogEliminar" [modal]="true" [style]="{width: '25rem'}">
        <div>¿Está seguro de que desea eliminar el proveedor <b>{{proveedorAEliminar?.nombre}}</b>?</div>
        <div class="flex justify-end gap-2 mt-4">
          <p-button type="button" label="Cancelar" class="p-button-secondary" (onClick)="showDialogEliminar = false"></p-button>
          <p-button type="button" label="Eliminar" class="p-button-danger" (onClick)="eliminarProveedorConfirmado()"></p-button>
        </div>
      </p-dialog>
    </div>
  `
})
export class MainProveedoresComponent {
    proveedores: Proveedor[] = [
        {
            nombre: 'Proveedor 1',
            telefono: '123456789',
            direccion: 'Calle 1',
            correo: 'proveedor1@mail.com',
            productos: [],
            categorias: []
        }
    ];

    productosOptions = [
        { nombre: 'Laptop' },
        { nombre: 'Mouse' }
    ];
    categoriasOptions = [
        { nombre: 'Electrónica' },
        { nombre: 'Hogar' }
    ];

    showDialog = false;
    showDialogEliminar = false;
    editando = false;
    proveedorEditandoIndex: number | null = null;
    proveedorAEliminar: Proveedor | null = null;

    nuevoProveedor: Proveedor = {
        nombre: '',
        telefono: '',
        direccion: '',
        correo: '',
        productos: [],
        categorias: []
    };

    constructor(private spinner: SpinnerService) { }

    ngOnInit() {
        this.spinner.show();
        setTimeout(() => {
            this.spinner.hide();
        }, 500);
    }

    abrirDialogoNuevo() {
        this.editando = false;
        this.nuevoProveedor = { nombre: '', telefono: '', direccion: '', correo: '', productos: [], categorias: [] };
        this.showDialog = true;
    }

    guardarProveedor() {
        this.spinner.show();
        setTimeout(() => {
            if (this.editando && this.proveedorEditandoIndex !== null) {
                this.proveedores[this.proveedorEditandoIndex] = { ...this.nuevoProveedor };
            } else {
                this.proveedores.push({ ...this.nuevoProveedor });
            }
            this.showDialog = false;
            this.resetForm();
            this.spinner.hide();
        }, 500);
    }

    resetForm() {
        this.nuevoProveedor = { nombre: '', telefono: '', direccion: '', correo: '', productos: [], categorias: [] };
        this.editando = false;
        this.proveedorEditandoIndex = null;
    }

    editarProveedor(proveedor: Proveedor) {
        this.editando = true;
        this.proveedorEditandoIndex = this.proveedores.indexOf(proveedor);
        this.nuevoProveedor = { ...proveedor };
        this.showDialog = true;
    }

    confirmarEliminar(proveedor: Proveedor) {
        this.proveedorAEliminar = proveedor;
        this.showDialogEliminar = true;
    }

    eliminarProveedorConfirmado() {
        if (this.proveedorAEliminar) {
            this.proveedores = this.proveedores.filter(p => p !== this.proveedorAEliminar);
            this.proveedorAEliminar = null;
            this.showDialogEliminar = false;
        }
    }
}
