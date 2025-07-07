import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { Dialog } from 'primeng/dialog';
import { InputText } from 'primeng/inputtext';
import { Button } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { SpinnerService } from '../../shared/spinner.service';

interface Categoria {
    nombre: string;
    descripcion: string;
}

@Component({
    selector: 'app-main-categorias',
    standalone: true,
    imports: [CommonModule, FormsModule, TableModule, Dialog, InputText, Button, CardModule],
    template: `
    <div class="p-4">
      <h2 class="text-xl font-bold mb-4 text-primary">Categorías</h2>
      <div class="flex justify-between items-center mb-2">
        <span class="font-semibold">Lista de categorías</span>
        <p-button type="button" label="Registrar categoría" icon="pi pi-plus" class="p-button-primary" (click)="abrirDialogoNuevo()"></p-button>
      </div>
      <p-table [value]="categorias" class="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th>Nombre</th>
            <th>Descripción</th>
            <th class="text-center">Acciones</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-categoria>
          <tr>
            <td>{{ categoria.nombre }}</td>
            <td>{{ categoria.descripcion }}</td>
            <td class="flex gap-2 justify-center">
              <p-button icon="pi pi-pencil" class="p-button-sm p-button-text p-button-info" (onClick)="editarCategoria(categoria)" ariaLabel="Editar"></p-button>
              <p-button icon="pi pi-trash" class="p-button-sm p-button-text p-button-danger" (onClick)="confirmarEliminar(categoria)" ariaLabel="Eliminar"></p-button>
            </td>
          </tr>
        </ng-template>
      </p-table>

      <p-dialog header="{{editando ? 'Editar categoría' : 'Registrar categoría'}}" [(visible)]="showDialog" [modal]="true" [style]="{width: '30rem'}" [closable]="true" (onHide)="resetForm()">
        <form (ngSubmit)="guardarCategoria()" #form="ngForm" class="p-fluid">
          <p-card class="mb-4">
            <div class="mb-3 w-full">
              <label for="nombre" class="block mb-1 font-medium">Nombre</label>
              <input pInputText id="nombre" name="nombre" [(ngModel)]="nuevaCategoria.nombre" required class="w-full" />
            </div>
            <div class="mb-4 w-full">
              <label for="descripcion" class="block mb-1 font-medium">Descripción</label>
              <input pInputText id="descripcion" name="descripcion" [(ngModel)]="nuevaCategoria.descripcion" required class="w-full" />
            </div>
          </p-card>
          <div class="flex justify-end gap-2 mt-2">
            <p-button type="button" label="Cancelar" class="p-button-secondary" (onClick)="showDialog = false"></p-button>
            <p-button type="submit" label="{{editando ? 'Actualizar' : 'Registrar'}}" class="p-button-primary" [disabled]="form.invalid"></p-button>
          </div>
        </form>
      </p-dialog>

      <p-dialog header="Confirmar eliminación" [(visible)]="showDialogEliminar" [modal]="true" [style]="{width: '25rem'}">
        <div>¿Está seguro de que desea eliminar la categoría <b>{{categoriaAEliminar?.nombre}}</b>?</div>
        <div class="flex justify-end gap-2 mt-4">
          <p-button type="button" label="Cancelar" class="p-button-secondary" (onClick)="showDialogEliminar = false"></p-button>
          <p-button type="button" label="Eliminar" class="p-button-danger" (onClick)="eliminarCategoriaConfirmada()"></p-button>
        </div>
      </p-dialog>
    </div>
  `
})
export class MainCategoriasComponent implements OnInit {
    categorias: Categoria[] = [
        { nombre: 'Electrónica', descripcion: 'Dispositivos electrónicos y gadgets' },
        { nombre: 'Hogar', descripcion: 'Artículos para el hogar y cocina' },
    ];

    showDialog = false;
    showDialogEliminar = false;
    editando = false;
    categoriaEditandoIndex: number | null = null;
    categoriaAEliminar: Categoria | null = null;

    nuevaCategoria: Categoria = { nombre: '', descripcion: '' };

    constructor(private spinner: SpinnerService) { }

    ngOnInit() {
        this.spinner.show();
        setTimeout(() => {
            this.spinner.hide();
        }, 500);
    }

    abrirDialogoNuevo() {
        this.editando = false;
        this.nuevaCategoria = { nombre: '', descripcion: '' };
        this.showDialog = true;
    }

    guardarCategoria() {
        this.spinner.show();
        setTimeout(() => {
            if (this.editando && this.categoriaEditandoIndex !== null) {
                this.categorias[this.categoriaEditandoIndex] = { ...this.nuevaCategoria };
            } else {
                this.categorias.push({ ...this.nuevaCategoria });
            }
            this.showDialog = false;
            this.resetForm();
            this.spinner.hide();
        }, 500);
    }

    resetForm() {
        this.nuevaCategoria = { nombre: '', descripcion: '' };
        this.editando = false;
        this.categoriaEditandoIndex = null;
    }

    editarCategoria(categoria: Categoria) {
        this.editando = true;
        this.categoriaEditandoIndex = this.categorias.indexOf(categoria);
        this.nuevaCategoria = { ...categoria };
        this.showDialog = true;
    }

    confirmarEliminar(categoria: Categoria) {
        this.categoriaAEliminar = categoria;
        this.showDialogEliminar = true;
    }

    eliminarCategoriaConfirmada() {
        if (this.categoriaAEliminar) {
            this.categorias = this.categorias.filter(c => c !== this.categoriaAEliminar);
            this.categoriaAEliminar = null;
            this.showDialogEliminar = false;
        }
    }
}
