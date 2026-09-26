/** Detecta si el dispositivo tiene pantalla táctil (uso principal: mostrar/ocultar el botón de
 * escaneo por cámara — en escritorio se usa el lector láser físico como teclado, no tiene sentido
 * mostrar la cámara ahí). Basado en capacidades del dispositivo, no en sniffing de user-agent. */
export function esDispositivoTactil(): boolean {
  if (typeof window === 'undefined') return false;
  const punteroGrueso = typeof window.matchMedia === 'function' && window.matchMedia('(pointer: coarse)').matches;
  const tieneTouch = 'ontouchstart' in window || navigator.maxTouchPoints > 0;
  return punteroGrueso || tieneTouch;
}
