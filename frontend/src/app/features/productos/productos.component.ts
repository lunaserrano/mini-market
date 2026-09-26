import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { CheckboxModule } from 'primeng/checkbox';
import { MessageService, ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfigService } from '../../core/services/config.service';
import { CategoriaService } from '../../core/services/catalogo.service';
import { ProductoService } from '../../core/services/producto.service';
import { Categoria } from '../../core/models/catalogo.models';
import { Producto, TipoPrecioCreate, TipoPrecio } from '../../core/models/producto.models';
import { MonedaPipe } from '../../core/pipes/moneda.pipe';
import { BarcodeScannerComponent } from '../../shared/components/barcode-scanner/barcode-scanner.component';
import { esDispositivoTactil } from '../../shared/utils/device.utils';

interface FormularioProducto {
  categoriaId: number | null;
  nombre: string;
  descripcion: string;
  codigoBarras: string;
  codigoInterno: string;
  unidadBase: string;
  tiposPrecio: FilaTipoPrecio[];
}

/** Fila de formulario para un tipo de precio: además del DTO que se envía a la Api, guarda
 * "precioBase" (sin IVA) solo como ayuda visual — se recalcula en vivo junto con "precioVenta"
 * (con IVA, el que realmente se persiste) usando la tasa de IVA de la empresa. */
type FilaTipoPrecio = TipoPrecioCreate & { precioBase: number };

/** Unidades base sugeridas (autocompletar vía <datalist>) — cubre productos que se venden
 * fraccionados por peso (ej. verduras: papas por libra/media libra) además de unidad entera. */
export const UNIDADES_BASE_SUGERIDAS = ['unidad', 'libra', 'kilogramo', 'gramo', 'litro', 'onza', 'galón', 'arroba', 'quintal'];

@Component({
  selector: 'app-productos',
  standalone: true,
  imports: [
    MonedaPipe,
    DecimalPipe,
    FormsModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TagModule,
    CheckboxModule,
    ConfirmDialogModule,
    BarcodeScannerComponent
  ],
  providers: [ConfirmationService],
  templateUrl: './productos.component.html'
})
export class ProductosComponent implements OnInit {
  readonly productos = signal<Producto[]>([]);
  readonly categorias = signal<Categoria[]>([]);
  readonly cargando = signal(false);

  readonly mostrarFormulario = signal(false);
  editandoId: number | null = null;
  formulario: FormularioProducto = this.formularioVacio();
  readonly unidadesBaseSugeridas = UNIDADES_BASE_SUGERIDAS;

  /** Solo en dispositivos táctiles se ofrece escanear el código de barras con la cámara — en
   * escritorio se sigue usando la pistola láser física, que escribe directo en el input. */
  readonly esTactil = esDispositivoTactil();
  readonly mostrarEscaner = signal(false);

  /** Factor para convertir precio sin IVA <-> con IVA, usando la tasa única configurada en Configuración. */
  readonly factorImpuesto = computed(() => 1 + this.configService.tasaImpuesto() / 100);

  /** Presentaciones (tipos de precio) del producto que se está editando — a diferencia de
   * "formulario.tiposPrecio" (que se envía todo junto al crear), estas se agregan/eliminan una
   * por una contra la Api porque el producto ya existe. Así se puede, por ejemplo, agregarle
   * "Media libra" a un producto de verdura que ya se creó solo con "Libra". */
  readonly tiposPrecioExistentes = signal<TipoPrecio[]>([]);
  nuevoTipoPrecio: FilaTipoPrecio = this.tipoPrecioVacio();
  readonly agregandoTipoPrecio = signal(false);

  constructor(
    private readonly productoService: ProductoService,
    private readonly categoriaService: CategoriaService,
    private readonly configService: ConfigService,
    private readonly messageService: MessageService,
    private readonly confirmationService: ConfirmationService
  ) {}

  ngOnInit(): void {
    this.categoriaService.listar().subscribe((categorias) => this.categorias.set(categorias));
    this.cargar();
  }

  private cargar(): void {
    this.cargando.set(true);
    this.productoService.listar().subscribe({
      next: (productos) => {
        this.productos.set(productos);
        this.cargando.set(false);
      },
      error: () => this.cargando.set(false)
    });
  }

  private formularioVacio(): FormularioProducto {
    return {
      categoriaId: null,
      nombre: '',
      descripcion: '',
      codigoBarras: '',
      codigoInterno: '',
      unidadBase: 'unidad',
      tiposPrecio: [{ nombre: 'Unidad', cantidadBase: 1, precioVenta: 0, precioBase: 0, precioCompra: 0, esDefault: true }]
    };
  }

  nuevoProducto(): void {
    this.editandoId = null;
    this.formulario = this.formularioVacio();
    this.mostrarFormulario.set(true);
  }

  editarProducto(producto: Producto): void {
    this.editandoId = producto.id;
    this.formulario = {
      categoriaId: producto.categoriaId,
      nombre: producto.nombre,
      descripcion: producto.descripcion ?? '',
      codigoBarras: producto.codigoBarras ?? '',
      codigoInterno: producto.codigoInterno ?? '',
      unidadBase: producto.unidadBase,
      tiposPrecio: []
    };
    this.tiposPrecioExistentes.set(producto.tiposPrecio);
    this.nuevoTipoPrecio = this.tipoPrecioVacio();
    this.mostrarFormulario.set(true);
  }

