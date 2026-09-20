import { HttpClient } from '@angular/common/http';
import { DestroyRef, Injectable, NgZone, inject } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { environment } from '../../../environments/environment';
import { EventoCliente } from '../models/seguridad.models';
import { TokenStorageService } from './token-storage.service';

/** El interceptor de errores no debe mostrar toasts por fallos de este endpoint: el usuario no lo pidió. */
export const RUTA_ACTIVIDAD = '/auditoria/cliente';

const INTERVALO_ENVIO_MS = 2_000;
const LOTE_MAXIMO = 100; // el backend rechaza lotes mayores
const ENVIAR_AL_LLEGAR_A = 20;
const PENDIENTES_MAXIMOS = 1_000; // tope si el backend no responde: se descartan los más antiguos
const MAX_TEXTO = 80;

const INTERACTIVOS = 'button, a, input, select, textarea, label, summary, [role="button"], [role="menuitem"], [role="tab"], [role="option"], [role="checkbox"], [role="switch"]';
const CAMPOS = new Set(['INPUT', 'SELECT', 'TEXTAREA']);
const NOMBRES: Record<string, string> = {
  BUTTON: 'botón',
  A: 'enlace',
  INPUT: 'campo',
  SELECT: 'lista',
  TEXTAREA: 'campo de texto',
  LABEL: 'etiqueta'
};

export interface DescriptorClic {
  detalle: string;
  datos: Record<string, string | null>;
}

/**
 * Describe el elemento sobre el que se hizo clic. NUNCA incluye el valor ni el texto de un campo de formulario
 * (podría ser una contraseña o un dato personal): de los campos solo se conoce su id, nombre, tipo y etiqueta accesible.
 */
export function describirClic(objetivo: Element): DescriptorClic {
  const el = objetivo.closest(INTERACTIVOS) ?? objetivo;
  const tag = el.tagName;
  const esCampo = CAMPOS.has(tag);

  const texto = esCampo
    ? limpiar(el.getAttribute('aria-label') ?? el.getAttribute('placeholder'))
    : limpiar(el.getAttribute('aria-label') ?? el.getAttribute('title') ?? (el as HTMLElement).innerText ?? el.textContent);
  const icono = Array.from(el.querySelectorAll('[class*="pi-"]'))
    .concat(el)
    .flatMap((n) => Array.from(n.classList))
    .find((c) => c.startsWith('pi-'));

  const enlace = tag === 'A' ? ((el as HTMLAnchorElement).getAttribute('href') ?? null) : null;
  const nombre = NOMBRES[tag] ?? tag.toLowerCase();
  const etiqueta = texto ?? (icono ? `icono ${icono}` : el.id || el.getAttribute('name'));

  return {
    detalle: etiqueta ? `Clic en ${nombre} "${etiqueta}"` : `Clic en ${nombre}`,
    datos: {
      tag,
      id: el.id || null,
      nombre: el.getAttribute('name'),
      tipo: el.getAttribute('type'),
      rol: el.getAttribute('role'),
      texto,
      icono: icono ?? null,
      enlace: enlace?.split('?')[0] ?? null,
      // Atributo opcional para nombrar acciones en la plantilla: <button data-audit="Anular venta">
      accion: el.closest('[data-audit]')?.getAttribute('data-audit') ?? null
    }
  };
}

function limpiar(valor: string | null | undefined): string | null {
  const t = valor?.replace(/\s+/g, ' ').trim();
  if (!t) return null;
  return t.length > MAX_TEXTO ? `${t.slice(0, MAX_TEXTO)}…` : t;
}

/**
 * Registra en la auditoría todo lo que el usuario hace en la interfaz: cada clic y cada cambio de pantalla.
 * Los eventos se acumulan y se envían por lotes al backend (que les pone empresa, usuario, IP y hora fiable).
 * Solo se registra con sesión iniciada. Al cerrar o ocultar la pestaña se envía lo pendiente con fetch keepalive.
 */
