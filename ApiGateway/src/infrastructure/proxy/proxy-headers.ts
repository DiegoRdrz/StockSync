import { toPublicInventoryPath } from '../../application/routing/inventory-routes.js';

function cleanHeaders<T extends Record<string, unknown>>(original: T): T {
  const headers = { ...original };
  const connectionTokens = String(original.connection || '').toLowerCase().split(',').map(value => value.trim());
  for (const name of ['connection', 'keep-alive', 'proxy-authenticate', 'proxy-authorization',
    'te', 'trailer', 'transfer-encoding', 'upgrade', ...connectionTokens]) delete headers[name];
  return headers;
}

export function prepareRequestHeaders<T extends Record<string, unknown>>(original: T, tenant: string | null, identity: boolean, isSwagger: boolean): T {
  const headers = cleanHeaders(original);
  for (const name of Object.keys(headers)) {
    if (name === 'forwarded' || name.startsWith('x-forwarded-')) delete headers[name];
  }
  delete headers['x-tenant-id'];
  if (tenant) Object.assign(headers, { 'x-tenant-id': tenant });
  if (!identity || !tenant) delete headers.authorization;
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

export { cleanHeaders };
