import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TableModule } from 'primeng/table';

@Component({
    selector: 'app-historial-inventario',
    standalone: true,
    imports: [CommonModule, TableModule],
    template: `
    <div class="p-4">
      <h3 class="text-lg font-bold mb-2">Historial de movimientos para: {{ producto }}</h3>
      <p-table [value]="movimientos" class="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th>Tipo</th>
            <th>Cantidad</th>
            <th>Motivo</th>
            <th>Fecha</th>
            <th>Usuario</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-mov>
          <tr>
            <td>{{ mov.tipo | titlecase }}</td>
            <td>{{ mov.cantidad }}</td>
            <td>{{ mov.motivo }}</td>
            <td>{{ mov.fecha | date:'short' }}</td>
            <td>{{ mov.usuario }}</td>
          </tr>
        </ng-template>
      </p-table>
    </div>
  `
})
export class HistorialInventarioComponent {
    @Input() producto: string = '';
    @Input() movimientos: any[] = [];
}
