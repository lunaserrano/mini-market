import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Exige sesión iniciada. Si el usuario debe cambiar su contraseña (creada o restablecida por un admin),
 * solo puede ir a esa pantalla: el backend además rechaza con 403 todo lo demás.
 */
export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.estaAutenticado()) return router.createUrlTree(['/auth/login']);
  if (authService.debeCambiarPassword()) return router.createUrlTree(['/auth/cambiar-password']);
  return true;
};

/** Solo exige sesión iniciada (para la pantalla de cambio de contraseña, que también es la del cambio forzado). */
export const sesionGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.estaAutenticado() ? true : router.createUrlTree(['/auth/login']);
};
