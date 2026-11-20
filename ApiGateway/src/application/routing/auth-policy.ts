import type { FastifyReply, FastifyRequest } from 'fastify';

declare module 'fastify' {
  interface FastifyRequest {
    verifiedTenant: string | null;
  }
}

type ProxyKind = 'identity' | 'orders' | 'inventory' | 'reporting' | 'swagger';
interface Principal { tenant: string; subject: string; owner: boolean }
type Verify = (header: string | undefined) => Promise<Principal>;

function canWriteIdentity(method: string, path: string, principal: Principal): boolean {
  if (principal.owner || ['GET', 'HEAD', 'OPTIONS'].includes(method)) return true;
  if (method !== 'PUT') return false;
  const selfPassword = /^\/api\/(?:identity\/)?users\/([^/]+)\/password\/?$/i.exec(path);
  if (!selfPassword) return false;
  try {
    const id = decodeURIComponent(selfPassword[1]).toLowerCase().replace(/^[{(]|[})]$/g, '').replaceAll('-', '');
    return id === principal.subject.replaceAll('-', '');
  } catch { return false; }
}

function isPublic(kind: ProxyKind, method: string, path: string): boolean {
  if (kind === 'identity') {
    return method === 'POST' && /^\/api\/(?:identity\/)?auth\/(?:login|register-tenant)\/?$/i.test(path);
  }
  // Only unambiguous Swagger asset paths may reach the Inventory proxy anonymously.
  return kind === 'swagger' && (method === 'GET' || method === 'HEAD') &&
    /^\/swagger(?:\/[a-z0-9_-][a-z0-9_.-]*)*\/?$/i.test(path);
}

export function authorizeProxy(kind: ProxyKind, verify: Verify, ordersTenant?: string) {
  return async (request: FastifyRequest, reply: FastifyReply) => {
    const path = request.url.split('?', 1)[0];
    if (isPublic(kind, request.method, path)) return;
    let principal: Principal;
    try {
      principal = await verify(request.headers.authorization);
    } catch {
      return reply.header('www-authenticate', 'Bearer').code(401).type('application/problem+json')
        .send({ type: 'about:blank', title: 'Authentication required', status: 401, requestId: request.id });
    }
    if ((kind === 'orders' && principal.tenant !== ordersTenant?.toLowerCase()) ||
        (kind === 'identity' && !canWriteIdentity(request.method, path, principal))) {
      return reply.code(403).type('application/problem+json')
        .send({ type: 'about:blank', title: 'Forbidden', status: 403, requestId: request.id });
    }
    request.verifiedTenant = principal.tenant;
  };
}
