import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-not-found',
  standalone: true,
  imports: [RouterLink, ButtonModule],
  template: `
    <div class="flex flex-col items-center justify-center min-h-screen gap-4 text-center px-4">
      <i class="pi pi-question-circle text-5xl text-surface-400"></i>
      <h1 class="text-2xl font-bold">Página no encontrada</h1>
      <a routerLink="/pos"><p-button label="Volver al inicio" icon="pi pi-home" /></a>
    </div>
  `
})
export class NotFoundComponent {}
