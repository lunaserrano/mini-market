import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { Dialog } from 'primeng/dialog';
import { InputText } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { InputNumberModule } from 'primeng/inputnumber';
import { Button } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { SelectModule } from 'primeng/select';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { SpinnerService } from '../../shared/spinner.service';

interface Producto {
  nombre: string;
  descripcion: string;
  cod_categoria: string;
  precio_compra: number;
  precio_venta: number;
  stock: number;
  stock_minimo: number;
  estado: 'A' | 'I';
}

@Component({
  selector: 'app-main-productos',
  standalone: true,
  imports: [CommonModule, FormsModule, TableModule, Dialog, InputText, Button, TextareaModule, InputNumberModule, CardModule, SelectModule, AutoCompleteModule],
  template: `
    <div class="p-4">
      <h2 class="text-xl font-bold mb-4 text-primary">Productos</h2>
      <div class="flex justify-between items-center mb-2">
        <span class="font-semibold">Lista de productos</span>
        <p-button type="button" label="Registrar producto" icon="pi pi-plus" class="p-button-primary" (click)="abrirDialogoNuevo()"></p-button>
      </div>
      <p-table [value]="productos" class="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th>Nombre</th>
            <th>Descripción</th>
            <th>Categoría</th>
            <th>Precio compra</th>
            <th>Precio venta</th>
            <th>Stock</th>
            <th>Mínimo</th>
            <th>Estado</th>
            <th class="flex justify-center">Acciones</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-producto>
          <tr [ngClass]="{
                'bg-red-200 text-red-900': producto.stock === 0,
                'bg-yellow-100 text-yellow-900': producto.stock > 0 && producto.stock <= producto.stock_minimo
              }">
            <td>{{ producto.nombre }}</td>
            <td>{{ producto.descripcion }}</td>
            <td>{{ obtenerNombreCategoria(producto.cod_categoria) }}</td>
            <td>{{ producto.precio_compra | currency:'USD' }}</td>
            <td>{{ producto.precio_venta | currency:'USD' }}</td>
            <td>{{ producto.stock }}</td>
            <td>{{ producto.stock_minimo }}</td>
            <td>
              <span [ngClass]="{'text-green-600': producto.estado === 'A', 'text-red-600': producto.estado === 'I'}">
                {{ producto.estado === 'A' ? 'Activo' : 'Inactivo' }}
              </span>
            </td>
            <td class="flex justify-center">
              <p-button icon="pi pi-pencil" class="p-button-sm p-button-text p-button-info" (onClick)="editarProducto(producto)" ariaLabel="Editar"></p-button>
              <p-button icon="pi pi-trash" class="p-button-sm p-button-text p-button-danger" (onClick)="confirmarEliminar(producto)" ariaLabel="Eliminar"></p-button>
            </td>
          </tr>
        </ng-template>
      </p-table>
      <div *ngIf="alertaStock" class="p-2 bg-red-200 text-red-800 rounded mt-2">
        ¡Atención! Hay productos con stock igual o menor al mínimo.
      </div>

      <p-dialog header="{{editando ? 'Editar producto' : 'Registrar producto'}}" [(visible)]="showDialog" [modal]="true" [style]="{width: '40rem'}" [closable]="true" (onHide)="resetForm()">
        <form (ngSubmit)="guardarProducto()" #form="ngForm" class="p-fluid">
          <p-card class="mb-4">
            <div class="mb-3 w-full">
              <label for="nombre" class="block mb-1 font-medium">Nombre</label>
              <input pInputText id="nombre" name="nombre" [(ngModel)]="nuevoProducto.nombre" required class="w-full" />
            </div>
            <div class="mb-3 w-full">
              <label for="descripcion" class="block mb-1 font-medium">Descripción</label>
              <textarea pTextarea id="descripcion" name="descripcion" [(ngModel)]="nuevoProducto.descripcion" required class="w-full" rows="3"></textarea>
            </div>
            <div class="mb-3 w-full">
              <label for="cod_categoria" class="block mb-1 font-medium">Categoría</label>
              <p-autocomplete [(ngModel)]="nuevoProducto.cod_categoria"
                [suggestions]="filteredCategorias"
                (completeMethod)="filterCategorias($event)"
                [dropdown]="true"
                field="nombre"
                [forceSelection]="true"
                [minLength]="0"
                inputId="cod_categoria"
                name="cod_categoria"
                class="w-full"
                [styleClass]="'w-full'"
                placeholder="Seleccione una categoría">
              </p-autocomplete>
            </div>
            <div class="mb-3 w-full">
              <label for="precio_compra" class="block mb-1 font-medium">Precio compra</label>
              <p-inputnumber [(ngModel)]="nuevoProducto.precio_compra" name="precio_compra" inputId="precio_compra" mode="currency" class="w-full" currency="USD" locale="en-US"></p-inputnumber>
            </div>
            <div class="mb-3 w-full">
              <label for="precio_venta" class="block mb-1 font-medium">Precio venta</label>
              <p-inputnumber [(ngModel)]="nuevoProducto.precio_venta" name="precio_venta" inputId="precio_venta" mode="currency" class="w-full" currency="USD" locale="en-US"></p-inputnumber>
            </div>
            <div class="mb-3 w-full">
              <label for="stock" class="block mb-1 font-medium">Stock</label>
              <p-inputnumber [(ngModel)]="nuevoProducto.stock" name="stock" inputId="stock" [min]=0 class="w-full"></p-inputnumber>
            </div>
            <div class="mb-3 w-full">
              <label for="stock_minimo" class="block mb-1 font-medium">Stock mínimo</label>
              <p-inputnumber [(ngModel)]="nuevoProducto.stock_minimo" name="stock_minimo" inputId="stock_minimo" [min]=0 class="w-full"></p-inputnumber>
            </div>
            <div class="mb-3 w-full">
              <label for="estado" class="block mb-1 font-medium">Estado</label>
              <p-select [options]="estados" id="estado" name="estado" [(ngModel)]="nuevoProducto.estado" class="w-full" placeholder="Seleccione un estado"
              optionLabel="label" optionValue="value">
              </p-select>
            </div>
          </p-card>
          <div class="flex justify-end gap-2 mt-2">
            <p-button type="button" label="Cancelar" class="p-button-secondary" (onClick)="showDialog = false"></p-button>
            <p-button type="submit" label="{{editando ? 'Actualizar' : 'Registrar'}}" class="p-button-primary" [disabled]="form.invalid"></p-button>
          </div>
        </form>
      </p-dialog>

      <p-dialog header="Confirmar eliminación" [(visible)]="showDialogEliminar" [modal]="true" [style]="{width: '25rem'}">
        <div>¿Está seguro de que desea eliminar el producto <b>{{productoAEliminar?.nombre}}</b>?</div>
        <div class="flex justify-end gap-2 mt-4">
          <p-button type="button" label="Cancelar" class="p-button-secondary" (onClick)="showDialogEliminar = false"></p-button>
          <p-button type="button" label="Eliminar" class="p-button-danger" (onClick)="eliminarProductoConfirmado()"></p-button>
        </div>
      </p-dialog>
    </div>
  `
})
export class MainProductosComponent {
  productos: Producto[] = [
    {
      nombre: 'Laptop',
      descripcion: 'Portátil de alto rendimiento',
      cod_categoria: 'Electrónica',
      precio_compra: 900,
      precio_venta: 1200,
      stock: 10,
      stock_minimo: 2,
      estado: 'A'
    },
    {
      nombre: 'Mouse',
      descripcion: 'Mouse inalámbrico',
      cod_categoria: 'Electrónica',
      precio_compra: 15,
      precio_venta: 25,
      stock: 3,
      stock_minimo: 5,
      estado: 'A'
    },
  ];

