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
import { ClienteService } from '../../core/services/catalogo.service';
import { Cliente, ClienteCreate } from '../../core/models/catalogo.models';

@Component({
  selector: 'app-clientes',
  standalone: true,
  imports: [FormsModule, ButtonModule, DialogModule, InputTextModule, TableModule, TagModule, ConfirmDialogModule],
  providers: [ConfirmationService],
  templateUrl: './clientes.component.html'
})
export class ClientesComponent implements OnInit {
  readonly clientes = signal<Cliente[]>([]);
  readonly cargando = signal(false);
  readonly mostrarFormulario = signal(false);

  editandoId: number | null = null;
  formulario: ClienteCreate = this.vacio();

  constructor(
    private readonly clienteService: ClienteService,
    private readonly messageService: MessageService,
    private readonly confirmationService: ConfirmationService
  ) {}

  ngOnInit(): void {
    this.cargar();
  }

  private vacio(): ClienteCreate {
    return { nombre: '', identificacionFiscal: '', telefono: '', email: '', direccion: '' };
  }

  private cargar(): void {
    this.cargando.set(true);
    this.clienteService.listar().subscribe({
      next: (clientes) => {
        this.clientes.set(clientes);
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

  editar(cliente: Cliente): void {
    this.editandoId = cliente.id;
    this.formulario = { ...cliente };
    this.mostrarFormulario.set(true);
  }

  guardar(): void {
    if (!this.formulario.nombre) return;
    const accion: Observable<unknown> = this.editandoId
      ? this.clienteService.actualizar(this.editandoId, this.formulario)
      : this.clienteService.crear(this.formulario);

    accion.subscribe(() => {
      this.mostrarFormulario.set(false);
      this.cargar();
      this.messageService.add({ severity: 'success', summary: this.editandoId ? 'Cliente actualizado' : 'Cliente creado' });
    });
  }

  confirmarDesactivar(cliente: Cliente): void {
    this.confirmationService.confirm({
      message: `¿Desactivar "${cliente.nombre}"?`,
      header: 'Confirmar',
      icon: 'pi pi-exclamation-triangle',
      accept: () => this.clienteService.desactivar(cliente.id).subscribe(() => this.cargar())
    });
  }
}
