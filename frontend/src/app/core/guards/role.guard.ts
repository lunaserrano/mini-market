import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/** Factory de guard por rol: roleGuard(['admin','supervisor']) — reemplaza el roleGuard mock anterior. */
export function roleGuard(rolesPermitidos: string[]): CanActivateFn {
  return () => {
    const authService = inject(AuthService);
    const router = inject(Router);

    if (authService.tienePermiso(...rolesPermitidos)) return true;

    router.navigate(['/auth/access']);
    return false;
  };
}
