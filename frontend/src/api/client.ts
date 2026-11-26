// Thin fetch wrapper: attaches the JWT, serializes query params and turns
// RFC 7807 ProblemDetails responses into ApiError instances.

const TOKEN_KEY = 'stocksync.token';

export function getToken(): string | null {
  try {
    return localStorage.getItem(TOKEN_KEY);
  } catch {
    return null;
  }
}

export function setToken(token: string | null) {
  try {
    if (token) localStorage.setItem(TOKEN_KEY, token);
    else localStorage.removeItem(TOKEN_KEY);
  } catch {
    /* storage unavailable: session lasts until reload */
  }
}

export class ApiError extends Error {
  readonly status: number;
  readonly title?: string;
  readonly detail?: string;
  /** Field validation errors from ValidationProblemDetails, keyed by field name. */
  readonly fieldErrors: Record<string, string[]>;

  constructor(status: number, body: unknown) {
    const problem = (body && typeof body === 'object' ? body : {}) as {
      title?: string;
      detail?: string;
      errors?: Record<string, string[]>;
    };
    super(problem.detail || problem.title || defaultMessage(status));
    this.status = status;
    this.title = problem.title;
    this.detail = problem.detail;
    this.fieldErrors = problem.errors ?? {};
  }

  /** First validation message per field, with camelCase keys for Mantine forms. */
  formErrors(): Record<string, string> {
    const result: Record<string, string> = {};
    for (const [key, messages] of Object.entries(this.fieldErrors)) {
      const field = key.charAt(0).toLowerCase() + key.slice(1);
      if (messages[0]) result[field] = messages[0];
    }
    return result;
  }

  /** All messages flattened into a single readable string. */
  fullMessage(): string {
    const fields = Object.values(this.fieldErrors).flat();
    return fields.length ? fields.join(' ') : this.message;
  }
}

function defaultMessage(status: number): string {
  switch (status) {
    case 0:
      return 'No hay conexión con el servidor. Verifica que el API Gateway esté en ejecución.';
    case 401:
      return 'Tu sesión expiró o no es válida. Inicia sesión de nuevo.';
    case 403:
      return 'Tu rol no tiene permiso para esta acción.';
    case 404:
      return 'El recurso no existe o fue desactivado.';
    case 409:
      return 'La operación no es posible con el estado actual.';
    case 502:
    case 503:
      return 'El servicio no está disponible en este momento. Intenta de nuevo.';
    default:
      return `Error inesperado (${status}).`;
  }
}

type QueryValue = string | number | boolean | null | undefined;

export function toQueryString(query?: object): string {
  if (!query) return '';
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query as Record<string, QueryValue>)) {
    if (value === undefined || value === null || value === '') continue;
    params.append(key, String(value));
  }
  const text = params.toString();
  return text ? `?${text}` : '';
}

let unauthorizedHandler: (() => void) | null = null;

/** Registered by the auth provider to log out when any request returns 401. */
export function onUnauthorized(handler: (() => void) | null) {
  unauthorizedHandler = handler;
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE';
  body?: unknown;
  query?: object;
  /** Skip the global 401 handler (used by login). */
  anonymous?: boolean;
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' };
  const token = getToken();
  if (token && !options.anonymous) headers.Authorization = `Bearer ${token}`;
  if (options.body !== undefined) headers['Content-Type'] = 'application/json';

  let response: Response;
  try {
    response = await fetch(`${path}${toQueryString(options.query)}`, {
      method: options.method ?? 'GET',
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
    });
  } catch {
    throw new ApiError(0, null);
  }

  const text = await response.text();
  let data: unknown = null;
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      // Non-JSON bodies (e.g. an HTML 502 page from a proxy) fall back to the status message.
      data = null;
    }
  }

  if (!response.ok) {
    if (response.status === 401 && !options.anonymous) unauthorizedHandler?.();
    throw new ApiError(response.status, data);
  }
  return data as T;
}

export const http = {
  get: <T>(path: string, query?: object) => request<T>(path, { query }),
  post: <T>(path: string, body?: unknown) => request<T>(path, { method: 'POST', body: body ?? {} }),
  put: <T>(path: string, body?: unknown) => request<T>(path, { method: 'PUT', body }),
  del: <T = void>(path: string) => request<T>(path, { method: 'DELETE' }),
};
