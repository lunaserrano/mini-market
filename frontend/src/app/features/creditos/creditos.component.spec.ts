import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MessageService } from 'primeng/api';
import { environment } from '../../../environments/environment';
import { Credito, CreditoResumen } from '../../core/models/credito.models';
import { guardarSesionFake } from '../../core/testing/sesion-fake';
import { CreditosComponent } from './creditos.component';

const URL = `${environment.apiUrl}/creditos`;

function credito(id: number, extra: Partial<CreditoResumen> = {}): CreditoResumen {
  return {
    id,
    ventaId: id * 10,
    ventaFolio: id,
    clienteId: 1,
    clienteNombre: 'Ana',
    fechaCreacion: '2026-09-01T10:00:00',
    fechaVencimiento: null,
    montoOriginal: 100,
    saldoPendiente: 100,
    estado: 'PENDIENTE',
    vencido: false,
    ...extra
  };
}

describe('CreditosComponent', () => {
  let http: HttpTestingController;

  function crear(lista: CreditoResumen[]): CreditosComponent {
    guardarSesionFake(['creditos.ver', 'creditos.abonar']);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), MessageService] });
    http = TestBed.inject(HttpTestingController);
    const componente = TestBed.createComponent(CreditosComponent).componentInstance;
    componente.ngOnInit();
    http.expectOne(URL).flush(lista);
    return componente;
  }

  beforeEach(() => localStorage.clear());
  afterEach(() => http.verify());

  it('los indicadores cuentan solo lo pendiente, sin importar el filtro activo', () => {
    const c = crear([
      credito(1, { saldoPendiente: 40 }),
      credito(2, { saldoPendiente: 60, vencido: true, fechaVencimiento: '2026-09-05T23:59:59' }),
      credito(3, { saldoPendiente: 0, estado: 'PAGADO' }),
      credito(4, { estado: 'ANULADO' })
    ]);

    c.filtroEstado.set('PAGADO');

    expect(c.totalPorCobrar()).toBe(100);
    expect(c.cantidadPendientes()).toBe(2);
    expect(c.cantidadVencidos()).toBe(1);
    expect(c.totalVencido()).toBe(60);
  });

  it('filtra por estado (pendientes por defecto) y por cliente', () => {
    const c = crear([credito(1), credito(2, { clienteId: 2, clienteNombre: 'Beto' }), credito(3, { estado: 'PAGADO', saldoPendiente: 0 })]);

    expect(c.visibles().map((x) => x.id)).toEqual([1, 2]);

    c.filtroEstado.set('TODOS');
    expect(c.visibles()).toHaveLength(3);

    c.filtroCliente.set(2);
    expect(c.visibles().map((x) => x.id)).toEqual([2]);
    expect(c.opcionesCliente().map((o) => o.label)).toEqual(['Ana', 'Beto']);
  });

  it('el abono arranca con el saldo completo y valida contra el saldo pendiente', () => {
    const c = crear([credito(1, { saldoPendiente: 75.5 })]);

    c.abrirAbono(c.creditos()[0]);
    expect(c.abono.monto).toBe(75.5);
    expect(c.abonoValido()).toBe(true);

    c.abono.monto = 75.51;
    expect(c.abonoValido()).toBe(false);
    c.abono.monto = 0;
    expect(c.abonoValido()).toBe(false);
    c.abono.monto = 20;
    expect(c.abonoValido()).toBe(true);
    expect(c.saldoTrasAbono()).toBeCloseTo(55.5);
  });

  it('confirmar un abono lo envía al backend y recarga el listado', () => {
    const c = crear([credito(1, { saldoPendiente: 50 })]);
    c.abrirAbono(c.creditos()[0]);
    c.abono = { metodo: 'TARJETA', monto: 50, referencia: '  V-9 ' };

    c.confirmarAbono();

    const post = http.expectOne(`${URL}/1/abonos`);
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual({ metodo: 'TARJETA', monto: 50, referencia: 'V-9' });
    post.flush({ ...credito(1, { saldoPendiente: 0, estado: 'PAGADO' }), totalVenta: 100, abonos: [] } satisfies Credito);

    http.expectOne(URL).flush([credito(1, { saldoPendiente: 0, estado: 'PAGADO' })]);
    expect(c.mostrarAbono()).toBe(false);
    expect(c.totalPorCobrar()).toBe(0);
  });

  it('no envía un abono inválido', () => {
    const c = crear([credito(1, { saldoPendiente: 10 })]);
    c.abrirAbono(c.creditos()[0]);
    c.abono.monto = 11;

    c.confirmarAbono();

    http.expectNone(`${URL}/1/abonos`);
  });

  it('calcula el pago inicial y el porcentaje pagado', () => {
    const c = crear([]);
    const detalle: Credito = { ...credito(1, { montoOriginal: 80, saldoPendiente: 20 }), totalVenta: 100, abonos: [] };

    expect(c.pagoInicial(detalle)).toBe(20);
    expect(c.totalAbonado(detalle)).toBe(60);
    expect(c.porcentajePagado(detalle)).toBe(75);
  });
});
