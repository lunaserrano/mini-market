import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Categoria, CategoriaCreate, Cliente, ClienteCreate, Proveedor, ProveedorCreate } from '../models/catalogo.models';

@Injectable({ providedIn: 'root' })
export class CategoriaService {
  private readonly baseUrl = `${environment.apiUrl}/categorias`;
  constructor(private readonly http: HttpClient) {}

  listar(): Observable<Categoria[]> {
    return this.http.get<Categoria[]>(this.baseUrl);
  }
  crear(dto: CategoriaCreate): Observable<number> {
    return this.http.post<number>(this.baseUrl, dto);
  }
  actualizar(id: number, dto: CategoriaCreate): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, dto);
  }
  desactivar(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}

@Injectable({ providedIn: 'root' })
export class ProveedorService {
  private readonly baseUrl = `${environment.apiUrl}/proveedores`;
  constructor(private readonly http: HttpClient) {}

  listar(): Observable<Proveedor[]> {
    return this.http.get<Proveedor[]>(this.baseUrl);
  }
  crear(dto: ProveedorCreate): Observable<number> {
    return this.http.post<number>(this.baseUrl, dto);
  }
  actualizar(id: number, dto: ProveedorCreate): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, dto);
  }
  desactivar(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}

@Injectable({ providedIn: 'root' })
export class ClienteService {
  private readonly baseUrl = `${environment.apiUrl}/clientes`;
  constructor(private readonly http: HttpClient) {}

  listar(): Observable<Cliente[]> {
    return this.http.get<Cliente[]>(this.baseUrl);
  }
  crear(dto: ClienteCreate): Observable<number> {
    return this.http.post<number>(this.baseUrl, dto);
  }
  actualizar(id: number, dto: ClienteCreate): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, dto);
  }
  desactivar(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
