import { Injectable } from '@nestjs/common';
import type { FastifyInstance } from 'fastify';
import { GatewayConfigService } from '../configuration/gateway-config.service.js';

function cleanOpenApi(raw: unknown): Record<string, unknown> {
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return {};
  const doc = structuredClone(raw) as Record<string, unknown>;
  doc.servers = [{ url: '/' }];

  if (doc.paths && typeof doc.paths === 'object') {
    for (const pathItem of Object.values(doc.paths as Record<string, unknown>)) {
      if (pathItem && typeof pathItem === 'object') {
        const item = pathItem as Record<string, unknown>;
        if (Array.isArray(item.parameters)) {
          item.parameters = item.parameters.filter(
            (p: unknown) => !(p && typeof p === 'object' && 'name' in p && String((p as { name: unknown }).name).toLowerCase() === 'x-tenant-id')
          );
        }
        for (const method of ['get', 'post', 'put', 'delete', 'patch', 'options', 'head']) {
          const op = item[method] as Record<string, unknown> | undefined;
          if (op && typeof op === 'object' && Array.isArray(op.parameters)) {
            op.parameters = op.parameters.filter(
              (p: unknown) => !(p && typeof p === 'object' && 'name' in p && String((p as { name: unknown }).name).toLowerCase() === 'x-tenant-id')
            );
          }
        }
      }
    }
  }

  // Ensure security definition exists for Bearer JWT
  doc.components = (doc.components && typeof doc.components === 'object') ? doc.components : {};
  const components = doc.components as Record<string, unknown>;
  components.securitySchemes = (components.securitySchemes && typeof components.securitySchemes === 'object')
    ? components.securitySchemes
    : {};
  const securitySchemes = components.securitySchemes as Record<string, unknown>;
  if (!securitySchemes.Bearer) {
    securitySchemes.Bearer = {
      type: 'http',
      scheme: 'bearer',
      bearerFormat: 'JWT',
      description: 'JWT Authorization header using the Bearer scheme. Example: "Authorization: Bearer {token}"',
    };
  }

  return doc;
}

@Injectable()
export class GatewaySwaggerService {
  constructor(private readonly config: GatewayConfigService) {}

  registerRoutes(app: FastifyInstance): void {
    // 1. Render custom Swagger UI page
    app.get('/swagger', (_request, reply) => {
      reply.type('text/html; charset=utf-8').send(this.getSwaggerHtml());
    });

    app.get('/swagger/', (_request, reply) => {
      reply.type('text/html; charset=utf-8').send(this.getSwaggerHtml());
    });

    app.get('/swagger/index.html', (_request, reply) => {
      reply.type('text/html; charset=utf-8').send(this.getSwaggerHtml());
    });

    // 2. Individual microservice OpenAPI specs
    app.get('/swagger/docs/:service.json', async (request, reply) => {
      const { service } = request.params as { service: string };
      const spec = await this.getServiceSpec(service);
      if (!spec) {
        return reply.code(404).type('application/json').send({ message: `Service OpenAPI spec '${service}' not found or unreachable.` });
      }
      return reply.type('application/json; charset=utf-8').send(spec);
    });

    // 3. Fallback compatibility route
    app.get('/swagger/v1/swagger.json', async (_request, reply) => {
      const spec = await this.getServiceSpec('all');
      return reply.type('application/json; charset=utf-8').send(spec ?? {});
    });

    // 4. Static assets proxy (cached from Inventory Service)
    app.get('/swagger/:asset', async (request, reply) => {
      const { asset } = request.params as { asset: string };
      if (asset === 'index.html' || asset === '') {
        return reply.type('text/html; charset=utf-8').send(this.getSwaggerHtml());
      }
      try {
        const base = typeof this.config.options.upstream === 'string'
          ? this.config.options.upstream
          : this.config.options.upstream.origin;
        const assetUrl = new URL(`/swagger/${asset}`, base).toString();
        const upstreamRes = await fetch(assetUrl);
        if (!upstreamRes.ok) return reply.code(404).send();
        const contentType = upstreamRes.headers.get('content-type') || 'application/octet-stream';
        const buffer = await upstreamRes.arrayBuffer();
        return reply.type(contentType).send(Buffer.from(buffer));
      } catch {
        return reply.code(502).send();
      }
    });
  }

