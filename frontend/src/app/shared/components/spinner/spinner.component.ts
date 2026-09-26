import { Component, input } from '@angular/core';

const TAMANOS = {
  sm: { anillo: 'w-6 h-6 border-2', icono: 'text-xs' },
  md: { anillo: 'w-10 h-10 border-[3px]', icono: 'text-base' },
  lg: { anillo: 'w-16 h-16 border-4', icono: 'text-2xl' }
};

/**
 * Espiner temático del rubro (mini market): un carrito de compras dentro de un anillo giratorio.
 * Un solo componente reutilizable para toda acción de carga (overlay global, tablas, botones, etc.).
 */
@Component({
  selector: 'app-spinner',
  standalone: true,
  template: `
    <div class="flex flex-col items-center gap-2">
      <div class="relative inline-flex items-center justify-center {{ anillo() }} rounded-full border-primary-200 dark:border-primary-900/50 border-t-primary animate-spin">
        <i class="pi pi-shopping-cart text-primary {{ icono() }}" style="animation: mm-spinner-bounce 1s ease-in-out infinite"></i>
      </div>
      @if (label()) {
        <span class="text-sm text-surface-500 dark:text-surface-400">{{ label() }}</span>
      }
    </div>
  `,
  styles: [
    `
      @keyframes mm-spinner-bounce {
        0%,
        100% {
          transform: translateY(0);
        }
        50% {
          transform: translateY(-2px);
        }
      }
    `
  ]
})
export class SpinnerComponent {
  readonly size = input<'sm' | 'md' | 'lg'>('md');
  readonly label = input<string | null>(null);

  readonly anillo = () => TAMANOS[this.size()].anillo;
  readonly icono = () => TAMANOS[this.size()].icono;
}
