/**
 * Política de contraseñas, espejo de PasswordPolicy.ContrasenaSegura (backend). Solo sirve para dar
 * retroalimentación mientras se escribe; el servidor es quien la impone.
 */
export interface ReglaPassword {
  texto: string;
  cumple: boolean;
}

export const LONGITUD_MINIMA_PASSWORD = 8;

export function evaluarPassword(password: string | null | undefined): ReglaPassword[] {
  const p = password ?? '';
  return [
    { texto: `Al menos ${LONGITUD_MINIMA_PASSWORD} caracteres`, cumple: p.length >= LONGITUD_MINIMA_PASSWORD },
    { texto: 'Una mayúscula', cumple: /\p{Lu}/u.test(p) },
    { texto: 'Una minúscula', cumple: /\p{Ll}/u.test(p) },
    { texto: 'Un número', cumple: /\p{Nd}/u.test(p) },
    { texto: 'Un símbolo', cumple: /[^\p{L}\p{Nd}]/u.test(p) }
  ];
}

export function passwordValida(password: string | null | undefined): boolean {
  return evaluarPassword(password).every((r) => r.cumple);
}
