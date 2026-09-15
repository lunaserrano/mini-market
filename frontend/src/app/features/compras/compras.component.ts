import { DatePipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { MessageService } from 'primeng/api';
import { CompraService } from '../../core/services/compra.service';
import { ProveedorService } from '../../core/services/catalogo.service';
import { ProductoService } from '../../core/services/producto.service';
import { CompraResumen, DetalleCompraCreate } from '../../core/models/compra.models';
import { Proveedor } from '../../core/models/catalogo.models';
import { Producto } from '../../core/models/producto.models';
import { MonedaPipe } from '../../core/pipes/moneda.pipe';

@Component({
  selector: 'app-compras',
  standalone: true,
  imports: [DatePipe, MonedaPipe, FormsModule, ButtonModule, DialogModule, InputTextModule, SelectModule, TableModule, TagModule],
  templateUrl: './compras.component.html'
})
export class ComprasComponent implements OnInit {
  readonly compras = signal<CompraResumen[]>([]);
  readonly proveedores = signal<Proveedor[]>([]);
  readonly productos = signal<Producto[]>([]);
  readonly cargando = signal(false);

  readonly mostrarFormulario = signal(false);
  proveedorId: number | null = null;
  numeroDocumentoProveedor = '';
  detalles: DetalleCompraCreate[] = [];

  constructor(
    private readonly compraService: CompraService,
    private readonly proveedorService: ProveedorService,
    private readonly productoService: ProductoService,
    private readonly messageService: MessageService
  ) {}

  ngOnInit(): void {
    this.proveedorService.listar().subscribe((proveedores) => this.proveedores.set(proveedores));
    this.productoService.listar().subscribe((productos) => this.productos.set(productos));
    this.cargar();
  }

  private cargar(): void {
    this.cargando.set(true);
    this.compraService.listar().subscribe({
      next: (compras) => {
        this.compras.set(compras);
        this.cargando.set(false);
      },
      error: () => this.cargando.set(false)
    });
  }

  nuevaCompra(): void {
    this.proveedorId = null;
    this.numeroDocumentoProveedor = '';
    this.detalles = [];
    this.mostrarFormulario.set(true);
  }

  agregarLinea(): void {
    this.detalles = [...this.detalles, { productoId: 0, cantidad: 1, cantidadBase: 1, costoUnitario: 0 }];
  }

  quitarLinea(index: number): void {
    this.detalles = this.detalles.filter((_, i) => i !== index);
  }

  get total(): number {
    return this.detalles.reduce((acc, d) => acc + d.cantidad * d.costoUnitario, 0);
  }

  subtotalLinea(linea: DetalleCompraCreate): number {
    return linea.cantidad * linea.costoUnitario;
  }

  guardar(): void {
    if (!this.proveedorId || this.detalles.length === 0 || this.detalles.some((d) => !d.productoId)) return;

    this.compraService.crear({ proveedorId: this.proveedorId, numeroDocumentoProveedor: this.numeroDocumentoProveedor, detalles: this.detalles }).subscribe(() => {
      this.mostrarFormulario.set(false);
      this.cargar();
      this.messageService.add({ severity: 'success', summary: 'Compra registrada' });
    });
  }

  anular(compra: CompraResumen): void {
    this.compraService.anular(compra.id).subscribe(() => this.cargar());
  }
}
