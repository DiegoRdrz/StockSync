import { ordersRoutes } from '../../application/routing/orders-routes.js';
import { Injectable } from '@nestjs/common';
import type { FastifyInstance } from 'fastify';
import httpProxy, { type FastifyHttpProxyOptions } from '@fastify/http-proxy';
import { cleanHeaders, extractTenantFromAuthorization, prepareRequestHeaders, toPublicLocation } from './proxy-headers.js';
import { GatewayConfigService } from '../configuration/gateway-config.service.js';

@Injectable()
export class OrdersProxyService {
  constructor(private readonly config: GatewayConfigService) {}

  registerRoutes(app: FastifyInstance): void {
    const target = new URL(this.config.options.ordersUpstream);
    const { timeoutMs, tenantId } = this.config.options;
    for (const { prefix, rewritePrefix } of ordersRoutes) {
      app.register(httpProxy, {
        upstream: target.origin,
        prefix,
        rewritePrefix,
        proxyPayloads: true,
        undici: { connectTimeout: timeoutMs, headersTimeout: timeoutMs, bodyTimeout: timeoutMs },
        disableRequestLogging: true,
        // Never retry: confirming a sale or receiving a purchase moves stock in Inventory.
        retryMethods: [],
        maxRetriesOn503: 0,
        internalRewriteLocationHeader: false,
        replyOptions: createReplyOptions(target, tenantId),
      });
    }
  }
}

function createReplyOptions(target: URL, tenantId: string): FastifyHttpProxyOptions['replyOptions'] {
  return {
    rewriteRequestHeaders(request, original) {
      const authHeader = original.authorization ? String(original.authorization) : undefined;
      const tokenTenant = extractTenantFromAuthorization(authHeader);
      const effectiveTenantId = tokenTenant || tenantId;

      return {
        ...prepareRequestHeaders(original, false),
        'x-request-id': request.id,
        'x-forwarded-for': request.ip,
        'x-forwarded-proto': request.protocol,
        'x-forwarded-host': request.host,
        'x-tenant-id': effectiveTenantId,
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
      ? 'The orders service timed out'
      : 'Unable to connect to the orders service',
  };
}
