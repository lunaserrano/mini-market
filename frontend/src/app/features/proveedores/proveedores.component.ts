import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService, MessageService } from 'primeng/api';
import { Observable } from 'rxjs';
import { ProveedorService } from '../../core/services/catalogo.service';
import { Proveedor, ProveedorCreate } from '../../core/models/catalogo.models';

@Component({
  selector: 'app-proveedores',
  standalone: true,
  imports: [FormsModule, ButtonModule, DialogModule, InputTextModule, TableModule, TagModule, ConfirmDialogModule],
  providers: [ConfirmationService],
  templateUrl: './proveedores.component.html'
})
export class ProveedoresComponent implements OnInit {
  readonly proveedores = signal<Proveedor[]>([]);
  readonly cargando = signal(false);
  readonly mostrarFormulario = signal(false);

  editandoId: number | null = null;
  formulario: ProveedorCreate = this.vacio();

  constructor(
    private readonly proveedorService: ProveedorService,
    private readonly messageService: MessageService,
    private readonly confirmationService: ConfirmationService
  ) {}

  ngOnInit(): void {
    this.cargar();
  }

  private vacio(): ProveedorCreate {
    return { nombre: '', contacto: '', telefono: '', email: '', direccion: '', identificacionFiscal: '' };
  }

  private cargar(): void {
    this.cargando.set(true);
    this.proveedorService.listar().subscribe({
      next: (proveedores) => {
        this.proveedores.set(proveedores);
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

  editar(proveedor: Proveedor): void {
    this.editandoId = proveedor.id;
    this.formulario = { ...proveedor };
    this.mostrarFormulario.set(true);
  }

  guardar(): void {
    if (!this.formulario.nombre) return;
    const accion: Observable<unknown> = this.editandoId
      ? this.proveedorService.actualizar(this.editandoId, this.formulario)
      : this.proveedorService.crear(this.formulario);

    accion.subscribe(() => {
      this.mostrarFormulario.set(false);
      this.cargar();
      this.messageService.add({ severity: 'success', summary: this.editandoId ? 'Proveedor actualizado' : 'Proveedor creado' });
    });
  }

  confirmarDesactivar(proveedor: Proveedor): void {
    this.confirmationService.confirm({
      message: `¿Desactivar "${proveedor.nombre}"?`,
      header: 'Confirmar',
      icon: 'pi pi-exclamation-triangle',
      accept: () => this.proveedorService.desactivar(proveedor.id).subscribe(() => this.cargar())
    });
  }
}
