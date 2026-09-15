import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { MessageService } from 'primeng/api';
import { ConfigService } from '../../core/services/config.service';
import { EmpresaService } from '../../core/services/empresa.service';
import { Empresa, EmpresaUpdate, MONEDAS_SUGERIDAS } from '../../core/models/empresa.models';

@Component({
  selector: 'app-configuracion',
  standalone: true,
  imports: [FormsModule, ButtonModule, InputTextModule, SelectModule],
  templateUrl: './configuracion.component.html'
})
export class ConfiguracionComponent implements OnInit {
  readonly cargando = signal(false);
  readonly guardando = signal(false);
  readonly monedasSugeridas = MONEDAS_SUGERIDAS;

  formulario: EmpresaUpdate = {
    nombre: '',
    razonSocial: '',
    identificacionFiscal: '',
    zonaHoraria: 'America/El_Salvador',
    codigoMoneda: 'USD',
    simboloMoneda: '$',
    tasaImpuesto: 13
  };

  constructor(
    private readonly empresaService: EmpresaService,
    private readonly configService: ConfigService,
    private readonly messageService: MessageService
  ) {}

  ngOnInit(): void {
    this.cargando.set(true);
    this.empresaService.obtenerActual().subscribe({
      next: (empresa) => {
        this.formulario = {
          nombre: empresa.nombre,
          razonSocial: empresa.razonSocial,
          identificacionFiscal: empresa.identificacionFiscal,
          zonaHoraria: empresa.zonaHoraria,
          codigoMoneda: empresa.codigoMoneda,
          simboloMoneda: empresa.simboloMoneda,
          tasaImpuesto: empresa.tasaImpuesto
        };
        this.cargando.set(false);
      },
      error: () => this.cargando.set(false)
    });
  }

  /** Al elegir una moneda sugerida, autocompleta el símbolo (el usuario puede seguir editándolo). */
  onMonedaSeleccionada(codigo: string): void {
    const sugerida = this.monedasSugeridas.find((m) => m.codigo === codigo);
    if (sugerida) this.formulario.simboloMoneda = sugerida.simbolo;
  }

  guardar(): void {
    if (!this.formulario.nombre || !this.formulario.codigoMoneda || !this.formulario.simboloMoneda) return;

    this.guardando.set(true);
    this.empresaService.actualizar(this.formulario).subscribe({
      next: (empresa: Empresa) => {
        this.configService.actualizarLocal(empresa);
        this.guardando.set(false);
        this.messageService.add({ severity: 'success', summary: 'Configuración guardada', detail: 'Los cambios ya se reflejan en toda la app.' });
      },
      error: () => this.guardando.set(false)
    });
  }
}