  private async fetchUpstreamSpec(upstreamUrl: string | URL): Promise<Record<string, unknown> | null> {
    try {
      const base = typeof upstreamUrl === 'string' ? upstreamUrl : upstreamUrl.origin;
      const targetUrl = new URL('/swagger/v1/swagger.json', base).toString();
      const res = await fetch(targetUrl, { signal: AbortSignal.timeout(4000) });
      if (!res.ok) return null;
      const json = await res.json();
      return cleanOpenApi(json);
    } catch {
      return null;
    }
  }

  private async getServiceSpec(service: string): Promise<Record<string, unknown> | null> {
    switch (service.toLowerCase()) {
      case 'identity':
        return this.fetchUpstreamSpec(this.config.options.identityUpstream);
      case 'inventory':
        return this.fetchUpstreamSpec(this.config.options.upstream);
      case 'orders':
        return this.fetchUpstreamSpec(this.config.options.ordersUpstream);
      case 'reporting':
        return this.fetchUpstreamSpec(this.config.options.reportingUpstream);
      case 'notification':
        return this.fetchUpstreamSpec(this.config.options.notificationUpstream);
      case 'all':
        return this.getCombinedSpec();
      default:
        return null;
    }
  }

  private async getCombinedSpec(): Promise<Record<string, unknown>> {
    const [identity, inventory, orders, reporting, notification] = await Promise.all([
      this.fetchUpstreamSpec(this.config.options.identityUpstream),
      this.fetchUpstreamSpec(this.config.options.upstream),
      this.fetchUpstreamSpec(this.config.options.ordersUpstream),
      this.fetchUpstreamSpec(this.config.options.reportingUpstream),
      this.fetchUpstreamSpec(this.config.options.notificationUpstream),
    ]);

    const combined: Record<string, unknown> = {
      openapi: '3.0.1',
      info: {
        title: 'StockSync Ecosystem API',
        version: 'v2',
        description: 'Unified documentation for all StockSync microservices through the API Gateway.',
      },
      servers: [{ url: '/' }],
      paths: {},
      components: {
        schemas: {},
        securitySchemes: {
          Bearer: {
            type: 'http',
            scheme: 'bearer',
            bearerFormat: 'JWT',
            description: 'JWT Authorization header using the Bearer scheme. Example: "Authorization: Bearer {token}"',
          },
        },
      },
      security: [{ Bearer: [] }],
    };

    const allSpecs = [identity, inventory, orders, reporting, notification];
    for (const spec of allSpecs) {
      if (!spec) continue;
      if (spec.paths && typeof spec.paths === 'object') {
        Object.assign(combined.paths as object, spec.paths);
      }
      const components = spec.components as Record<string, unknown> | undefined;
      if (components?.schemas && typeof components.schemas === 'object') {
        Object.assign((combined.components as Record<string, unknown>).schemas as object, components.schemas);
      }
    }

    return combined;
  }

