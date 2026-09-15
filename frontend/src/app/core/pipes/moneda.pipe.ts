import { Pipe, PipeTransform, inject } from '@angular/core';
import { ConfigService } from '../services/config.service';

/**
 * Formatea un monto con el símbolo de moneda configurado para la empresa (ConfigService), en vez
 * de un símbolo fijo ("Q", "$", etc.) escrito en cada plantilla. Uso: {{ monto | moneda }}.
 */
@Pipe({ name: 'moneda', standalone: true, pure: false })
export class MonedaPipe implements PipeTransform {
  private readonly config = inject(ConfigService);

  transform(valor: number | null | undefined): string {
    const monto = (valor ?? 0).toFixed(2);
    return `${this.config.simboloMoneda()}${monto}`;
  }
}
