import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MessageService } from 'primeng/api';
import { catchError, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { TokenStorageService } from '../services/token-storage.service';

/**
 * Traduce el { "error": "..." } que emite ExceptionHandlingMiddleware (backend) a un toast de
 * PrimeNG, y fuerza logout ante un 401 (token vencido o inválido).
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const messageService = inject(MessageService);
  const tokenStorage = inject(TokenStorageService);
  const router = inject(Router);

  // GET /api/caja/actual responde 404 cuando simplemente no hay una caja abierta todavía —
  // es un estado esperado que el componente ya maneja (muestra el botón "Abrir caja"), no un error
  // real que deba interrumpir al usuario con un toast.
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        tokenStorage.limpiar();
        router.navigate(['/auth/login']);
      } else if (!(error.status === 404 && req.url.endsWith('/caja/actual'))) {
        const mensaje = error.error?.error ?? 'Ocurrió un error inesperado. Intente nuevamente.';
        messageService.add({ severity: 'error', summary: 'Error', detail: mensaje, life: 5000 });
      }
      return throwError(() => error);
    })
  );
};
