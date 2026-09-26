import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { finalize } from 'rxjs';
import { RUTA_ACTIVIDAD } from '../services/actividad.service';
import { LoadingService } from '../services/loading.service';

/**
 * Muestra el espiner global mientras haya al menos una petición en curso. No cuenta el reporte de
 * actividad (auditoría de clics): corre cada pocos segundos en segundo plano y no debe hacer
 * parpadear el espiner en cada clic del usuario.
 */
export const loadingInterceptor: HttpInterceptorFn = (req, next) => {
  if (req.url.endsWith(RUTA_ACTIVIDAD)) return next(req);

  const loadingService = inject(LoadingService);
  loadingService.iniciar();
  return next(req).pipe(finalize(() => loadingService.finalizar()));
};
