import { Inject, Injectable } from '@nestjs/common';

export const GATEWAY_OPTIONS = Symbol('GATEWAY_OPTIONS');
export interface GatewayOptions {
  upstream: string | URL;
  identityUpstream?: string | URL;
  ordersUpstream?: string | URL;
  reportingUpstream?: string | URL;
  timeoutMs?: number;
  logger?: boolean;
  caPem?: string;
  tenantId?: string;
}

@Injectable()
export class GatewayConfigService {
  readonly options: Required<GatewayOptions>;

  constructor(@Inject(GATEWAY_OPTIONS) options: GatewayOptions) {
    this.options = {
      reportingUpstream: 'http://localhost:5004',
      ordersUpstream: 'http://localhost:5003',
      identityUpstream: 'http://localhost:5002',
      timeoutMs: 10000,
      logger: true,
      caPem: '',
      tenantId: '00000000-0000-0000-0000-000000000001',
      ...options,
    };
  }
}
