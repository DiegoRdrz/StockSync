import { authorizeProxy } from '../../application/routing/auth-policy.js';
import { AccessTokenVerifier } from '../auth/access-token-verifier.service.js';
import { inventoryRoutes } from '../../application/routing/inventory-routes.js';
import { sendGatewayOpenApi } from './openapi-response.js';
import { Injectable } from '@nestjs/common';
import type { FastifyInstance } from 'fastify';
import httpProxy, { type FastifyHttpProxyOptions } from '@fastify/http-proxy';
import { cleanHeaders, prepareRequestHeaders, toPublicLocation } from './proxy-headers.js';
import { GatewayConfigService } from '../configuration/gateway-config.service.js';

@Injectable()
export class InventoryProxyService {
  constructor(private readonly config: GatewayConfigService, private readonly auth: AccessTokenVerifier) {}

  registerRoutes(app: FastifyInstance): void {
    const target = new URL(this.config.options.upstream);
    const { timeoutMs, caPem } = this.config.options;
    for (const { prefix, rewritePrefix } of [...inventoryRoutes, { prefix: '/swagger', rewritePrefix: '/swagger' }]) {
      app.register(httpProxy, {
        upstream: target.origin,
        prefix,
        rewritePrefix,
        // The plugin streams bodies and manages connections.
        preHandler: authorizeProxy(prefix === '/swagger' ? 'swagger' : 'inventory', header => this.auth.verify(header)),
        proxyPayloads: true,
        undici: {
          connectTimeout: timeoutMs, headersTimeout: timeoutMs, bodyTimeout: timeoutMs,
          ...(caPem ? { connect: { ca: caPem } } : {}),
        },
        disableRequestLogging: true,
        retryMethods: [],
        maxRetriesOn503: 0,
        internalRewriteLocationHeader: false,
        replyOptions: createReplyOptions(target, prefix === '/swagger'),
      });
    }
  }
}

function createReplyOptions(target: URL, isSwagger: boolean): FastifyHttpProxyOptions['replyOptions'] {
  return {
    onResponse(_request, reply, response) {
      const isOpenApi = isSwagger && response.statusCode === 200 &&
        String(response.headers['content-type']).includes('json');
      if (isOpenApi) {
        void sendGatewayOpenApi(reply, response.stream);
        return;
      }
      reply.send(response.stream);
    },
    rewriteRequestHeaders(request, original) {
      return {
        ...prepareRequestHeaders(original, request.verifiedTenant, false, isSwagger),
        'x-request-id': request.id,
        'x-forwarded-for': request.ip,
        'x-forwarded-proto': request.protocol,
        'x-forwarded-host': request.host,
      };
    },
    rewriteHeaders(original, request) {
      const headers = { ...cleanHeaders(original), 'x-request-id': request?.id };
      if (headers.location) headers.location = toPublicLocation(headers.location, target);
      return headers;
    },
    onError(reply, { error }) {
      const problem = upstreamProblem(error);
      reply.code(problem.status).type('application/problem+json').send({
        type: 'about:blank',
        ...problem,
        requestId: reply.request.id,
      });
    },
  };
}

function upstreamProblem(error: Error & { statusCode?: number; cause?: unknown }) {
  const cause = error.cause as { code?: string } | undefined;
  const timeout = error.statusCode === 504 || cause?.code?.includes('TIMEOUT');
  return {
    status: timeout ? 504 : 502,
    title: timeout
      ? 'The inventory service timed out'
      : 'Unable to connect to the inventory service',
  };
}
