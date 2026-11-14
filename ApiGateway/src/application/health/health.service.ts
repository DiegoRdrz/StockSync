export interface HealthResponse {
  status: 'ok';
  service: string;
}

export class HealthService {
  getStatus(): HealthResponse {
    return { status: 'ok', service: 'stocksync-api-gateway' };
  }
}
