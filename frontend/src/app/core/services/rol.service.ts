import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PermisoModulo, Rol, RolDetalle, RolInput } from '../models/seguridad.models';

@Injectable({ providedIn: 'root' })
export class RolService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/roles`;

  listar(): Observable<Rol[]> {
    return this.http.get<Rol[]>(this.baseUrl);
  }

  obtener(id: number): Observable<RolDetalle> {
    return this.http.get<RolDetalle>(`${this.baseUrl}/${id}`);
  }

  crear(dto: RolInput): Observable<number> {
    return this.http.post<number>(this.baseUrl, dto);
  }

  actualizar(id: number, dto: RolInput): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, dto);
  }

  eliminar(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Catálogo completo de permisos agrupado por módulo. */
  catalogoPermisos(): Observable<PermisoModulo[]> {
    return this.http.get<PermisoModulo[]>(`${environment.apiUrl}/permisos`);
  }
}