  categorias = [
    { nombre: 'Electrónica', descripcion: 'Dispositivos electrónicos y gadgets' },
    { nombre: 'Hogar', descripcion: 'Artículos para el hogar y cocina' },
  ];
  estados = [
    { label: 'Activo', value: 'A' },
    { label: 'Inactivo', value: 'I' }
  ];

  showDialog = false;
  showDialogEliminar = false;
  alertaStock = false;
  editando = false;
  productoEditandoIndex: number | null = null;
  productoAEliminar: Producto | null = null;

  nuevoProducto: Producto = {
    nombre: '',
    descripcion: '',
    cod_categoria: '',
    precio_compra: 0,
    precio_venta: 0,
    stock: 0,
    stock_minimo: 0,
    estado: 'A'
  };

  filteredCategorias: any[] = [];

  constructor(private spinner: SpinnerService) { }

  ngOnInit() {
    this.spinner.show();
    // Simulación de carga de datos
    setTimeout(() => {
      this.spinner.hide();
    }, 500);
  }

  abrirDialogoNuevo() {
    this.editando = false;
    this.nuevoProducto = { nombre: '', descripcion: '', cod_categoria: '', precio_compra: 0, precio_venta: 0, stock: 0, stock_minimo: 0, estado: 'A' };
    this.showDialog = true;
  }

  guardarProducto() {
    this.spinner.show();
    // Simulación de guardado
    setTimeout(() => {
      if (this.editando && this.productoEditandoIndex !== null) {
        this.productos[this.productoEditandoIndex] = { ...this.nuevoProducto };
      } else {
        this.productos.push({ ...this.nuevoProducto });
      }
      this.showDialog = false;
      this.resetForm();
      this.verificarAlertaStock();
      this.spinner.hide();
    }, 500);
  }

  resetForm() {
    this.nuevoProducto = { nombre: '', descripcion: '', cod_categoria: '', precio_compra: 0, precio_venta: 0, stock: 0, stock_minimo: 0, estado: 'A' };
    this.editando = false;
    this.productoEditandoIndex = null;
  }

  editarProducto(producto: Producto) {
    this.editando = true;
    this.productoEditandoIndex = this.productos.indexOf(producto);
    this.nuevoProducto = { ...producto };
    this.showDialog = true;
  }

  confirmarEliminar(producto: Producto) {
    this.productoAEliminar = producto;
    this.showDialogEliminar = true;
  }

  eliminarProductoConfirmado() {
    if (this.productoAEliminar) {
      this.productos = this.productos.filter(p => p !== this.productoAEliminar);
      this.productoAEliminar = null;
      this.showDialogEliminar = false;
      this.verificarAlertaStock();
    }
  }

  verificarAlertaStock() {
    this.alertaStock = this.productos.some(p => p.stock <= p.stock_minimo);
  }

  obtenerNombreCategoria(cod_categoria: string) {
    const cat = this.categorias.find(c => c.nombre === cod_categoria);
    return cat ? cat.nombre : cod_categoria;
  }

  filterCategorias(event: any) {
    const query = event.query ? event.query.toLowerCase() : '';
    this.filteredCategorias = this.categorias.filter(cat =>
      cat.nombre.toLowerCase().includes(query)
    );
  }
}
