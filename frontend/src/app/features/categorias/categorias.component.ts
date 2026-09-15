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
import { CategoriaService } from '../../core/services/catalogo.service';
import { Categoria } from '../../core/models/catalogo.models';

@Component({
  selector: 'app-categorias',
  standalone: true,
  imports: [FormsModule, ButtonModule, DialogModule, InputTextModule, TableModule, TagModule, ConfirmDialogModule],
  providers: [ConfirmationService],
  templateUrl: './categorias.component.html'
})
export class CategoriasComponent implements OnInit {
  readonly categorias = signal<Categoria[]>([]);
  readonly cargando = signal(false);
  readonly mostrarFormulario = signal(false);

  editandoId: number | null = null;
  formulario = { nombre: '', descripcion: '' };

  constructor(
    private readonly categoriaService: CategoriaService,
    private readonly messageService: MessageService,
    private readonly confirmationService: ConfirmationService
  ) {}

  ngOnInit(): void {
    this.cargar();
  }

  private cargar(): void {
    this.cargando.set(true);
    this.categoriaService.listar().subscribe({
      next: (categorias) => {
        this.categorias.set(categorias);
        this.cargando.set(false);
      },
      error: () => this.cargando.set(false)
    });
  }

  nueva(): void {
    this.editandoId = null;
    this.formulario = { nombre: '', descripcion: '' };
    this.mostrarFormulario.set(true);
  }

  editar(categoria: Categoria): void {
    this.editandoId = categoria.id;
    this.formulario = { nombre: categoria.nombre, descripcion: categoria.descripcion ?? '' };
    this.mostrarFormulario.set(true);
  }

  guardar(): void {
    if (!this.formulario.nombre) return;
    const accion: Observable<unknown> = this.editandoId
      ? this.categoriaService.actualizar(this.editandoId, this.formulario)
      : this.categoriaService.crear(this.formulario);

    accion.subscribe(() => {
      this.mostrarFormulario.set(false);
      this.cargar();
      this.messageService.add({ severity: 'success', summary: this.editandoId ? 'Categoría actualizada' : 'Categoría creada' });
    });
  }

  confirmarDesactivar(categoria: Categoria): void {
    this.confirmationService.confirm({
      message: `¿Desactivar "${categoria.nombre}"?`,
      header: 'Confirmar',
      icon: 'pi pi-exclamation-triangle',
      accept: () => this.categoriaService.desactivar(categoria.id).subscribe(() => this.cargar())
    });
  }
}
