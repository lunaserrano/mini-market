import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AbonoCreate, Credito, CreditoResumen, EstadoCredito } from '../models/credito.models';

@Injectable({ providedIn: 'root' })
export class CreditoService {
  private readonly baseUrl = `${environment.apiUrl}/creditos`;

  constructor(private readonly http: HttpClient) {}

  listar(filtros: { estado?: EstadoCredito | null; clienteId?: number | null } = {}): Observable<CreditoResumen[]> {
    const params: Record<string, string | number> = {};
    if (filtros.estado) params['estado'] = filtros.estado;
    if (filtros.clienteId) params['clienteId'] = filtros.clienteId;
    return this.http.get<CreditoResumen[]>(this.baseUrl, { params });
  }

  obtener(id: number): Observable<Credito> {
    return this.http.get<Credito>(`${this.baseUrl}/${id}`);
  }

  /** Registra un abono y devuelve el crédito ya actualizado (con su nuevo saldo y estado). */
  abonar(id: number, dto: AbonoCreate): Observable<Credito> {
    return this.http.post<Credito>(`${this.baseUrl}/${id}/abonos`, dto);
  }
}
