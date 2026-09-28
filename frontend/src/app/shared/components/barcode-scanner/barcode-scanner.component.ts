import { Component, ElementRef, OnDestroy, ViewChild, effect, input, output, signal } from '@angular/core';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';
import { BrowserMultiFormatReader, IScannerControls } from '@zxing/browser';
import { BarcodeFormat, DecodeHintType, NotFoundException } from '@zxing/library';

/** Formatos habituales en códigos de barras de productos de retail.
 * Restringir los formatos (en vez de probar todos, incluyendo 2D) acelera
 * mucho la detección por frame. */
const FORMATOS_SOPORTADOS = [
  BarcodeFormat.EAN_13,
  BarcodeFormat.EAN_8,
  BarcodeFormat.UPC_A,
  BarcodeFormat.UPC_E,
  BarcodeFormat.CODE_128,
  BarcodeFormat.CODE_39,
  BarcodeFormat.ITF,
  BarcodeFormat.CODABAR
];

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

    const hints = new Map<DecodeHintType, unknown>([
      [DecodeHintType.POSSIBLE_FORMATS, FORMATOS_SOPORTADOS],
      [DecodeHintType.TRY_HARDER, true]
    ]);
    this.lector = new BrowserMultiFormatReader(hints, {
      delayBetweenScanAttempts: 100,
      delayBetweenScanSuccess: 300
    });
    try {
      this.controls = await this.lector.decodeFromConstraints(
        {
          video: {
            facingMode: { ideal: 'environment' },
            width: { ideal: 1920 },
            height: { ideal: 1080 },
            advanced: [{ focusMode: 'continuous' } as MediaTrackConstraintSet]
          }
        },
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
