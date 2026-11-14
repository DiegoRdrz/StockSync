import { toPublicInventoryPath } from '../routing/inventory-routes.js';

function isRecord(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function isTenantHeader(parameter: unknown): boolean {
  if (!isRecord(parameter)) return false;
  return parameter.in === 'header' && String(parameter.name).toLowerCase() === 'x-tenant-id';
}

function removeInternalSettings(item: Record<string, unknown>): void {
  delete item.servers;
  if (Array.isArray(item.parameters)) {
    item.parameters = item.parameters.filter(parameter => !isTenantHeader(parameter));
  }
}

function publishPath(item: unknown): Record<string, unknown> {
  if (!isRecord(item)) throw new Error('Invalid OpenAPI path');
  removeInternalSettings(item);
  for (const method of ['get', 'put', 'post', 'delete', 'options', 'head', 'patch', 'trace']) {
    const operation = item[method];
    if (isRecord(operation)) removeInternalSettings(operation);
  }
  return item;
}

// Publication policy independent of HTTP transport and framework.
export function publishInventoryOpenApi(input: unknown): Record<string, unknown> {
  if (!isRecord(input)) throw new Error('Invalid OpenAPI document');
  const document = structuredClone(input);
  if (typeof document.openapi !== 'string') return document;

  document.servers = [{ url: '/' }];
  if (document.paths !== undefined && !isRecord(document.paths)) {
    throw new Error('Invalid OpenAPI paths');
  }
  const paths = Object.entries(document.paths ?? {});
  document.paths = Object.fromEntries(paths.map(([path, item]) => [
    toPublicInventoryPath(path),
    publishPath(item),
  ]));
  return document;
}
