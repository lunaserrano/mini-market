import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-access-denied',
  standalone: true,
  imports: [RouterLink, ButtonModule],
  template: `
    <div class="flex flex-col items-center justify-center min-h-screen gap-4 text-center px-4">
      <i class="pi pi-lock text-5xl text-red-500"></i>
      <h1 class="text-2xl font-bold">Acceso denegado</h1>
      <p class="text-surface-500">No tienes permiso para ver esta página.</p>
      <a routerLink="/pos"><p-button label="Volver al inicio" icon="pi pi-home" /></a>
    </div>
  `
})
export class AccessDeniedComponent {}
