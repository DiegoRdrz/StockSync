import assert from 'node:assert/strict';
import { after, before, test } from 'node:test';
import { randomBytes, randomUUID } from 'node:crypto';
import { createServer } from 'node:http';
import { SignJWT } from 'jose';
import { createGateway } from '../dist/composition/bootstrap.js';

const secret = randomBytes(48).toString('hex');
const fixed = randomUUID(), other = randomUUID();
const issuer = 'StockSync.Identity', audience = 'StockSync.Ecosystem';
let server, app;
const calls = [];
async function credential(tenant) {
  return 'Bearer ' + await new SignJWT({ tenant_id: tenant })
    .setSubject(randomUUID()).setIssuer(issuer).setAudience(audience).setExpirationTime('5m')
    .setProtectedHeader({ alg: 'HS256' }).sign(Buffer.from(secret));
}
function request(path, headers = {}, method = 'GET') {
  return app.inject({ method, url: path, headers });
}
before(async () => {
  server = createServer((req, res) => {
    calls.push(req.url);
    res.writeHead(200, { 'content-type': 'application/json' });
    res.end(JSON.stringify({ path: req.url, tenant: req.headers['x-tenant-id'], bearer: !!req.headers.authorization }));
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const upstream = 'http://127.0.0.1:' + server.address().port;
  app = await createGateway({ upstream, identityUpstream: upstream, ordersUpstream: upstream,
    reportingUpstream: upstream, logger: false, tenantId: fixed, jwtSecret: secret, jwtIssuer: issuer, jwtAudience: audience });
});
after(async () => { await app?.close(); server?.closeAllConnections(); await new Promise(resolve => server.close(resolve)); });

test('rejects protected requests before contacting any upstream', async () => {
  const count = calls.length;
  for (const path of ['/api/inventory/products','/api/orders','/api/purchase-orders','/api/reports/sales',
    '/api/auth/me','/api/identity/auth/me','/api/users','/api/identity/users','/api/tenants/current','/api/identity/tenants/current']) {
    for (const headers of [{}, { authorization: 'Bearer invalid', 'x-tenant-id': fixed }]) {
      assert.equal((await request(path, headers)).statusCode, 401, path);
    }
  }
  for (const path of ['/api/orders', '/api/purchase-orders']) {
    assert.equal((await request(path, { authorization: await credential(other) })).statusCode, 403);
  }
  for (const path of ['/api/users', '/api/identity/users']) {
    assert.equal((await request(path, { authorization: await credential(other) }, 'POST')).statusCode, 403);
  }
  assert.equal(calls.length, count);
});
test('public paths preserve methods and preflight terminates locally', async () => {
  assert.equal((await request('/')).statusCode, 302);
  assert.equal((await request('/health')).statusCode, 200);
  assert.equal((await request('/swagger/index.html')).statusCode, 200);
  for (const prefix of ['/api/auth','/api/identity/auth']) {
    for (const action of ['login','register-tenant']) {
      const path = prefix + '/' + action;
      const reply = await request(path, { 'x-tenant-id': fixed }, 'POST');
      assert.equal(reply.statusCode, 200);
      assert.equal(reply.json().tenant, undefined);
      assert.equal((await request(path)).statusCode, 401);
    }
  }
  const count = calls.length;
  assert.equal((await request('/api/orders', { origin: 'https://example.test', 'access-control-request-method':'POST' }, 'OPTIONS')).statusCode, 204);
  assert.equal(count, calls.length);
});
test('concurrent proxy calls keep verified tenant, route and credential boundaries', async () => {
  await Promise.all([fixed, other].map(async tenant => {
    const authorization = await credential(tenant);
    const mismatch = tenant === fixed ? other : fixed;
    const routes = [
      ['/api/inventory/products?page=2','/api/products?page=2',false],
      ['/api/reports/sales','/api/reports/sales',false],
      ['/api/identity/tenants/current','/api/tenants/current',true],
      ['/api/users','/api/users',true],
    ];
    if (tenant === fixed) routes.push(['/api/orders','/api/orders',false],['/api/purchase-orders','/api/purchase-orders',false]);
    await Promise.all(routes.map(async ([path, expected, bearer]) => {
      const res = await request(path, { authorization, 'x-tenant-id': mismatch });
      assert.equal(res.statusCode, 200);
      assert.deepEqual(res.json(), { path: expected, tenant, bearer });
    }));
  }));
});
