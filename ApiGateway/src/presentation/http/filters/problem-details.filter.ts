import { Catch, HttpException, type ArgumentsHost, type ExceptionFilter } from '@nestjs/common';
import type { FastifyReply, FastifyRequest } from 'fastify';

@Catch()
export class ProblemDetailsFilter implements ExceptionFilter {
  catch(exception: unknown, host: ArgumentsHost): void {
    const context = host.switchToHttp();
    const request = context.getRequest<FastifyRequest>();
    const reply = context.getResponse<FastifyReply>();
    const status = exception instanceof HttpException ? exception.getStatus() : 500;
    const title = status === 404 ? 'Route not available in the gateway'
      : status === 500 ? 'Internal gateway error' : 'Invalid request';
    reply.code(status).type('application/problem+json').send({
      type: 'about:blank', status, title, requestId: request.id,
    });
  }
}
