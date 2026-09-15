import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Caja, MovimientoCaja, MovimientoCajaCreate } from '../models/caja.models';

@Injectable({ providedIn: 'root' })
export class CajaService {
  private readonly baseUrl = `${environment.apiUrl}/caja`;

  constructor(private readonly http: HttpClient) {}

  obtenerActual(): Observable<Caja | null> {
    return this.http.get<Caja>(`${this.baseUrl}/actual`);
  }

  abrir(montoInicial: number): Observable<Caja> {
    return this.http.post<Caja>(`${this.baseUrl}/apertura`, { montoInicial });
  }

  cerrar(id: number, montoFinalDeclarado: number): Observable<Caja> {
    return this.http.post<Caja>(`${this.baseUrl}/${id}/cierre`, { montoFinalDeclarado });
  }

  listarMovimientos(id: number): Observable<MovimientoCaja[]> {
    return this.http.get<MovimientoCaja[]>(`${this.baseUrl}/${id}/movimientos`);
  }

  registrarMovimiento(id: number, dto: MovimientoCajaCreate): Observable<number> {
    return this.http.post<number>(`${this.baseUrl}/${id}/movimientos`, dto);
  }
}
