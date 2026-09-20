import { Component } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { HasPermissionDirective } from '../directives/has-permission.directive';
import { MENU_ITEMS } from '../../layout/menu-items';
import { guardarSesionFake } from '../testing/sesion-fake';
import { evaluarPassword, passwordValida } from './password-policy';
import { PERMISOS } from './permisos';

describe('política de contraseñas (cliente)', () => {
  it.each(['Abcdef1!', 'Una-Clave-Larga_2026', 'ñandú.Ñ4ndu'])('acepta "%s"', (clave) => {
    expect(passwordValida(clave)).toBe(true);
  });

  it.each([
    ['', 'todo'],
    ['Ab1!', 'longitud'],
    ['abcdefg1!', 'sin mayúscula'],
    ['ABCDEFG1!', 'sin minúscula'],
    ['Abcdefgh!', 'sin número'],
    ['Abcdefg12', 'sin símbolo']
  ])('rechaza "%s" (%s)', (clave) => {
    expect(passwordValida(clave)).toBe(false);
  });

  it('evalúa cada regla por separado para poder mostrarlas', () => {
    const reglas = evaluarPassword('abc');

    expect(reglas).toHaveLength(5);
    expect(reglas.filter((r) => r.cumple)).toHaveLength(1); // solo la minúscula
  });

  it('tolera null/undefined', () => {
    expect(passwordValida(null)).toBe(false);
    expect(passwordValida(undefined)).toBe(false);
  });
});

describe('catálogo de permisos y menú', () => {
  it('los códigos son únicos y con formato modulo.accion', () => {
    const codigos = Object.values(PERMISOS);

    expect(new Set(codigos).size).toBe(codigos.length);
    codigos.forEach((c) => expect(c).toMatch(/^[a-z]+\.[a-z_]+$/));
  });

  it('todo permiso del menú existe en el catálogo', () => {
    const catalogo = new Set<string>(Object.values(PERMISOS));

    MENU_ITEMS.flatMap((i) => i.permisos).forEach((p) => expect(catalogo.has(p)).toBe(true));
  });

  it('las rutas del menú son únicas y cada entrada exige al menos un permiso', () => {
    const rutas = MENU_ITEMS.map((i) => i.route);

    expect(new Set(rutas).size).toBe(rutas.length);
    MENU_ITEMS.forEach((i) => expect(i.permisos.length).toBeGreaterThan(0));
  });
});

@Component({
  standalone: true,
  imports: [HasPermissionDirective],
  template: `
    <span id="uno" *appHasPermission="'usuarios.crear'">crear</span>
    <span id="cualquiera" *appHasPermission="['usuarios.editar', 'roles.ver']">editar-o-roles</span>
    <span id="otro" *appHasPermission="'auditoria.ver'">auditoria</span>
  `
})
class HostComponent {}

describe('HasPermissionDirective', () => {
  function render(permisos: string[]): HTMLElement {
    localStorage.clear();
    guardarSesionFake(permisos);
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('muestra solo los elementos cuyo permiso tiene el usuario', () => {
    const el = render([PERMISOS.UsuariosCrear]);

    expect(el.querySelector('#uno')).not.toBeNull();
    expect(el.querySelector('#cualquiera')).toBeNull();
    expect(el.querySelector('#otro')).toBeNull();
  });

  it('con una lista basta un permiso', () => {
    const el = render([PERMISOS.RolesVer]);

    expect(el.querySelector('#cualquiera')).not.toBeNull();
    expect(el.querySelector('#uno')).toBeNull();
  });

  it('sin permisos no muestra nada', () => {
    const el = render([]);

    expect(el.querySelector('#uno')).toBeNull();
    expect(el.querySelector('#cualquiera')).toBeNull();
    expect(el.querySelector('#otro')).toBeNull();
  });
});
