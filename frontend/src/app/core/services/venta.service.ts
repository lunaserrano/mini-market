import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Venta, VentaCreate, VentaResumen } from '../models/venta.models';

@Injectable({ providedIn: 'root' })
export class VentaService {
  private readonly baseUrl = `${environment.apiUrl}/ventas`;

  constructor(private readonly http: HttpClient) {}

  crear(dto: VentaCreate): Observable<Venta> {
    return this.http.post<Venta>(this.baseUrl, dto);
  }

  listar(): Observable<VentaResumen[]> {
    return this.http.get<VentaResumen[]>(this.baseUrl);
  }

  obtener(id: number): Observable<Venta> {
    return this.http.get<Venta>(`${this.baseUrl}/${id}`);
  }

  anular(id: number, motivo: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/anular`, { motivo });
  }
}
