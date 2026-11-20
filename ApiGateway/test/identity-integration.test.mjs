import assert from 'node:assert/strict';
import test from 'node:test';
import { randomBytes, randomUUID } from 'node:crypto';
import { setTimeout as delay } from 'node:timers/promises';

const base = process.env.STOCKSYNC_TEST_GATEWAY;
test('Identity tokens isolate two tenants through sales, purchasing and reporting', {
  skip: base ? false : 'Set STOCKSYNC_TEST_GATEWAY to run against an isolated local stack.',
  timeout: 120_000,
}, async () => {
  const origin = new URL(base);
  assert.ok(['localhost', '127.0.0.1', '[::1]'].includes(origin.hostname), 'Use an isolated loopback gateway.');
  const email = process.env.STOCKSYNC_TEST_OWNER_EMAIL;
  const password = process.env.STOCKSYNC_TEST_OWNER_PASSWORD;
  assert.ok(email && password, 'Configure the isolated Orders tenant owner credentials.');

  async function call(path, { token, tenant, body, method = body ? 'POST' : 'GET', status = 200 } = {}) {
    const headers = {};
    if (token) headers.authorization = 'Bearer ' + token;
    if (tenant) headers['x-tenant-id'] = tenant;
    if (body) headers['content-type'] = 'application/json';
    const response = await fetch(new URL(path, origin), {
      method, headers, redirect: 'error', body: body && JSON.stringify(body), signal: AbortSignal.timeout(10_000),
    });
    assert.equal(response.status, status, method + ' ' + path);
    if (status >= 400 || status === 204) return;
    return response.json();
  }

  const a = await call('/api/auth/login', { body: { email, password } });
  const suffix = randomUUID();
  const b = await call('/api/identity/auth/register-tenant', { status: 201, body: {
    companyName: 'Security test ' + suffix, firstName: 'Test', lastName: 'Owner',
    email: suffix + '@example.test', password: 'A1' + randomBytes(24).toString('hex'),
  } });
  assert.notEqual(a.tenant.id, b.tenant.id);
  const owner = { token: a.accessToken, tenant: b.tenant.id };
  const other = { token: b.accessToken, tenant: a.tenant.id };
  const checkTenant = async (session, id, login) => {
    assert.equal((await call('/api/tenants/current', session)).id, id);
    const users = await call('/api/identity/users', session);
    assert.ok(users.items.some(u => u.email === login));
    assert.ok(users.items.every(u => u.tenantId === id));
  };
  await Promise.all([checkTenant(owner, a.tenant.id, email), checkTenant(other, b.tenant.id, b.user.email)]);
  const updated = await call('/api/identity/tenants/current', { ...other, method: 'PUT', body: { name: b.tenant.name } });
  assert.equal(updated.id, b.tenant.id);
  const employeeLogin = { email: 'employee-' + suffix + '@example.test', password: 'A1' + randomBytes(24).toString('hex') };
  const employee = await call('/api/users', { ...other, status: 201, body: {
    ...employeeLogin, firstName: 'Test', lastName: 'Employee', role: 2,
  } });
  assert.equal(employee.tenantId, b.tenant.id);
  const employeeSession = await call('/api/auth/login', { body: employeeLogin });
  await call('/api/tenants/current', { token: employeeSession.accessToken, method: 'PUT', status: 403, body: { name: b.tenant.name } });
  await call('/api/orders', { ...other, status: 403 });
  await call('/api/purchase-orders', { ...other, status: 403 });
  await call('/api/reports/sales/summary', { status: 401 });
  const segments = a.accessToken.split('.');
  const claims = JSON.parse(Buffer.from(segments[1], 'base64url'));
  segments[1] = Buffer.from(JSON.stringify({ ...claims, tenant_id: b.tenant.id })).toString('base64url');
  await call('/api/reports/sales/summary', { token: segments.join('.'), status: 401 });

  const branchId = randomUUID();
  const product = await call('/api/inventory/products', { ...owner, status: 201, body: {
    name: 'Verification product', sku: 'SEC-' + suffix.slice(0, 12), purchasePrice: 7, salePrice: 11, minimumStock: 30,
  } });
  await call('/api/inventory/products/' + product.id, { ...other, status: 404 });
  const stock = await call('/api/inventory/stock', { ...owner, status: 201, body: { productId: product.id, branchId, quantity: 10 } });
  const sale = await call('/api/orders', { ...owner, status: 201, body: { branchId } });
  await call('/api/orders/' + sale.id + '/lines', { ...owner, body: { productId: product.id, quantity: 2 } });
  const confirmed = await call('/api/orders/' + sale.id + '/confirm', { ...owner, method: 'POST' });
  assert.equal(confirmed.status, 'Confirmed');
  assert.equal(confirmed.total, 22);
  const po = await call('/api/purchase-orders', { ...owner, status: 201, body: { branchId, supplierName: 'Verification supplier' } });
  const purchase = '/api/purchase-orders/' + po.id;
  await call(purchase + '/lines', { ...owner, body: { productId: product.id, quantity: 5 } });
  await call(purchase + '/place', { ...owner, method: 'POST' });
  const received = await call(purchase + '/receive', { ...owner, method: 'POST' });
  assert.equal(received.status, 'Received');
  assert.equal(received.total, 35);

  const report = (path, session) => call('/api/reports/' + path + '?branchId=' + branchId, session);
  async function snapshot(session) {
    const [sales, purchases, movements, levels] = await Promise.all(
      ['sales/summary','purchases/summary','inventory/movements/summary','inventory/low-stock'].map(p => report(p, session)));
    return { sales: sales.totalAmount, purchases: purchases.totalAmount, movements: movements.byProduct[0]?.count ?? 0,
      balance: levels.items.find(l => l.stockId === stock.id)?.quantity ?? null };
  }
  const deadline = Date.now() + 45_000;
  let result;
  do {
    result = await snapshot(owner);
    if (result.sales === 22 && result.purchases === 35 && result.movements === 3 && result.balance === 13) break;
    await delay(300);
  } while (Date.now() < deadline);
  assert.deepEqual(result, { sales: 22, purchases: 35, movements: 3, balance: 13 });
  assert.deepEqual(await snapshot(other), { sales: 0, purchases: 0, movements: 0, balance: null });
});
