import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { MENU_ITEMS } from '../../layout/menu-items';
import { AuthService } from '../services/auth.service';

/** Guard por permisos: permissionGuard(PERMISOS.UsuariosVer) — pasa si el usuario tiene AL MENOS UNO. */
export function permissionGuard(...permisosRequeridos: string[]): CanActivateFn {
  return () => {
    const authService = inject(AuthService);
    const router = inject(Router);

    return authService.tienePermiso(...permisosRequeridos) ? true : router.createUrlTree(['/auth/access']);
  };
}

/** Primera pantalla del menú a la que el usuario tiene acceso (o "acceso denegado" si no tiene ninguna). */
export function rutaInicial(authService: AuthService): string {
  return MENU_ITEMS.find((item) => authService.tienePermiso(...item.permisos))?.route ?? '/auth/access';
}

/** Ruta "/": lleva a la primera pantalla permitida en vez de asumir que todos pueden usar el POS. */
export const inicioGuard: CanActivateFn = () => {
  const router = inject(Router);
  return router.createUrlTree([rutaInicial(inject(AuthService))]);
};
