import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AjusteInventarioRequest, Inventario, MovimientoInventario } from '../models/inventario.models';

@Injectable({ providedIn: 'root' })
export class InventarioService {
  private readonly baseUrl = `${environment.apiUrl}/inventario`;

  constructor(private readonly http: HttpClient) {}

  listar(sucursalId?: number, productoId?: number): Observable<Inventario[]> {
    return this.http.get<Inventario[]>(this.baseUrl, { params: this.buildParams({ sucursalId, productoId }) });
  }

  obtenerPuntual(productoId: number, sucursalId: number): Observable<Inventario> {
    return this.http.get<Inventario>(`${this.baseUrl}/${productoId}/sucursal/${sucursalId}`);
  }

  ajustar(request: AjusteInventarioRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/ajuste`, request);
  }

  listarMovimientos(productoId?: number, sucursalId?: number): Observable<MovimientoInventario[]> {
    return this.http.get<MovimientoInventario[]>(`${this.baseUrl}/movimientos`, {
      params: this.buildParams({ productoId, sucursalId })
    });
  }

  private buildParams(obj: Record<string, number | undefined>): Record<string, string> {
    const params: Record<string, string> = {};
    for (const [key, value] of Object.entries(obj)) {
      if (value !== undefined && value !== null) params[key] = String(value);
    }
    return params;
  }
}
