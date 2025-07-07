import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { SpinnerService } from './spinner.service';

@Component({
    selector: 'app-spinner',
    standalone: true,
    imports: [CommonModule, ProgressSpinnerModule],
    template: `
    <div *ngIf="visible" class="spinner-overlay">
      <p-progressSpinner styleClass="custom-spinner" strokeWidth="4" fill="#EEEEEE" animationDuration=".5s" aria-label="Cargando"></p-progressSpinner>
    </div>
  `,
    styles: [`
    .spinner-overlay {
      position: fixed;
      top: 0; left: 0; right: 0; bottom: 0;
      background: rgba(255,255,255,0.6);
      z-index: 9999;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .custom-spinner {
      width: 70px;
      height: 70px;
    }
  `]
})
export class AppSpinner {
    visible = false;
    constructor(private spinner: SpinnerService) {
        this.spinner.loading$.subscribe(v => this.visible = v);
    }
}
