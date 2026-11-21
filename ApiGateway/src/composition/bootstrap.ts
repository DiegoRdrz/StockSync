import 'reflect-metadata';
import { NestFactory } from '@nestjs/core';
import { FastifyAdapter, type NestFastifyApplication } from '@nestjs/platform-fastify';
import Fastify, { LogController } from 'fastify';
import { randomUUID } from 'node:crypto';
import { AppModule } from './app.module.js';
import type { GatewayOptions } from '../infrastructure/configuration/gateway-config.service.js';
import { InventoryProxyService } from '../infrastructure/proxy/inventory-proxy.service.js';
import { IdentityProxyService } from '../infrastructure/proxy/identity-proxy.service.js';
import { OrdersProxyService } from '../infrastructure/proxy/orders-proxy.service.js';
import { ReportingProxyService } from '../infrastructure/proxy/reporting-proxy.service.js';
import { NotificationProxyService } from '../infrastructure/proxy/notification-proxy.service.js';
import { GatewaySwaggerService } from '../infrastructure/swagger/gateway-swagger.service.js';
import { ProblemDetailsFilter } from '../presentation/http/filters/problem-details.filter.js';

export async function createGateway(options: GatewayOptions): Promise<NestFastifyApplication> {
  const server = Fastify({
    logger: options.logger ?? true,
    logController: new LogController({ disableRequestLogging: true }),
    genReqId: () => randomUUID(),
    requestIdHeader: false,
    requestTimeout: 30000,
    forceCloseConnections: true,
    routerOptions: { caseSensitive: false },
  });
  server.addHook('onRequest', async (request, reply) => {
    reply.header('x-request-id', request.id);
  });
  server.addHook('onResponse', async (request, reply) => {
    request.log.info({
      requestId: request.id,
      method: request.method,
      path: request.url.split('?')[0],
      status: reply.statusCode,
      durationMs: Math.round(reply.elapsedTime),
    }, 'Request completed');
  });
  const app = await NestFactory.create<NestFastifyApplication>(
    AppModule.register(options),
    new FastifyAdapter(server),
    {
      logger: options.logger === false ? false : ['log', 'warn', 'error'],
      bodyParser: false,
      abortOnError: false,
    },
  );
  app.useGlobalFilters(new ProblemDetailsFilter());
  // Register infrastructure routes without duplicating backend controllers.
  app.get(GatewaySwaggerService).registerRoutes(server);
  app.get(InventoryProxyService).registerRoutes(server);
  app.get(IdentityProxyService).registerRoutes(server);
  app.get(OrdersProxyService).registerRoutes(server);
  app.get(ReportingProxyService).registerRoutes(server);
  app.get(NotificationProxyService).registerRoutes(server);
  await app.init();
  return app;
}