  onCodigoEscaneado(codigo: string): void {
    this.formulario.codigoBarras = codigo;
    this.mostrarEscaner.set(false);
  }

  private tipoPrecioVacio(): FilaTipoPrecio {
    return { nombre: '', cantidadBase: 1, precioVenta: 0, precioBase: 0, precioCompra: 0, esDefault: false };
  }

  /** Precio sin IVA de un precio con IVA ya guardado — solo para mostrarlo de referencia en la lista. */
  precioSinIva(precioConIva: number): number {
    return Math.round((precioConIva / this.factorImpuesto()) * 100) / 100;
  }

  /** El admin escribió el precio SIN IVA: recalcula el precio CON IVA (el que se guarda). */
  actualizarDesdeBase(fila: FilaTipoPrecio, precioBase: number): void {
    fila.precioBase = precioBase;
    fila.precioVenta = Math.round(precioBase * this.factorImpuesto() * 100) / 100;
  }

  /** El admin escribió el precio CON IVA directamente: recalcula el precio sin IVA (solo referencia). */
  actualizarDesdeVenta(fila: FilaTipoPrecio, precioVenta: number): void {
    fila.precioVenta = precioVenta;
    fila.precioBase = this.precioSinIva(precioVenta);
  }

  agregarTipoPrecio(): void {
    this.formulario.tiposPrecio = [...this.formulario.tiposPrecio, this.tipoPrecioVacio()];
  }

  quitarTipoPrecio(index: number): void {
    this.formulario.tiposPrecio = this.formulario.tiposPrecio.filter((_, i) => i !== index);
  }

  /** Agrega una presentación nueva a un producto YA EXISTENTE (ej. "Media libra" a la papa que
   * ya se creó solo con "Libra") — llama a la Api de inmediato, no espera a "Guardar". */
  guardarTipoPrecioExistente(): void {
    if (!this.editandoId || !this.nuevoTipoPrecio.nombre || this.nuevoTipoPrecio.cantidadBase <= 0) return;

    const { precioBase, ...dto } = this.nuevoTipoPrecio;
    this.agregandoTipoPrecio.set(true);
    this.productoService.agregarTipoPrecio(this.editandoId, dto).subscribe({
      next: () => {
        this.agregandoTipoPrecio.set(false);
        this.nuevoTipoPrecio = this.tipoPrecioVacio();
        this.recargarTiposPrecioExistentes();
        this.messageService.add({ severity: 'success', summary: 'Presentación agregada' });
      },
      error: () => this.agregandoTipoPrecio.set(false)
    });
  }

  eliminarTipoPrecioExistente(tipoPrecio: TipoPrecio): void {
    if (!this.editandoId) return;
    this.confirmationService.confirm({
      message: `¿Eliminar la presentación "${tipoPrecio.nombre}"?`,
      header: 'Confirmar',
      icon: 'pi pi-exclamation-triangle',
      accept: () =>
        this.productoService.eliminarTipoPrecio(this.editandoId!, tipoPrecio.id).subscribe(() => this.recargarTiposPrecioExistentes())
    });
  }

  private recargarTiposPrecioExistentes(): void {
    if (!this.editandoId) return;
    this.productoService.obtener(this.editandoId).subscribe((producto) => {
      this.tiposPrecioExistentes.set(producto.tiposPrecio);
      this.cargar();
    });
  }

  guardar(): void {
    if (!this.formulario.categoriaId || !this.formulario.nombre) return;

    if (this.editandoId) {
      const { tiposPrecio, ...dto } = this.formulario;
      this.productoService.actualizar(this.editandoId, { ...dto, categoriaId: this.formulario.categoriaId, proveedorId: null }).subscribe(() => {
        this.mostrarFormulario.set(false);
        this.cargar();
        this.messageService.add({ severity: 'success', summary: 'Producto actualizado' });
      });
      return;
    }

    if (this.formulario.tiposPrecio.length === 0 || !this.formulario.tiposPrecio.some((t) => t.esDefault)) {
      this.messageService.add({ severity: 'warn', summary: 'Debe marcar un tipo de precio como default' });
      return;
    }

    const tiposPrecio = this.formulario.tiposPrecio.map(({ precioBase, ...tp }) => tp);
    this.productoService
      .crear({ ...this.formulario, categoriaId: this.formulario.categoriaId, proveedorId: null, tiposPrecio })
      .subscribe(() => {
        this.mostrarFormulario.set(false);
        this.cargar();
        this.messageService.add({ severity: 'success', summary: 'Producto creado' });
      });
  }

  confirmarDesactivar(producto: Producto): void {
    this.confirmationService.confirm({
      message: `¿Desactivar "${producto.nombre}"?`,
      header: 'Confirmar',
      icon: 'pi pi-exclamation-triangle',
      accept: () =>
        this.productoService.desactivar(producto.id).subscribe(() => {
          this.cargar();
          this.messageService.add({ severity: 'success', summary: 'Producto desactivado' });
        })
    });
  }
}
