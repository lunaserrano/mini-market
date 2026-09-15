import { DatePipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { MessageService } from 'primeng/api';
import { AuthService } from '../../core/services/auth.service';
import { InventarioService } from '../../core/services/inventario.service';
import { ProductoService } from '../../core/services/producto.service';
import { Inventario, MovimientoInventario } from '../../core/models/inventario.models';
import { Producto } from '../../core/models/producto.models';

@Component({
  selector: 'app-inventario',
  standalone: true,
  imports: [DatePipe, FormsModule, ButtonModule, DialogModule, InputTextModule, SelectModule, TableModule, TabsModule],
  templateUrl: './inventario.component.html'
})
export class InventarioComponent implements OnInit {
  readonly inventario = signal<Inventario[]>([]);
  readonly movimientos = signal<MovimientoInventario[]>([]);
  readonly productos = signal<Producto[]>([]);
  readonly cargando = signal(false);

  readonly mostrarAjuste = signal(false);
  ajuste = { productoId: null as number | null, cantidadAjuste: 0, observacion: '' };

  constructor(
    private readonly inventarioService: InventarioService,
    private readonly productoService: ProductoService,
    private readonly authService: AuthService,
    private readonly messageService: MessageService
  ) {}

  ngOnInit(): void {
    this.productoService.listar().subscribe((productos) => this.productos.set(productos));
    this.cargar();
  }

  private get sucursalId(): number | null {
    return this.authService.usuario()?.sucursalId ?? null;
  }

  private cargar(): void {
    this.cargando.set(true);
    this.inventarioService.listar(this.sucursalId ?? undefined).subscribe({
      next: (inventario) => {
        this.inventario.set(inventario);
        this.cargando.set(false);
      },
      error: () => this.cargando.set(false)
    });
    this.inventarioService.listarMovimientos().subscribe((movimientos) => this.movimientos.set(movimientos));
  }

  abrirAjuste(): void {
    this.ajuste = { productoId: null, cantidadAjuste: 0, observacion: '' };
    this.mostrarAjuste.set(true);
  }

  confirmarAjuste(): void {
    if (!this.ajuste.productoId || !this.sucursalId || this.ajuste.cantidadAjuste === 0) return;

    this.inventarioService
      .ajustar({ productoId: this.ajuste.productoId, sucursalId: this.sucursalId, cantidadAjuste: this.ajuste.cantidadAjuste, observacion: this.ajuste.observacion })
      .subscribe(() => {
        this.mostrarAjuste.set(false);
        this.cargar();
        this.messageService.add({ severity: 'success', summary: 'Inventario ajustado' });
      });
  }
}
