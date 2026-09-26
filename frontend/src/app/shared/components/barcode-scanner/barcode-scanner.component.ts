import { Component, ElementRef, OnDestroy, ViewChild, effect, input, output, signal } from '@angular/core';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';
import { BrowserMultiFormatReader, IScannerControls } from '@zxing/browser';
import { NotFoundException } from '@zxing/library';

/** Diálogo que abre la cámara trasera del dispositivo y decodifica un código de barras en vivo,
 * usando @zxing/browser (funciona en Android e iOS, a diferencia de la API nativa BarcodeDetector). */
@Component({
  selector: 'app-barcode-scanner',
  standalone: true,
  imports: [DialogModule],
  templateUrl: './barcode-scanner.component.html'
})
export class BarcodeScannerComponent implements OnDestroy {
  readonly visible = input.required<boolean>();
  readonly visibleChange = output<boolean>();
  readonly codigoDetectado = output<string>();

  @ViewChild('video') private videoRef?: ElementRef<HTMLVideoElement>;

  readonly error = signal<string | null>(null);

  private lector: BrowserMultiFormatReader | null = null;
  private controls: IScannerControls | null = null;

  constructor(private readonly messageService: MessageService) {
    effect(() => {
      if (this.visible()) this.iniciar();
      else this.detener();
    });
  }

  private async iniciar(): Promise<void> {
    this.error.set(null);
    const videoEl = this.videoRef?.nativeElement;
    if (!videoEl) return;

    this.lector = new BrowserMultiFormatReader();
    try {
      this.controls = await this.lector.decodeFromConstraints(
        { video: { facingMode: { ideal: 'environment' } } },
        videoEl,
        (resultado, err) => {
          if (resultado) {
            this.controls?.stop();
            this.codigoDetectado.emit(resultado.getText());
            this.cerrar();
            return;
          }
          if (err && !(err instanceof NotFoundException)) {
            this.manejarError(err);
          }
        }
      );
    } catch (err) {
      this.manejarError(err);
    }
  }

  private manejarError(err: unknown): void {
    const nombre = err instanceof Error ? err.name : '';
    const mensaje =
      nombre === 'NotAllowedError'
        ? 'Permiso de cámara denegado. Habilítalo en la configuración del navegador.'
        : nombre === 'NotFoundError'
          ? 'No se encontró una cámara en este dispositivo.'
          : nombre === 'OverconstrainedError' || nombre === 'NotReadableError'
            ? 'No se pudo iniciar la cámara. Puede estar en uso por otra aplicación.'
            : 'Ocurrió un error al escanear. Intenta de nuevo.';

    this.error.set(mensaje);
    this.messageService.add({ severity: 'error', summary: 'No se pudo acceder a la cámara', detail: mensaje });
    this.cerrar();
  }

  private detener(): void {
    this.controls?.stop();
    this.controls = null;
    this.lector = null;
  }

  cerrar(): void {
    this.visibleChange.emit(false);
  }

  ngOnDestroy(): void {
    this.detener();
  }
}
