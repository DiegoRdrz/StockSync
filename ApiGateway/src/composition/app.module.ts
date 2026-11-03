import { Module, type DynamicModule } from '@nestjs/common';
import { AppController } from '../presentation/http/controllers/app.controller.js';
import { HealthController } from '../presentation/http/controllers/health.controller.js';
import { HealthService } from '../application/health/health.service.js';
import { GATEWAY_OPTIONS, GatewayConfigService, type GatewayOptions } from '../infrastructure/configuration/gateway-config.service.js';
import { InventoryProxyService } from '../infrastructure/proxy/inventory-proxy.service.js';

@Module({})
export class AppModule {
  static register(options: GatewayOptions): DynamicModule {
    return {
      module: AppModule,
      controllers: [AppController, HealthController],
      providers: [
        { provide: HealthService, useFactory: () => new HealthService() },
        { provide: GATEWAY_OPTIONS, useValue: options },
        GatewayConfigService,
        InventoryProxyService,
      ],
    };
  }
}
