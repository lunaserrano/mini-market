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
import { forkJoin } from 'rxjs';
import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import { CajaService } from '../../core/services/caja.service';
import { VentaService } from '../../core/services/venta.service';
import { AuthService } from '../../core/services/auth.service';
import { ConfigService } from '../../core/services/config.service';
import { Caja, MovimientoCaja } from '../../core/models/caja.models';
import { VentaResumen } from '../../core/models/venta.models';
import { MonedaPipe } from '../../core/pipes/moneda.pipe';
import { PERMISOS } from '../../core/security/permisos';

@Component({
  selector: 'app-caja',
  standalone: true,
  imports: [
    DatePipe,
    MonedaPipe,
    FormsModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TagModule
  ],
  templateUrl: './caja.component.html'
})
export class CajaComponent implements OnInit {
  readonly caja = signal<Caja | null>(null);
  readonly movimientos = signal<MovimientoCaja[]>([]);
  readonly cargando = signal(false);

  readonly mostrarApertura = signal(false);
  montoInicial = 0;

  readonly mostrarCierre = signal(false);
  montoFinalDeclarado = 0;

  readonly mostrarMovimiento = signal(false);
  nuevoMovimiento = { tipo: 'INGRESO' as 'INGRESO' | 'EGRESO', concepto: '', monto: 0 };
  readonly tiposMovimiento = [
    { label: 'Ingreso', value: 'INGRESO' },
    { label: 'Egreso', value: 'EGRESO' }
  ];

  readonly historial = signal<Caja[]>([]);
  readonly cargandoHistorial = signal(false);

  readonly mostrarDetalleCierre = signal(false);
  readonly cargandoDetalleCierre = signal(false);
  readonly cierreSeleccionado = signal<Caja | null>(null);
  readonly ventasCierre = signal<VentaResumen[]>([]);
  readonly movimientosCierre = signal<MovimientoCaja[]>([]);

  constructor(
    private readonly cajaService: CajaService,
    private readonly ventaService: VentaService,
    readonly authService: AuthService,
    private readonly configService: ConfigService,
    private readonly messageService: MessageService
  ) {}

  ngOnInit(): void {
    this.cargar();
    if (this.authService.tienePermiso(PERMISOS.CajaVerTodas)) {
      this.cargarHistorial();
    }
  }

  private cargar(): void {
    this.cargando.set(true);
    this.cajaService.obtenerActual().subscribe({
      next: (caja) => {
        this.caja.set(caja);
        this.cargando.set(false);
        if (caja) this.cargarMovimientos(caja.id);
      },
      error: () => {
        this.caja.set(null);
        this.cargando.set(false);
      }
    });
  }

  private cargarMovimientos(cajaId: number): void {
    this.cajaService.listarMovimientos(cajaId).subscribe((movimientos) => this.movimientos.set(movimientos));
  }

  private cargarHistorial(): void {
    this.cargandoHistorial.set(true);
    this.cajaService.listar().subscribe({
      next: (historial) => {
        this.historial.set(historial);
        this.cargandoHistorial.set(false);
      },
      error: () => this.cargandoHistorial.set(false)
    });
  }

  abrirDialogoApertura(): void {
    this.montoInicial = 0;
    this.mostrarApertura.set(true);
  }

  confirmarApertura(): void {
    this.cajaService.abrir(this.montoInicial).subscribe((caja) => {
      this.caja.set(caja);
      this.movimientos.set([]);
      this.mostrarApertura.set(false);
      this.messageService.add({ severity: 'success', summary: 'Caja abierta', detail: `Monto inicial: Q${this.montoInicial.toFixed(2)}` });
    });
  }

  abrirDialogoCierre(): void {
    this.montoFinalDeclarado = 0;
    this.mostrarCierre.set(true);
  }

  confirmarCierre(): void {
    const cajaActual = this.caja();
    if (!cajaActual) return;

    this.cajaService.cerrar(cajaActual.id, this.montoFinalDeclarado).subscribe((caja) => {
      this.caja.set(caja);
      this.mostrarCierre.set(false);
      this.messageService.add({
        severity: caja.diferencia === 0 ? 'success' : 'warn',
        summary: 'Caja cerrada',
        detail: `Diferencia: Q${caja.diferencia?.toFixed(2)}`,
        life: 6000
      });
      if (this.authService.tienePermiso(PERMISOS.CajaVerTodas)) this.cargarHistorial();
    });
  }

