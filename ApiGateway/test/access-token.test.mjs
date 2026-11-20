import 'reflect-metadata';
import assert from 'node:assert/strict';
import test from 'node:test';
import { randomBytes, randomUUID } from 'node:crypto';
import { SignJWT } from 'jose';
import { AccessTokenVerifier } from '../dist/infrastructure/auth/access-token-verifier.service.js';

const jwtSecret = randomBytes(48).toString('hex');
const options = { jwtSecret, jwtIssuer: 'StockSync.Identity', jwtAudience: 'StockSync.Ecosystem' };
const verifier = new AccessTokenVerifier({ options });
const key = Buffer.from(jwtSecret);
const now = Math.floor(Date.now() / 1000);
const claims = { sub: randomUUID(), tenant_id: randomUUID(), iss: options.jwtIssuer, aud: options.jwtAudience, exp: now + 300 };
const sign = (payload, alg = 'HS256', signingKey = key) =>
  new SignJWT(payload).setProtectedHeader({ alg }).sign(signingKey);

test('accepts an Identity-compatible access token', async () => {
  assert.equal(await verifier.verify('Bearer ' + await sign(claims)), claims.tenant_id);
});
for (const [name, changes] of Object.entries({
  expired: { exp: now - 1 }, premature: { nbf: now + 120 }, issuer: { iss: 'other' }, audience: { aud: 'other' },
  subject: { sub: undefined }, tenant: { tenant_id: undefined }, expiration: { exp: undefined },
  malformedTenant: { tenant_id: 'invalid' }, emptySubject: { sub: '00000000-0000-0000-0000-000000000000' },
})) {
  test('rejects invalid ' + name, async () => {
    await assert.rejects(verifier.verify('Bearer ' + await sign({ ...claims, ...changes })));
  });
}
test('rejects mismatched signatures, algorithms, unsigned tokens and malformed credentials', async () => {
  const signed = await sign(claims);
  const [header, body, signature] = signed.split('.');
  const otherBody = (await sign({ ...claims, tenant_id: randomUUID() })).split('.')[1];
  for (const credential of [undefined, 'Basic abc', 'Bearer opaque', 'Bearer  ' + signed,
    'Bearer ' + await sign(claims, 'HS256', randomBytes(48)), 'Bearer ' + await sign(claims, 'HS512'),
    'Bearer ' + [header, otherBody, signature].join('.'), 'Bearer ' + [Buffer.from('{"alg":"none"}').toString('base64url'), body, ''].join('.')]) {
    await assert.rejects(verifier.verify(credential));
  }
});
test('requires signing configuration', () => {
  for (const invalid of [{ jwtSecret: '' }, { jwtSecret: 'short' }, { jwtIssuer: '' }, { jwtAudience: '' }]) {
    assert.throws(() => new AccessTokenVerifier({ options: { ...options, ...invalid } }));
  }
});
