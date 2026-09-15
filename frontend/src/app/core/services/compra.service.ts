import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Compra, CompraCreate, CompraResumen } from '../models/compra.models';

@Injectable({ providedIn: 'root' })
export class CompraService {
  private readonly baseUrl = `${environment.apiUrl}/compras`;

  constructor(private readonly http: HttpClient) {}

  listar(): Observable<CompraResumen[]> {
    return this.http.get<CompraResumen[]>(this.baseUrl);
  }

  obtener(id: number): Observable<Compra> {
    return this.http.get<Compra>(`${this.baseUrl}/${id}`);
  }

  crear(dto: CompraCreate): Observable<number> {
    return this.http.post<number>(this.baseUrl, dto);
  }

  anular(id: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/anular`, {});
  }
}
