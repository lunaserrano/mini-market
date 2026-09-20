import { Directive, DoCheck, ElementRef, HostListener, inject } from '@angular/core';

/**
 * Muestra siempre dos decimales (12 -> 12.00) en un <input type="number"> de montos mientras no
 * tiene el foco; al escribir no toca el valor, para no pelear con el usuario. No cambia el modelo:
 * solo el texto visible.
 *   <input type="number" appDosDecimales [(ngModel)]="monto" />
 */
@Directive({ selector: 'input[type=number][appDosDecimales]', standalone: true })
export class DosDecimalesDirective implements DoCheck {
  private readonly el = inject<ElementRef<HTMLInputElement>>(ElementRef).nativeElement;

  ngDoCheck(): void {
    if (document.activeElement !== this.el) this.formatear();
  }

  @HostListener('blur')
  formatear(): void {
    if (this.el.value === '') return;
    const numero = Number(this.el.value);
    if (!Number.isFinite(numero)) return;
    const texto = numero.toFixed(2);
    if (this.el.value !== texto) this.el.value = texto;
  }
}
