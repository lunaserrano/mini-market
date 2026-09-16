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
import { Producto, TipoPrecio } from '../../core/models/producto.models';
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
    this.detalles = [...this.detalles, { productoId: 0, tipoPrecioId: 0, cantidad: 1, costoUnidadMedida: 0 }];
  }

  quitarLinea(index: number): void {
    this.detalles = this.detalles.filter((_, i) => i !== index);
  }

  get total(): number {
    return this.detalles.reduce((acc, d) => acc + d.cantidad * d.costoUnidadMedida, 0);
  }

  subtotalLinea(linea: DetalleCompraCreate): number {
    return linea.cantidad * linea.costoUnidadMedida;
  }

  /** Presentaciones disponibles del producto elegido en esa línea (ya vienen embebidas en Producto). */
  presentacionesDe(productoId: number): TipoPrecio[] {
    return this.productos().find((p) => p.id === productoId)?.tiposPrecio ?? [];
  }

  /** Al elegir producto: autoselecciona su presentación default y sugiere su costo de referencia. */
  onProductoChange(linea: DetalleCompraCreate, productoId: number): void {
    linea.productoId = productoId;
    const presentaciones = this.presentacionesDe(productoId);
    const sugerida = presentaciones.find((t) => t.esDefault) ?? presentaciones[0];
    linea.tipoPrecioId = sugerida?.id ?? 0;
    linea.costoUnidadMedida = sugerida?.precioCompra ?? 0;
  }

  /** Al cambiar la unidad de medida: vuelve a sugerir el costo de referencia de esa presentación. */
  onTipoPrecioChange(linea: DetalleCompraCreate, tipoPrecioId: number): void {
    linea.tipoPrecioId = tipoPrecioId;
    const tipoPrecio = this.presentacionesDe(linea.productoId).find((t) => t.id === tipoPrecioId);
    linea.costoUnidadMedida = tipoPrecio?.precioCompra ?? 0;
  }

  /** Costo por unidad base equivalente, solo como referencia visual (no se guarda aparte). */
  costoPorUnidadBase(linea: DetalleCompraCreate): number {
    const tipoPrecio = this.presentacionesDe(linea.productoId).find((t) => t.id === linea.tipoPrecioId);
    return tipoPrecio && tipoPrecio.cantidadBase > 0 ? linea.costoUnidadMedida / tipoPrecio.cantidadBase : 0;
  }

  guardar(): void {
    if (!this.proveedorId || this.detalles.length === 0 || this.detalles.some((d) => !d.productoId || !d.tipoPrecioId)) return;

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
