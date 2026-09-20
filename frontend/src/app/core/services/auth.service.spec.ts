import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { environment } from '../../../environments/environment';
import { guardarSesionFake, sesionFake } from '../testing/sesion-fake';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  const api = environment.apiUrl;

  function crear(): { auth: AuthService; http: HttpTestingController; router: Router } {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    const router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
    return { auth: TestBed.inject(AuthService), http: TestBed.inject(HttpTestingController), router };
  }

  beforeEach(() => localStorage.clear());
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  describe('permisos', () => {
    it('tienePermiso es verdadero si tiene AL MENOS UNO de los indicados', () => {
      guardarSesionFake(['ventas.ver', 'productos.ver']);
      const { auth } = crear();

      expect(auth.tienePermiso('ventas.ver')).toBe(true);
      expect(auth.tienePermiso('usuarios.ver', 'productos.ver')).toBe(true);
      expect(auth.tienePermiso('usuarios.ver', 'roles.ver')).toBe(false);
    });

    it('tieneTodos exige todos los permisos indicados', () => {
      guardarSesionFake(['ventas.ver', 'productos.ver']);
      const { auth } = crear();

      expect(auth.tieneTodos('ventas.ver', 'productos.ver')).toBe(true);
      expect(auth.tieneTodos('ventas.ver', 'usuarios.ver')).toBe(false);
    });

    it('sin sesión no tiene ningún permiso', () => {
      const { auth } = crear();

      expect(auth.estaAutenticado()).toBe(false);
      expect(auth.tienePermiso('ventas.ver')).toBe(false);
      expect(auth.tieneTodos('ventas.ver')).toBe(false);
    });

    it('un permiso a medias (prefijo) no cuenta', () => {
      guardarSesionFake(['ventas.ver_todas']);
      const { auth } = crear();

      expect(auth.tienePermiso('ventas.ver')).toBe(false);
    });

    it('debeCambiarPassword refleja el flag de la sesión', () => {
      guardarSesionFake([], 'jwt', 'r', 10, { debeCambiarPassword: true });
      const { auth } = crear();

      expect(auth.debeCambiarPassword()).toBe(true);
    });
  });

  describe('login', () => {
    it('guarda tokens y usuario, y actualiza el estado', () => {
      const { auth, http } = crear();

      auth.login({ username: 'ana', password: 'x' }).subscribe();
      http.expectOne(`${api}/auth/login`).flush(sesionFake(['ventas.ver'], 'jwt-1', 'refresh-1'));

      expect(auth.estaAutenticado()).toBe(true);
      expect(auth.tienePermiso('ventas.ver')).toBe(true);
      expect(localStorage.getItem('mm_token')).toBe('jwt-1');
      expect(localStorage.getItem('mm_refresh')).toBe('refresh-1');
    });
  });

  describe('refrescar', () => {
    it('varias llamadas simultáneas comparten una sola petición (el refresh token rota en cada uso)', () => {
      guardarSesionFake(['ventas.ver'], 'jwt-viejo', 'refresh-viejo');
      const { auth, http } = crear();
      const tokens: string[] = [];

      auth.refrescar().subscribe((r) => tokens.push(r.token));
      auth.refrescar().subscribe((r) => tokens.push(r.token));
      http.expectOne(`${api}/auth/refresh`).flush(sesionFake(['ventas.ver', 'ventas.crear'], 'jwt-2', 'refresh-2'));

      expect(tokens).toEqual(['jwt-2', 'jwt-2']);
      expect(localStorage.getItem('mm_refresh')).toBe('refresh-2');
      // Los permisos se actualizan con los del rol al momento de renovar.
      expect(auth.tienePermiso('ventas.crear')).toBe(true);
    });

    it('envía el refresh token guardado', () => {
      guardarSesionFake([], 'jwt-viejo', 'refresh-viejo');
      const { auth, http } = crear();

      auth.refrescar().subscribe();
      const req = http.expectOne(`${api}/auth/refresh`);

      expect(req.request.body).toEqual({ refreshToken: 'refresh-viejo' });
      req.flush(sesionFake());
    });

    it('sin refresh token falla sin llamar al backend', () => {
      const { auth, http } = crear();
      let fallo = false;

      auth.refrescar().subscribe({ error: () => (fallo = true) });

      expect(fallo).toBe(true);
      http.expectNone(`${api}/auth/refresh`);
    });

    it('si otra pestaña ya rotó el token, reintenta con el nuevo en vez de fallar', () => {
      guardarSesionFake([], 'jwt-viejo', 'refresh-viejo');
      const { auth, http } = crear();
      let token = '';

      auth.refrescar().subscribe((r) => (token = r.token));
      const primera = http.expectOne(`${api}/auth/refresh`);
      // La otra pestaña guardó su sesión nueva mientras esta petición volaba.
      localStorage.setItem('mm_refresh', 'refresh-de-otra-pestana');
      primera.flush({ error: 'Sesión no válida' }, { status: 401, statusText: 'Unauthorized' });

      const segunda = http.expectOne(`${api}/auth/refresh`);
      expect(segunda.request.body).toEqual({ refreshToken: 'refresh-de-otra-pestana' });
      segunda.flush(sesionFake([], 'jwt-3', 'refresh-3'));

      expect(token).toBe('jwt-3');
    });

    it('si el token rechazado sigue siendo el mismo, propaga el error', () => {
      guardarSesionFake([], 'jwt-viejo', 'refresh-viejo');
      const { auth, http } = crear();
      let status = 0;

      auth.refrescar().subscribe({ error: (e) => (status = e.status) });
      http.expectOne(`${api}/auth/refresh`).flush({}, { status: 401, statusText: 'Unauthorized' });

      expect(status).toBe(401);
    });
  });

  describe('logout', () => {
    it('avisa al backend con el refresh token, limpia lo local y va al login', () => {
      guardarSesionFake(['ventas.ver'], 'jwt-viejo', 'refresh-viejo');
      const { auth, http, router } = crear();

      auth.logout();

      const req = http.expectOne(`${api}/auth/logout`);
      expect(req.request.body).toEqual({ refreshToken: 'refresh-viejo' });
      req.flush(null, { status: 204, statusText: 'No Content' });
      expect(auth.estaAutenticado()).toBe(false);
      expect(localStorage.getItem('mm_token')).toBeNull();
      expect(localStorage.getItem('mm_refresh')).toBeNull();
      expect(router.navigate).toHaveBeenCalledWith(['/auth/login']);
    });

    it('cierra la sesión local aunque el backend falle', () => {
      guardarSesionFake([], 'jwt-viejo', 'refresh-viejo');
      const { auth, http } = crear();

      auth.logout();
      http.expectOne(`${api}/auth/logout`).flush({}, { status: 500, statusText: 'Server Error' });

      expect(auth.estaAutenticado()).toBe(false);
    });
  });

  describe('cambiarPassword', () => {
    it('reemplaza la sesión por la nueva que devuelve el backend', () => {
      guardarSesionFake([], 'jwt-viejo', 'refresh-viejo', 10, { debeCambiarPassword: true });
      const { auth, http } = crear();

      auth.cambiarPassword({ passwordActual: 'a', passwordNueva: 'B' }).subscribe();
      http.expectOne(`${api}/auth/cambiar-password`).flush(sesionFake(['ventas.ver'], 'jwt-9', 'refresh-9'));

      expect(auth.debeCambiarPassword()).toBe(false);
      expect(localStorage.getItem('mm_token')).toBe('jwt-9');
    });
  });
});
