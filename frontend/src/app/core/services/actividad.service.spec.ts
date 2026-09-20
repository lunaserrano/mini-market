import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { environment } from '../../../environments/environment';
import { guardarSesionFake } from '../testing/sesion-fake';
import { ActividadService, describirClic } from './actividad.service';

function html(contenido: string): HTMLElement {
  const contenedor = document.createElement('div');
  contenedor.innerHTML = contenido;
  document.body.appendChild(contenedor);
  return contenedor;
}

describe('describirClic', () => {
  afterEach(() => (document.body.innerHTML = ''));

  it('describe un botón por su texto, aunque se haga clic en un elemento interno', () => {
    const c = html('<button id="cobrar"><span class="p-button-label">Cobrar</span></button>');

    const { detalle, datos } = describirClic(c.querySelector('span')!);

    expect(detalle).toBe('Clic en botón "Cobrar"');
    expect(datos['tag']).toBe('BUTTON');
    expect(datos['id']).toBe('cobrar');
  });

  it('un botón solo con icono se identifica por la clase del icono', () => {
    const c = html('<button><i class="pi pi-trash"></i></button>');

    expect(describirClic(c.querySelector('i')!).detalle).toBe('Clic en botón "icono pi-trash"');
  });

  it('nunca incluye el valor de un campo, y menos el de una contraseña', () => {
    const c = html('<input type="password" name="password" value="secreto123" placeholder="Contraseña">');
    const input = c.querySelector('input')!;
    input.value = 'secreto123';

    const { detalle, datos } = describirClic(input);

    expect(JSON.stringify({ detalle, datos })).not.toContain('secreto123');
    expect(datos['tipo']).toBe('password');
    expect(datos['texto']).toBe('Contraseña'); // solo la etiqueta accesible
  });

  it('en un enlace guarda la ruta sin la query string', () => {
    const c = html('<a href="/ventas/5?token=abc">Ver venta</a>');

    expect(describirClic(c.querySelector('a')!).datos['enlace']).toBe('/ventas/5');
  });

  it('acota los textos largos y respeta data-audit', () => {
    const c = html(`<div data-audit="Anular venta"><button>${'x'.repeat(500)}</button></div>`);

    const { datos } = describirClic(c.querySelector('button')!);

    expect(datos['texto']!.length).toBeLessThanOrEqual(81);
    expect(datos['accion']).toBe('Anular venta');
  });
});

describe('ActividadService', () => {
  const url = `${environment.apiUrl}/auditoria/cliente`;
  let backend: HttpTestingController;
  let servicio: ActividadService;

  function preparar(): void {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    backend = TestBed.inject(HttpTestingController);
    servicio = TestBed.inject(ActividadService);
    servicio.iniciar();
  }

  beforeEach(() => {
    localStorage.clear();
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.useRealTimers();
    document.body.innerHTML = '';
  });

  it('agrupa los clics y los envía en un solo lote', () => {
    guardarSesionFake([], 'jwt', 'r', 10);
    preparar();
    const c = html('<button>Guardar</button><button>Cancelar</button>');

    c.querySelectorAll('button').forEach((b) => b.click());
    backend.expectNone(url); // todavía dentro de la ventana de agrupación
    vi.advanceTimersByTime(2_500);

    const lote = backend.expectOne(url).request.body as { tipo: string; detalle: string }[];
    expect(lote.map((e) => e.detalle)).toEqual(['Clic en botón "Guardar"', 'Clic en botón "Cancelar"']);
    expect(lote.every((e) => e.tipo === 'CLIC_UI')).toBe(true);
  });

  it('sin sesión no registra nada', () => {
    preparar();
    html('<button>Entrar</button>').querySelector('button')!.click();

    vi.advanceTimersByTime(5_000);

    backend.expectNone(url);
  });

  it('registra los cambios de pantalla', async () => {
    guardarSesionFake([], 'jwt', 'r', 10);
    TestBed.configureTestingModule({
      providers: [provideRouter([{ path: 'ventas', children: [] }]), provideHttpClient(), provideHttpClientTesting()]
    });
    backend = TestBed.inject(HttpTestingController);
    TestBed.inject(ActividadService).iniciar();

    await TestBed.inject(Router).navigateByUrl('/ventas');
    vi.advanceTimersByTime(2_500);

    const lote = backend.expectOne(url).request.body as { tipo: string; ruta: string }[];
    expect(lote).toEqual([expect.objectContaining({ tipo: 'NAVEGACION_UI', ruta: '/ventas' })]);
  });

  it('si el envío falla conserva los eventos y los reintenta', () => {
    guardarSesionFake([], 'jwt', 'r', 10);
    preparar();
    html('<button>Guardar</button>').querySelector('button')!.click();

    vi.advanceTimersByTime(2_500);
    backend.expectOne(url).flush({ error: 'caído' }, { status: 500, statusText: 'Server Error' });
    vi.advanceTimersByTime(11_000);

    const reintento = backend.expectOne(url).request.body as unknown[];
    expect(reintento).toHaveLength(1);
  });
});
