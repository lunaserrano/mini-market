import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CanActivateFn, Router, UrlTree, provideRouter } from '@angular/router';
import { guardarSesionFake } from '../testing/sesion-fake';
import { AuthService } from '../services/auth.service';
import { authGuard, sesionGuard } from './auth.guard';
import { inicioGuard, permissionGuard, rutaInicial } from './permission.guard';

describe('guards de seguridad', () => {
  function ejecutar(guard: CanActivateFn): boolean | UrlTree {
    return TestBed.runInInjectionContext(() => guard({} as never, {} as never)) as boolean | UrlTree;
  }

  const ruta = (r: boolean | UrlTree): string => (r instanceof UrlTree ? TestBed.inject(Router).serializeUrl(r) : String(r));

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
  });

  describe('permissionGuard', () => {
    it('deja pasar a quien tiene el permiso', () => {
      guardarSesionFake(['usuarios.ver']);

      expect(ejecutar(permissionGuard('usuarios.ver'))).toBe(true);
    });

    it('con varios permisos basta uno', () => {
      guardarSesionFake(['roles.ver']);

      expect(ejecutar(permissionGuard('usuarios.ver', 'roles.ver'))).toBe(true);
    });

    it('manda a "acceso denegado" a quien no lo tiene', () => {
      guardarSesionFake(['ventas.ver']);

      expect(ruta(ejecutar(permissionGuard('usuarios.ver')))).toBe('/auth/access');
    });

    it('sin sesión tampoco pasa', () => {
      expect(ruta(ejecutar(permissionGuard('usuarios.ver')))).toBe('/auth/access');
    });
  });

  describe('authGuard', () => {
    it('sin sesión manda al login', () => {
      expect(ruta(ejecutar(authGuard))).toBe('/auth/login');
    });

    it('con sesión normal deja pasar', () => {
      guardarSesionFake(['ventas.ver']);

      expect(ejecutar(authGuard)).toBe(true);
    });

    it('con contraseña temporal solo permite ir a cambiarla', () => {
      guardarSesionFake(['ventas.ver'], 'jwt', 'r', 10, { debeCambiarPassword: true });

      expect(ruta(ejecutar(authGuard))).toBe('/auth/cambiar-password');
    });
  });

  describe('sesionGuard', () => {
    it('exige sesión pero permite el cambio forzado de contraseña', () => {
      expect(ruta(ejecutar(sesionGuard))).toBe('/auth/login');

      guardarSesionFake([], 'jwt', 'r', 10, { debeCambiarPassword: true });
      // El AuthService ya se creó sin sesión (los signals leen el storage al construirse): se recrea el módulo.
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });

      expect(ejecutar(sesionGuard)).toBe(true);
    });
  });

  describe('pantalla de inicio', () => {
    it('un cajero típico aterriza en el POS', () => {
      guardarSesionFake(['ventas.crear', 'ventas.ver', 'caja.operar']);

      expect(rutaInicial(TestBed.inject(AuthService))).toBe('/pos');
    });

    it('quien no puede vender aterriza en la primera pantalla que sí puede usar', () => {
      guardarSesionFake(['inventario.ver', 'auditoria.ver']);

      expect(rutaInicial(TestBed.inject(AuthService))).toBe('/inventario');
    });

    it('solo con permisos de seguridad aterriza en la sección de seguridad', () => {
      guardarSesionFake(['auditoria.ver']);

      expect(rutaInicial(TestBed.inject(AuthService))).toBe('/auditoria');
    });

    it('sin ningún permiso de pantalla va a "acceso denegado"', () => {
      guardarSesionFake(['productos.ver']); // productos.ver no habilita ninguna pantalla del menú

      expect(rutaInicial(TestBed.inject(AuthService))).toBe('/auth/access');
    });

    it('inicioGuard redirige "/" a esa pantalla', () => {
      guardarSesionFake(['ventas.crear']);

      expect(ruta(ejecutar(inicioGuard))).toBe('/pos');
    });
  });
});
