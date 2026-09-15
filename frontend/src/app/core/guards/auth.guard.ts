import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/** Reemplaza el guard mock anterior: valida un JWT real (presencia del token), no un JSON en localStorage. */
export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.estaAutenticado()) return true;

  router.navigate(['/auth/login']);
  return false;
};
