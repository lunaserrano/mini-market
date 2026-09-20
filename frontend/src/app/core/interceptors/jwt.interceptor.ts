import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, of, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { TokenStorageService } from '../services/token-storage.service';

/** login y refresh no llevan Bearer ni disparan renovación: son las llamadas que la crean. */
export function esEndpointPublicoDeAuth(url: string): boolean {
  return url.endsWith('/auth/login') || url.endsWith('/auth/refresh');
}

const conToken = (req: HttpRequest<unknown>, token: string) => req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });

/**
 * Agrega "Authorization: Bearer <token>" y mantiene viva la sesión:
 *  - si el access token está por vencer, lo renueva antes de enviar;
 *  - si el backend responde 401, renueva una vez con el refresh token y reintenta la petición;
 *  - si no se puede renovar, cierra la sesión y manda al login.
 */
export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  if (esEndpointPublicoDeAuth(req.url)) return next(req);

  const auth = inject(AuthService);
  const storage = inject(TokenStorageService);
  const token = storage.obtenerToken();
  if (!token) return next(req);

  const tokenVigente$ =
    storage.tokenPorVencer() && storage.obtenerRefreshToken()
      ? auth.refrescar().pipe(
          switchMap((r) => of(r.token)),
          catchError(() => of(token)) // si falla, se intenta con el que hay: el 401 posterior cerrará la sesión
        )
      : of(token);

  return tokenVigente$.pipe(
    switchMap((t) =>
      next(conToken(req, t)).pipe(
        catchError((error: unknown) => {
          if (!(error instanceof HttpErrorResponse) || error.status !== 401) return throwError(() => error);

          if (!storage.obtenerRefreshToken()) {
            auth.cerrarSesionLocal();
            return throwError(() => error);
          }

          return auth.refrescar().pipe(
            // Solo un fallo al RENOVAR cierra la sesión; un error de la petición reintentada se propaga tal cual.
            catchError(() => {
              auth.cerrarSesionLocal();
              return throwError(() => error);
            }),
            switchMap((r) => next(conToken(req, r.token)))
          );
        })
      )
    )
  );
};
