import { readFileSync } from 'node:fs';

export function readConfig(env: NodeJS.ProcessEnv = process.env) {
  const upstream = readUpstream(env.INVENTORY_API_URL || 'http://localhost:5001');
  const port = readPositiveInteger(env.PORT || '9090', 'PORT', 65535);
  const timeoutMs = readPositiveInteger(env.UPSTREAM_TIMEOUT_MS || '10000', 'UPSTREAM_TIMEOUT_MS', 300000);
  const tenantId = readTenantId(env.TENANT_ID || '00000000-0000-0000-0000-000000000001');
  const caPem = env.INVENTORY_CA_FILE ? readFileSync(env.INVENTORY_CA_FILE, 'utf8') : '';

  return { upstream, port, timeoutMs, caPem, tenantId, host: env.HOST || '127.0.0.1' };
}

function readUpstream(value: string): URL {
  const url = new URL(value);
  const isHttp = url.protocol === 'http:' || url.protocol === 'https:';
  const hasCredentials = Boolean(url.username || url.password);
  const hasPathOrQuery = url.pathname !== '/' || Boolean(url.search || url.hash);
  if (!isHttp || hasCredentials || hasPathOrQuery) {
    throw new Error('INVENTORY_API_URL must be an HTTP(S) origin without credentials, path or query.');
  }
  return url;
}

function readPositiveInteger(value: string, name: string, maximum: number): number {
  const number = Number(value);
  if (!Number.isInteger(number) || number < 1 || number > maximum) {
    throw new Error(`${name} must be between 1 and ${maximum}.`);
  }
  return number;
}

function readTenantId(value: string): string {
  const isGuid = /^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(value);
  const isEmpty = value === '00000000-0000-0000-0000-000000000000';
  if (!isGuid || isEmpty) {
    throw new Error('TENANT_ID must be a valid, non-empty GUID.');
  }
  return value;
}
