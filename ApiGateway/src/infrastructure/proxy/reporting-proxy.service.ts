import { authorizeProxy } from '../../application/routing/auth-policy.js';
import { AccessTokenVerifier } from '../auth/access-token-verifier.service.js';
import { reportingRoutes } from '../../application/routing/reporting-routes.js';
import { Injectable } from '@nestjs/common';
import type { FastifyInstance } from 'fastify';
import httpProxy, { type FastifyHttpProxyOptions } from '@fastify/http-proxy';
import { cleanHeaders, prepareRequestHeaders, toPublicLocation } from './proxy-headers.js';
import { GatewayConfigService } from '../configuration/gateway-config.service.js';

@Injectable()
export class ReportingProxyService {
  constructor(private readonly config: GatewayConfigService, private readonly auth: AccessTokenVerifier) {}

  registerRoutes(app: FastifyInstance): void {
    const target = new URL(this.config.options.reportingUpstream);
    const { timeoutMs } = this.config.options;
    for (const { prefix, rewritePrefix } of reportingRoutes) {
      app.register(httpProxy, {
        upstream: target.origin,
        prefix,
        rewritePrefix,
        preHandler: authorizeProxy('reporting', header => this.auth.verify(header)),
        proxyPayloads: true,
        undici: { connectTimeout: timeoutMs, headersTimeout: timeoutMs, bodyTimeout: timeoutMs },
        disableRequestLogging: true,
        retryMethods: [],
        maxRetriesOn503: 0,
        internalRewriteLocationHeader: false,
        replyOptions: createReplyOptions(target),
      });
    }
  }
}

function createReplyOptions(target: URL): FastifyHttpProxyOptions['replyOptions'] {
  return {
    rewriteRequestHeaders(request, original) {
      return {
        ...prepareRequestHeaders(original, request.verifiedTenant, false, false),
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
      ? 'The reporting service timed out'
      : 'Unable to connect to the reporting service',
  };
}