@Injectable({ providedIn: 'root' })
export class ActividadService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly storage = inject(TokenStorageService);
  private readonly zone = inject(NgZone);
  private readonly destroyRef = inject(DestroyRef);
  private readonly url = `${environment.apiUrl}${RUTA_ACTIVIDAD}`;

  private pendientes: EventoCliente[] = [];
  private temporizador: ReturnType<typeof setTimeout> | null = null;
  private iniciado = false;

  iniciar(): void {
    if (this.iniciado || typeof document === 'undefined') return;
    this.iniciado = true;

    // El AbortController quita los listeners globales si el servicio se destruye (tests, recarga en caliente).
    const cancelar = new AbortController();
    this.destroyRef.onDestroy(() => {
      cancelar.abort();
      if (this.temporizador) clearTimeout(this.temporizador);
    });

    // Fuera de la zona: un listener global por clic no debe disparar detección de cambios de toda la app.
    this.zone.runOutsideAngular(() => {
      // Fase de captura: se registra el clic aunque algún componente detenga la propagación.
      document.addEventListener('click', (e) => this.alHacerClic(e), { capture: true, signal: cancelar.signal });
      document.addEventListener(
        'visibilitychange',
        () => {
          if (document.visibilityState === 'hidden') this.enviarAlSalir();
        },
        { signal: cancelar.signal }
      );
      window.addEventListener('pagehide', () => this.enviarAlSalir(), { signal: cancelar.signal });
    });

    this.router.events.pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd)).subscribe((e) =>
      this.registrar({ tipo: 'NAVEGACION_UI', ruta: e.urlAfterRedirects, detalle: `Navegó a ${e.urlAfterRedirects.split('?')[0]}`, datos: null })
    );
  }

  private alHacerClic(evento: MouseEvent): void {
    if (!(evento.target instanceof Element)) return;
    const { detalle, datos } = describirClic(evento.target);
    this.registrar({ tipo: 'CLIC_UI', ruta: this.router.url, detalle, datos });
  }

  private registrar(evento: Omit<EventoCliente, 'fechaUtc'>): void {
    if (!this.storage.obtenerToken()) return; // sin sesión no hay a quién atribuir la acción

    this.pendientes.push({ ...evento, fechaUtc: new Date().toISOString() });
    if (this.pendientes.length > PENDIENTES_MAXIMOS) this.pendientes.splice(0, this.pendientes.length - PENDIENTES_MAXIMOS);

    if (this.pendientes.length >= ENVIAR_AL_LLEGAR_A) this.enviar();
    else this.temporizador ??= setTimeout(() => this.enviar(), INTERVALO_ENVIO_MS);
  }

  private enviar(): void {
    if (this.temporizador) {
      clearTimeout(this.temporizador);
      this.temporizador = null;
    }
    if (this.pendientes.length === 0) return;

    const lote = this.pendientes.splice(0, LOTE_MAXIMO);
    this.http.post(this.url, lote).subscribe({
      // Si falla (red, backend caído) el lote vuelve al principio de la cola y se reintenta con el siguiente envío.
      error: () => {
        this.pendientes.unshift(...lote);
        if (this.pendientes.length > PENDIENTES_MAXIMOS) this.pendientes.splice(0, this.pendientes.length - PENDIENTES_MAXIMOS);
        this.temporizador ??= setTimeout(() => this.enviar(), INTERVALO_ENVIO_MS * 5);
      }
    });
    if (this.pendientes.length > 0) this.temporizador ??= setTimeout(() => this.enviar(), 0);
  }

  /** Al cerrar la pestaña HttpClient no alcanza a enviar: fetch con keepalive sí sobrevive a la descarga de la página. */
  private enviarAlSalir(): void {
    const token = this.storage.obtenerToken();
    if (!token || this.storage.tokenPorVencer() || this.pendientes.length === 0) return;
    if (this.temporizador) {
      clearTimeout(this.temporizador);
      this.temporizador = null;
    }

    const lote = this.pendientes.splice(0, LOTE_MAXIMO);
    void fetch(this.url, {
      method: 'POST',
      keepalive: true,
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
      body: JSON.stringify(lote)
    }).catch(() => undefined);
  }
}
