import { DatePipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { MessageService } from 'primeng/api';
import { AuthService } from '../../core/services/auth.service';
import { VentaService } from '../../core/services/venta.service';
import { VentaResumen } from '../../core/models/venta.models';
import { MonedaPipe } from '../../core/pipes/moneda.pipe';

@Component({
  selector: 'app-ventas',
  standalone: true,
  imports: [DatePipe, MonedaPipe, FormsModule, RouterLink, ButtonModule, DialogModule, InputTextModule, TableModule, TagModule],
  templateUrl: './ventas.component.html'
})
export class VentasComponent implements OnInit {
  readonly ventas = signal<VentaResumen[]>([]);
  readonly cargando = signal(false);

  readonly mostrarAnular = signal(false);
  ventaAnularId: number | null = null;
  motivoAnulacion = '';

  constructor(
    private readonly ventaService: VentaService,
    readonly authService: AuthService,
    private readonly messageService: MessageService
  ) {}

  ngOnInit(): void {
    this.cargar();
  }

  private cargar(): void {
    this.cargando.set(true);
    this.ventaService.listar().subscribe({
      next: (ventas) => {
        this.ventas.set(ventas);
        this.cargando.set(false);
      },
      error: () => this.cargando.set(false)
    });
  }

  abrirAnular(venta: VentaResumen): void {
    this.ventaAnularId = venta.id;
    this.motivoAnulacion = '';
    this.mostrarAnular.set(true);
  }

  confirmarAnular(): void {
    if (!this.ventaAnularId || !this.motivoAnulacion) return;
    this.ventaService.anular(this.ventaAnularId, this.motivoAnulacion).subscribe(() => {
      this.mostrarAnular.set(false);
      this.cargar();
      this.messageService.add({ severity: 'success', summary: 'Venta anulada' });
    });
  }
}
