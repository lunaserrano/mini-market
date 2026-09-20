import { Directive, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';
import { AuthService } from '../services/auth.service';

/**
 * Muestra el elemento solo si el usuario tiene AL MENOS UNO de los permisos indicados:
 *   <p-button *appHasPermission="'usuarios.crear'" ... />
 *   <p-button *appHasPermission="['usuarios.editar', 'usuarios.desbloquear']" ... />
 * Es solo presentación; el backend valida el permiso de cada operación.
 */
@Directive({ selector: '[appHasPermission]', standalone: true })
export class HasPermissionDirective {
  private readonly authService = inject(AuthService);
  private readonly plantilla = inject(TemplateRef<unknown>);
  private readonly contenedor = inject(ViewContainerRef);

  readonly appHasPermission = input.required<string | readonly string[]>();

  constructor() {
    effect(() => {
      const requeridos = this.appHasPermission();
      const lista = typeof requeridos === 'string' ? [requeridos] : [...requeridos];
      this.contenedor.clear();
      if (this.authService.tienePermiso(...lista)) this.contenedor.createEmbeddedView(this.plantilla);
    });
  }
}
