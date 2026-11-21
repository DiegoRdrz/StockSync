import { Injectable } from '@nestjs/common';
import { jwtVerify } from 'jose';
import { GatewayConfigService } from '../configuration/gateway-config.service.js';

function isIdentity(value: unknown): value is string {
  return typeof value === 'string' && /^[a-f0-9]{8}(?:-[a-f0-9]{4}){3}-[a-f0-9]{12}$/i.test(value)
    && value !== '00000000-0000-0000-0000-000000000000';
}

@Injectable()
export class AccessTokenVerifier {
  private readonly key: Uint8Array;

  constructor(private readonly config: GatewayConfigService) {
    const { jwtSecret, jwtIssuer, jwtAudience } = config.options;
    if (!jwtSecret?.trim() || Buffer.byteLength(jwtSecret, 'utf8') < 32 || !jwtIssuer?.trim() || !jwtAudience?.trim()) {
      throw new Error('Configure a signing key of at least 32 UTF-8 bytes, an issuer and an audience.');
    }
    this.key = new TextEncoder().encode(jwtSecret);
  }

  async verify(header: string | undefined): Promise<{ tenant: string; subject: string; owner: boolean }> {
    const match = /^Bearer ([^\s,]+)$/i.exec(header ?? '');
    if (!match) throw new Error('Invalid bearer credential.');
    const { jwtIssuer: issuer, jwtAudience: audience } = this.config.options;
    const { payload } = await jwtVerify(match[1], this.key, {
      algorithms: ['HS256'], issuer, audience, clockTolerance: 0, requiredClaims: ['exp', 'sub', 'tenant_id'],
    });
    if (!isIdentity(payload.sub) || !isIdentity(payload.tenant_id)) throw new Error('Invalid identity.');
    return { tenant: payload.tenant_id.toLowerCase(), subject: payload.sub.toLowerCase(), owner: payload.role === 'Owner' };
  }
}
