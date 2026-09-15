import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Producto, ProductoCreate, ProductoPos, ProductoUpdate, TipoPrecioCreate } from '../models/producto.models';

@Injectable({ providedIn: 'root' })
export class ProductoService {
  private readonly baseUrl = `${environment.apiUrl}/productos`;

  constructor(private readonly http: HttpClient) {}

  listar(): Observable<Producto[]> {
    return this.http.get<Producto[]>(this.baseUrl);
  }

  buscarParaPos(termino: string): Observable<ProductoPos[]> {
    return this.http.get<ProductoPos[]>(`${this.baseUrl}/buscar`, { params: { termino } });
  }

  obtener(id: number): Observable<Producto> {
    return this.http.get<Producto>(`${this.baseUrl}/${id}`);
  }

  crear(dto: ProductoCreate): Observable<number> {
    return this.http.post<number>(this.baseUrl, dto);
  }

  actualizar(id: number, dto: ProductoUpdate): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, dto);
  }

  desactivar(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  agregarTipoPrecio(productoId: number, dto: TipoPrecioCreate): Observable<number> {
    return this.http.post<number>(`${this.baseUrl}/${productoId}/tipos-precio`, dto);
  }

  eliminarTipoPrecio(productoId: number, tipoPrecioId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${productoId}/tipos-precio/${tipoPrecioId}`);
  }
}
