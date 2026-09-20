import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Usuario, UsuarioCreate, UsuarioUpdate } from '../models/usuario.models';

@Injectable({ providedIn: 'root' })
export class UsuarioService {
  private readonly baseUrl = `${environment.apiUrl}/usuarios`;

  constructor(private readonly http: HttpClient) {}

  listar(): Observable<Usuario[]> {
    return this.http.get<Usuario[]>(this.baseUrl);
  }

  crear(dto: UsuarioCreate): Observable<number> {
    return this.http.post<number>(this.baseUrl, dto);
  }

  actualizar(id: number, dto: UsuarioUpdate): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, dto);
  }

  cambiarEstado(id: number, activo: boolean): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}/estado`, {}, { params: { activo } });
  }

  resetPassword(id: number, nuevaPassword: string, debeCambiarPassword: boolean): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/reset-password`, { nuevaPassword, debeCambiarPassword });
  }

  desbloquear(id: number): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}/desbloquear`, {});
  }

  revocarSesiones(id: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/revocar-sesiones`, {});
  }
}
