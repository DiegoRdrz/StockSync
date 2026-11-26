// Client-side mirrors of backend rules, so users see errors before submitting.

/** Identity AuthValidator: 8–100 chars, at least one letter and one digit. */
export function passwordError(value: string): string | null {
  if (value.length < 8) return 'Usa al menos 8 caracteres';
  if (value.length > 100) return 'Usa máximo 100 caracteres';
  if (!/\d/.test(value)) return 'Incluye al menos un número';
  if (!/\p{L}/u.test(value)) return 'Incluye al menos una letra';
  return null;
}

export const emailError = (value: string) => (/^\S+@\S+\.\S+$/.test(value.trim()) ? null : 'Escribe un correo válido');
export const requiredError = (label: string) => (value: string) => (value?.trim() ? null : `${label} es obligatorio`);
