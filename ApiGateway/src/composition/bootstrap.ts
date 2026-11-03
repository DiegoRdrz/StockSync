import 'reflect-metadata';
import { NestFactory } from '@nestjs/core';
import { FastifyAdapter, type NestFastifyApplication } from '@nestjs/platform-fastify';
import Fastify, { LogController } from 'fastify';
import { randomUUID } from 'node:crypto';
import { AppModule } from './app.module.js';
import type { GatewayOptions } from '../infrastructure/configuration/gateway-config.service.js';
import { InventoryProxyService } from '../infrastructure/proxy/inventory-proxy.service.js';
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
    }, 'Solicitud finalizada');
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
  // Como MapReverseProxy en ASP.NET Core: rutas de infraestructura, sin duplicar los controladores del inventario.
  app.get(InventoryProxyService).registerRoutes(server);
  await app.init();
  return app;
}
