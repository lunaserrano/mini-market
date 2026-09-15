import { Injectable, computed, signal } from '@angular/core';
import { Empresa } from '../models/empresa.models';
import { EmpresaService } from './empresa.service';

/**
 * Mantiene en memoria la configuración de la empresa actual (moneda, zona horaria, etc.) para que
 * cualquier parte de la app pueda leer el símbolo de moneda vigente sin volver a pedirlo al
 * backend en cada pantalla. AppLayoutComponent la carga una vez al entrar autenticado; la pantalla
 * de Configuración la refresca en memoria al guardar cambios (sin recargar la página).
 */
@Injectable({ providedIn: 'root' })
export class ConfigService {
  private readonly empresaSignal = signal<Empresa | null>(null);

  readonly empresa = this.empresaSignal.asReadonly();
  readonly simboloMoneda = computed(() => this.empresaSignal()?.simboloMoneda ?? '$');
  readonly codigoMoneda = computed(() => this.empresaSignal()?.codigoMoneda ?? 'USD');
  readonly tasaImpuesto = computed(() => this.empresaSignal()?.tasaImpuesto ?? 0);

  constructor(private readonly empresaService: EmpresaService) {}

  cargar(): void {
    this.empresaService.obtenerActual().subscribe({
      next: (empresa) => this.empresaSignal.set(empresa),
      error: () => {
        /* si falla, los pipes/formatos siguen usando el valor por defecto ('USD'/'$') */
      }
    });
  }

  actualizarLocal(empresa: Empresa): void {
    this.empresaSignal.set(empresa);
  }
}