  private getSwaggerHtml(): string {
    return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <title>StockSync - Unified API Gateway Documentation</title>
  <link rel="stylesheet" type="text/css" href="/swagger/swagger-ui.css" />
  <link rel="icon" type="image/png" href="/swagger/favicon-32x32.png" sizes="32x32" />
  <link rel="icon" type="image/png" href="/swagger/favicon-16x16.png" sizes="16x16" />
  <style>
    html { box-sizing: border-box; overflow-y: scroll; }
    *, *:before, *:after { box-sizing: inherit; }
    body { margin: 0; background: #fafafa; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif; }
    
    .gateway-nav-bar {
      background: #1e293b;
      color: #ffffff;
      padding: 14px 24px;
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
      border-bottom: 3px solid #2563eb;
      box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1);
    }
    .gateway-brand {
      display: flex;
      flex-direction: column;
    }
    .gateway-brand h1 {
      margin: 0;
      font-size: 1.25rem;
      font-weight: 700;
      letter-spacing: -0.025em;
      color: #f8fafc;
    }
    .gateway-brand span {
      font-size: 0.8rem;
      color: #94a3b8;
    }
    .service-buttons {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
    .service-btn {
      background: #334155;
      color: #f1f5f9;
      border: 1px solid #475569;
      padding: 7px 14px;
      border-radius: 6px;
      font-size: 0.85rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.15s ease-in-out;
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .service-btn:hover {
      background: #475569;
      border-color: #64748b;
      color: #ffffff;
    }
    .service-btn.active {
      background: #2563eb;
      border-color: #3b82f6;
      color: #ffffff;
      box-shadow: 0 0 10px rgba(37, 99, 235, 0.4);
    }
    /* Style tweak for swagger topbar to avoid duplicate headers */
    .swagger-ui .topbar {
      background-color: #0f172a;
    }
  </style>
</head>
<body>
  <div class="gateway-nav-bar">
    <div class="gateway-brand">
      <h1>StockSync API Gateway</h1>
      <span>Microservices Ecosystem — Interactive Documentation</span>
    </div>
    <div class="service-buttons" id="serviceButtonList">
      <button class="service-btn active" data-url="/swagger/docs/identity.json" onclick="selectService(this, '/swagger/docs/identity.json')">
        🔐 Identity
      </button>
      <button class="service-btn" data-url="/swagger/docs/inventory.json" onclick="selectService(this, '/swagger/docs/inventory.json')">
        📦 Inventory
      </button>
      <button class="service-btn" data-url="/swagger/docs/orders.json" onclick="selectService(this, '/swagger/docs/orders.json')">
        🛒 Orders
      </button>
      <button class="service-btn" data-url="/swagger/docs/reporting.json" onclick="selectService(this, '/swagger/docs/reporting.json')">
        📊 Reporting
      </button>
      <button class="service-btn" data-url="/swagger/docs/notification.json" onclick="selectService(this, '/swagger/docs/notification.json')">
        🔔 Notifications
      </button>
      <button class="service-btn" data-url="/swagger/docs/all.json" onclick="selectService(this, '/swagger/docs/all.json')">
        🌐 All Services
      </button>
    </div>
  </div>

  <div id="swagger-ui"></div>

  <script src="/swagger/swagger-ui-bundle.js"></script>
  <script src="/swagger/swagger-ui-standalone-preset.js"></script>
  <script>
    let swaggerUiInstance = null;

    function selectService(buttonElement, specUrl) {
      document.querySelectorAll('.service-btn').forEach(btn => btn.classList.remove('active'));
      if (buttonElement) buttonElement.classList.add('active');

      if (swaggerUiInstance) {
        swaggerUiInstance.specActions.updateUrl(specUrl);
        swaggerUiInstance.specActions.download(specUrl);
      }
    }

    window.onload = function() {
      swaggerUiInstance = SwaggerUIBundle({
        urls: [
          { url: "/swagger/docs/identity.json", name: "🔐 Identity Service (Auth & Users)" },
          { url: "/swagger/docs/inventory.json", name: "📦 Inventory Service (Catalog & Stock)" },
          { url: "/swagger/docs/orders.json", name: "🛒 Orders Service (Sales & Purchases)" },
          { url: "/swagger/docs/reporting.json", name: "📊 Reporting Service (Analytics)" },
          { url: "/swagger/docs/notification.json", name: "🔔 Notification Service (Alerts)" },
          { url: "/swagger/docs/all.json", name: "🌐 All Services (Combined View)" }
        ],
        "urls.primaryName": "🔐 Identity Service (Auth & Users)",
        dom_id: '#swagger-ui',
        deepLinking: true,
        presets: [
          SwaggerUIBundle.presets.apis,
          SwaggerUIStandalonePreset
        ],
        plugins: [
          SwaggerUIBundle.plugins.DownloadUrl
        ],
        layout: "StandaloneLayout"
      });

      // Synchronize button highlight when user selects from native Swagger dropdown
      setInterval(() => {
        const select = document.querySelector('.topbar select');
        if (select && !select.dataset.syncBound) {
          select.dataset.syncBound = 'true';
          select.addEventListener('change', () => {
            const currentUrl = select.value;
            document.querySelectorAll('.service-btn').forEach(btn => {
              if (btn.getAttribute('data-url') === currentUrl) {
                btn.classList.add('active');
              } else {
                btn.classList.remove('active');
              }
            });
          });
        }
      }, 500);
    };
  </script>
</body>
</html>`;
  }
}
