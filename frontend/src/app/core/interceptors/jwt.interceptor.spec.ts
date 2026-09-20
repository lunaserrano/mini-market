import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { environment } from '../../../environments/environment';
import { guardarSesionFake, sesionFake } from '../testing/sesion-fake';
import { jwtInterceptor } from './jwt.interceptor';

describe('jwtInterceptor', () => {
  const api = environment.apiUrl;
  const recurso = `${api}/productos`;
  let http: HttpClient;
  let backend: HttpTestingController;
  let router: Router;

  function iniciar(): void {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([jwtInterceptor])), provideHttpClientTesting()]
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  }

  beforeEach(() => localStorage.clear());
  afterEach(() => backend.verify());

  it('agrega el Bearer del access token vigente', () => {
    guardarSesionFake([], 'jwt-vigente', 'r1', 10);
    iniciar();

    http.get(recurso).subscribe();

    expect(backend.expectOne(recurso).request.headers.get('Authorization')).toBe('Bearer jwt-vigente');
  });

  it('no agrega token a login ni a refresh', () => {
    guardarSesionFake([], 'jwt-vigente', 'r1', 10);
    iniciar();

    http.post(`${api}/auth/login`, {}).subscribe();
    http.post(`${api}/auth/refresh`, {}).subscribe();

    expect(backend.expectOne(`${api}/auth/login`).request.headers.has('Authorization')).toBe(false);
    expect(backend.expectOne(`${api}/auth/refresh`).request.headers.has('Authorization')).toBe(false);
  });

  it('sin sesión envía la petición tal cual', () => {
    iniciar();

    http.get(recurso).subscribe();

    expect(backend.expectOne(recurso).request.headers.has('Authorization')).toBe(false);
  });

  it('ante un 401 renueva la sesión una vez y reintenta con el token nuevo', () => {
    guardarSesionFake([], 'jwt-viejo', 'refresh-viejo', 10);
    iniciar();
    let respuesta: unknown;

    http.get(recurso).subscribe((r) => (respuesta = r));
    backend.expectOne(recurso).flush({}, { status: 401, statusText: 'Unauthorized' });

    const refresh = backend.expectOne(`${api}/auth/refresh`);
    expect(refresh.request.body).toEqual({ refreshToken: 'refresh-viejo' });
    refresh.flush(sesionFake([], 'jwt-nuevo', 'refresh-nuevo'));

    const reintento = backend.expectOne(recurso);
    expect(reintento.request.headers.get('Authorization')).toBe('Bearer jwt-nuevo');
    reintento.flush({ ok: true });
    expect(respuesta).toEqual({ ok: true });
  });

  it('varias peticiones que fallan a la vez comparten una sola renovación', () => {
    guardarSesionFake([], 'jwt-viejo', 'refresh-viejo', 10);
    iniciar();
    const otro = `${api}/categorias`;

    http.get(recurso).subscribe();
    http.get(otro).subscribe();
    backend.expectOne(recurso).flush({}, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne(otro).flush({}, { status: 401, statusText: 'Unauthorized' });

    backend.expectOne(`${api}/auth/refresh`).flush(sesionFake([], 'jwt-nuevo', 'refresh-nuevo'));

    backend.expectOne(recurso).flush({});
    backend.expectOne(otro).flush({});
  });

  it('si la renovación falla cierra la sesión y propaga el 401', () => {
    guardarSesionFake([], 'jwt-viejo', 'refresh-viejo', 10);
    iniciar();
    let status = 0;

    http.get(recurso).subscribe({ error: (e) => (status = e.status) });
    backend.expectOne(recurso).flush({}, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne(`${api}/auth/refresh`).flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(status).toBe(401);
    expect(localStorage.getItem('mm_token')).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/auth/login']);
  });

  it('un error de la petición reintentada NO cierra la sesión', () => {
    guardarSesionFake([], 'jwt-viejo', 'refresh-viejo', 10);
    iniciar();
    let status = 0;

    http.get(recurso).subscribe({ error: (e) => (status = e.status) });
    backend.expectOne(recurso).flush({}, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne(`${api}/auth/refresh`).flush(sesionFake([], 'jwt-nuevo', 'refresh-nuevo'));
    backend.expectOne(recurso).flush({ error: 'mal' }, { status: 400, statusText: 'Bad Request' });

    expect(status).toBe(400);
    expect(localStorage.getItem('mm_token')).toBe('jwt-nuevo');
  });

  it('un 401 sin refresh token cierra la sesión directamente', () => {
    guardarSesionFake([], 'jwt-viejo', 'refresh-viejo', 10);
    localStorage.removeItem('mm_refresh');
    iniciar();

    http.get(recurso).subscribe({ error: () => undefined });
    backend.expectOne(recurso).flush({}, { status: 401, statusText: 'Unauthorized' });

    backend.expectNone(`${api}/auth/refresh`);
    expect(router.navigate).toHaveBeenCalledWith(['/auth/login']);
  });

  it('los errores que no son 401 pasan sin renovar la sesión', () => {
    guardarSesionFake([], 'jwt-viejo', 'refresh-viejo', 10);
    iniciar();
    let status = 0;

    http.get(recurso).subscribe({ error: (e) => (status = e.status) });
    backend.expectOne(recurso).flush({}, { status: 403, statusText: 'Forbidden' });

    expect(status).toBe(403);
    backend.expectNone(`${api}/auth/refresh`);
  });

  it('si el access token está por vencer lo renueva ANTES de enviar la petición', () => {
    guardarSesionFake([], 'jwt-viejo', 'refresh-viejo', 0); // vence ahora
    iniciar();

    http.get(recurso).subscribe();
    backend.expectOne(`${api}/auth/refresh`).flush(sesionFake([], 'jwt-nuevo', 'refresh-nuevo'));

    expect(backend.expectOne(recurso).request.headers.get('Authorization')).toBe('Bearer jwt-nuevo');
  });
});
