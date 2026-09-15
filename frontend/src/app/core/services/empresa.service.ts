import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Empresa, EmpresaUpdate } from '../models/empresa.models';

@Injectable({ providedIn: 'root' })
export class EmpresaService {
  private readonly baseUrl = `${environment.apiUrl}/empresa`;

  constructor(private readonly http: HttpClient) {}

  obtenerActual(): Observable<Empresa> {
    return this.http.get<Empresa>(`${this.baseUrl}/actual`);
  }

  actualizar(dto: EmpresaUpdate): Observable<Empresa> {
    return this.http.put<Empresa>(this.baseUrl, dto);
  }
}
