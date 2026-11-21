import { notificationRoutes } from '../../application/routing/notification-routes.js';
import { Injectable } from '@nestjs/common';
import type { FastifyInstance } from 'fastify';
import httpProxy, { type FastifyHttpProxyOptions } from '@fastify/http-proxy';
import { cleanHeaders, extractTenantFromAuthorization, prepareRequestHeaders, toPublicLocation } from './proxy-headers.js';
import { GatewayConfigService } from '../configuration/gateway-config.service.js';

@Injectable()
export class NotificationProxyService {
  constructor(private readonly config: GatewayConfigService) {}

  registerRoutes(app: FastifyInstance): void {
    const target = new URL(this.config.options.notificationUpstream);
    const { timeoutMs, tenantId } = this.config.options;
    for (const { prefix, rewritePrefix } of notificationRoutes) {
      app.register(httpProxy, {
        upstream: target.origin,
        prefix,
        rewritePrefix,
        proxyPayloads: true,
        undici: { connectTimeout: timeoutMs, headersTimeout: timeoutMs, bodyTimeout: timeoutMs },
        disableRequestLogging: true,
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
      return {
        ...prepareRequestHeaders(original, false),
        'x-request-id': request.id,
        'x-forwarded-for': request.ip,
        'x-forwarded-proto': request.protocol,
        'x-forwarded-host': request.host,
        'x-tenant-id': extractTenantFromAuthorization(authHeader) || tenantId,
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
      ? 'The notification service timed out'
      : 'Unable to connect to the notification service',
  };
}
