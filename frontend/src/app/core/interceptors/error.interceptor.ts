import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MessageService } from 'primeng/api';
import { catchError, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { RUTA_ACTIVIDAD } from '../services/actividad.service';
import { esEndpointPublicoDeAuth } from './jwt.interceptor';

/**
 * Traduce el { "error": "..." } que emite ExceptionHandlingMiddleware (backend) a un toast de PrimeNG.
 * No intervienen:
 *  - 401: lo gestiona jwtInterceptor (renovar sesión o cerrarla);
 *  - login/refresh: el formulario de login muestra su propio mensaje;
 *  - reporte de actividad (auditoría de clics): corre en segundo plano y no debe molestar al usuario.
 * Un 403 "cambio_password_requerido" lleva a la pantalla de cambio de contraseña en vez de mostrar un error.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const messageService = inject(MessageService);
  const router = inject(Router);

  // GET /api/caja/actual responde 404 cuando simplemente no hay una caja abierta todavía —
  // es un estado esperado que el componente ya maneja (muestra el botón "Abrir caja"), no un error
  // real que deba interrumpir al usuario con un toast.
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 || esEndpointPublicoDeAuth(req.url) || req.url.endsWith(RUTA_ACTIVIDAD)) return throwError(() => error);

      if (error.status === 403 && error.error?.codigo === 'cambio_password_requerido') {
        router.navigate(['/auth/cambiar-password']);
        return throwError(() => error);
      }

      if (!(error.status === 404 && req.url.endsWith('/caja/actual'))) {
        const mensaje = error.error?.error ?? 'Ocurrió un error inesperado. Intente nuevamente.';
        messageService.add({ severity: 'error', summary: 'Error', detail: mensaje, life: 5000 });
      }
      return throwError(() => error);
    })
  );
};
