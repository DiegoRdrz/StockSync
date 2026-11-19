import { toPublicInventoryPath } from '../../application/routing/inventory-routes.js';

function cleanHeaders<T extends Record<string, unknown>>(original: T): T {
  const headers = { ...original };
  const connectionTokens = String(original.connection || '').toLowerCase().split(',').map(value => value.trim());
  for (const name of ['connection', 'keep-alive', 'proxy-authenticate', 'proxy-authorization',
    'te', 'trailer', 'transfer-encoding', 'upgrade', ...connectionTokens]) delete headers[name];
  return headers;
}

export function prepareRequestHeaders<T extends Record<string, unknown>>(original: T, isSwagger: boolean): T {
  const headers = cleanHeaders(original);
  for (const name of Object.keys(headers)) {
    if (name === 'forwarded' || name.startsWith('x-forwarded-')) delete headers[name];
  }
  if (isSwagger) {
    Object.assign(headers, { 'accept-encoding': 'identity' });
    delete headers['if-none-match'];
    delete headers['if-modified-since'];
  }
  return headers;
}

export function toPublicLocation(value: string, target: URL): string {
  try {
    const location = new URL(value, target);
    if (location.origin !== target.origin) return value;
    return toPublicInventoryPath(location.pathname) + location.search + location.hash;
  } catch {
    return value;
  }
}

export function extractTenantFromAuthorization(authHeader?: string): string | null {
  if (!authHeader || !authHeader.toLowerCase().startsWith('bearer ')) return null;
  const token = authHeader.slice(7).trim();
  const parts = token.split('.');
  if (parts.length !== 3) return null;
  try {
    const payloadJson = Buffer.from(parts[1], 'base64url').toString('utf8');
    const payload = JSON.parse(payloadJson);
    const tenantId = payload.tenant_id || payload.TenantId;
    if (typeof tenantId === 'string' && /^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(tenantId)) {
      return tenantId;
    }
  } catch {
    return null;
  }
  return null;
}

export { cleanHeaders };