  abrirDialogoMovimiento(): void {
    this.nuevoMovimiento = { tipo: 'INGRESO', concepto: '', monto: 0 };
    this.mostrarMovimiento.set(true);
  }

  confirmarMovimiento(): void {
    const cajaActual = this.caja();
    if (!cajaActual || !this.nuevoMovimiento.concepto || this.nuevoMovimiento.monto <= 0) return;

    this.cajaService.registrarMovimiento(cajaActual.id, this.nuevoMovimiento).subscribe(() => {
      this.mostrarMovimiento.set(false);
      this.cargarMovimientos(cajaActual.id);
    });
  }

  verDetalleCierre(caja: Caja): void {
    this.cierreSeleccionado.set(caja);
    this.mostrarDetalleCierre.set(true);
    this.cargandoDetalleCierre.set(true);
    forkJoin([this.ventaService.listar(caja.id), this.cajaService.listarMovimientos(caja.id)]).subscribe({
      next: ([ventas, movimientos]) => {
        this.ventasCierre.set(ventas);
        this.movimientosCierre.set(movimientos);
        this.cargandoDetalleCierre.set(false);
      },
      error: () => this.cargandoDetalleCierre.set(false)
    });
  }

  descargarReporte(caja: Caja): void {
    forkJoin([this.ventaService.listar(caja.id), this.cajaService.listarMovimientos(caja.id)]).subscribe(([ventas, movimientos]) => {
      this.generarReportePdf(caja, ventas, movimientos);
    });
  }

  private generarReportePdf(caja: Caja, ventas: VentaResumen[], movimientos: MovimientoCaja[]): void {
    const simbolo = this.configService.simboloMoneda();
    const moneda = (valor: number | null | undefined) => `${simbolo}${(valor ?? 0).toFixed(2)}`;
    const doc = new jsPDF();

    doc.setFontSize(14);
    doc.text(this.configService.empresa()?.nombre ?? 'Mini Market', 14, 16);
    doc.setFontSize(12);
    doc.text(`Cierre de caja #${caja.id}`, 14, 24);

    doc.setFontSize(10);
    const info = [
      [`Apertura: ${new Date(caja.fechaApertura).toLocaleString()}`, `Usuario: ${caja.usuarioAperturaNombre}`],
      [`Cierre: ${caja.fechaCierre ? new Date(caja.fechaCierre).toLocaleString() : '—'}`, `Usuario: ${caja.usuarioCierreNombre ?? '—'}`],
      [`Monto inicial: ${moneda(caja.montoInicial)}`, `Monto final declarado: ${moneda(caja.montoFinalDeclarado)}`],
      [`Monto final sistema: ${moneda(caja.montoFinalSistema)}`, `Diferencia: ${moneda(caja.diferencia)}`]
    ];
    let y = 32;
    for (const [izq, der] of info) {
      doc.text(izq, 14, y);
      doc.text(der, 110, y);
      y += 6;
    }

    doc.setFontSize(11);
    doc.text('Ventas del turno', 14, y + 6);
    autoTable(doc, {
      startY: y + 10,
      head: [['Folio', 'Fecha', 'Cliente', 'Total', 'Estado']],
      body: ventas.map((v) => [
        `#${v.folio}`,
        new Date(v.fecha).toLocaleString(),
        v.clienteNombre ?? 'Consumidor final',
        moneda(v.total),
        v.estado
      ]),
      headStyles: { fillColor: [51, 65, 85] }
    });

    const finalY = (doc as unknown as { lastAutoTable: { finalY: number } }).lastAutoTable.finalY;
    doc.setFontSize(11);
    doc.text('Movimientos manuales', 14, finalY + 10);
    autoTable(doc, {
      startY: finalY + 14,
      head: [['Fecha', 'Tipo', 'Concepto', 'Monto']],
      body: movimientos.map((m) => [new Date(m.fecha).toLocaleString(), m.tipo, m.concepto, moneda(m.monto)]),
      headStyles: { fillColor: [51, 65, 85] }
    });

    doc.save(`cierre-caja-${caja.id}.pdf`);
  }
}
