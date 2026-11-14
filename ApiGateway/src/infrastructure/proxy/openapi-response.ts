import { publishInventoryOpenApi } from '../../application/documentation/publish-inventory-openapi.js';
import type { FastifyReply, RouteGenericInterface, RawServerBase } from 'fastify';
import type { Readable } from 'node:stream';

// The public document must use the public origin, never the internal host.
export async function sendGatewayOpenApi(reply: FastifyReply<RouteGenericInterface, RawServerBase>, stream: Readable): Promise<void> {
  try {
    const input = await readOpenApi(stream);
    const document = publishInventoryOpenApi(input);
    reply.removeHeader('content-length');
    reply.removeHeader('etag');
    reply.removeHeader('last-modified');
    reply.header('cache-control', 'no-store').type('application/json').send(JSON.stringify(document));
  } catch {
    stream.destroy();
    reply.removeHeader('content-length');
    reply.code(502).type('application/problem+json').send({ type: 'about:blank', status: 502,
      title: 'Unable to process the Inventory documentation', requestId: reply.request.id });
  }
}

async function readOpenApi(stream: Readable): Promise<unknown> {
  const maxBytes = 2 * 1024 * 1024;
  const chunks: Buffer[] = [];
  let bytes = 0;
  for await (const chunk of stream) {
    const buffer = Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk);
    bytes += buffer.length;
    if (bytes > maxBytes) throw new Error('OpenAPI exceeds 2 MiB');
    chunks.push(buffer);
  }
  return JSON.parse(Buffer.concat(chunks).toString('utf8'));
}
