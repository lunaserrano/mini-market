import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuditoriaFiltro, EventoSeguridad, PaginaResultado } from '../models/seguridad.models';

@Injectable({ providedIn: 'root' })
export class AuditoriaService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/auditoria`;

  listar(filtro: AuditoriaFiltro): Observable<PaginaResultado<EventoSeguridad>> {
    let params = new HttpParams().set('pagina', filtro.pagina).set('tamanoPagina', filtro.tamanoPagina);
    if (filtro.desde) params = params.set('desde', filtro.desde);
    if (filtro.hasta) params = params.set('hasta', filtro.hasta);
    if (filtro.usuarioId) params = params.set('usuarioId', filtro.usuarioId);
    if (filtro.tipo) params = params.set('tipo', filtro.tipo);
    if (filtro.origen) params = params.set('origen', filtro.origen);
    return this.http.get<PaginaResultado<EventoSeguridad>>(this.baseUrl, { params });
  }

  tipos(): Observable<string[]> {
    return this.http.get<string[]>(`${this.baseUrl}/tipos`);
  }
}
