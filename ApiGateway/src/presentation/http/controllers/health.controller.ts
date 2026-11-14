import { Controller, Get } from '@nestjs/common';
import { HealthService, type HealthResponse } from '../../../application/health/health.service.js';

@Controller('health')
export class HealthController {
  constructor(private readonly healthService: HealthService) {}

  @Get()
  getStatus(): HealthResponse {
    return this.healthService.getStatus();
  }
}
