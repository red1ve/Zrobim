import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';

const base = process.env.ZROBIM_SMOKE_BASE_URL ?? 'http://localhost:5262';
const adminEmail = process.env.ZROBIM_SMOKE_ADMIN_EMAIL;
const adminPassword = process.env.ZROBIM_SMOKE_ADMIN_PASSWORD;
const prefix = process.env.ZROBIM_SMOKE_PREFIX ?? 'smoke-' + randomUUID().slice(0, 12);
const fixturePassword = randomUUID() + '!';
assert.ok(adminEmail && adminPassword, 'Set the credentials of the administrator in the test environment.');
assert.match(prefix, /^smoke-[a-z0-9-]{4,32}$/, 'Use a unique smoke- prefix for test data.');

async function request(path, { token, body, method = body ? 'POST' : 'GET' } = {}) {
  const headers = { Accept: 'application/json' };
  if (token) headers.Authorization = 'Bearer ' + token;
  if (body) headers['Content-Type'] = 'application/json';
  const response = await fetch(new URL(path, base), {
    method, headers, body: body && JSON.stringify(body), signal: AbortSignal.timeout(15000)
  });
  return { status: response.status, data: await response.json().catch(() => null) };
}

async function expect(path, status, options) {
  const response = await request(path, options);
  assert.equal(response.status, status, path + ' returned ' + JSON.stringify(response.data));
  return response.data;
}

async function register(name, role) {
  return expect('/api/auth/register', 201, {
    body: { email: prefix + '-' + name + '@example.test', password: fixturePassword, firstName: name, role }
  });
}

function getBookings(token) {
  return expect('/api/bookings', 200, { token });
}

async function main() {
  assert.equal((await fetch(new URL('/health', base), { signal: AbortSignal.timeout(15000) })).status, 200);
  const admin = await expect('/api/auth/login', 200, { body: { email: adminEmail, password: adminPassword } });
  assert.equal(admin.user.role, 'ADMIN');
  const master = await register('master', 'MASTER');
  const first = await register('client-a', 'CLIENT');
  const second = await register('client-b', 'CLIENT');
  const at = admin.accessToken;
  const mt = master.accessToken;
  const masterId = master.user.masterId;

  const subscriptionPath = '/api/master/subscription';
  const subscribe = subscriptionPath + '/demo-payment';
  await expect(subscriptionPath, 401);
  await expect(subscribe, 401, { body: { method: 'CARD' } });
  for (const token of [at, first.accessToken]) {
    await expect(subscriptionPath, 403, { token });
    await expect(subscribe, 403, { token, body: { method: 'CARD' } });
  }
  const inactive = await expect(subscriptionPath, 200, { token: mt });
  assert.equal(inactive.priceMonthly, 15);
  assert.equal(inactive.isActive, false);
  assert.equal(inactive.expiresAt, null);
  assert.equal(inactive.canPayDemo, true);
  await expect(subscribe, 400, { token: mt, body: { method: 'CASH' } });
  const purchased = await Promise.all(['CARD', 'ERIP'].map(method =>
    expect(subscribe, 200, { token: mt, body: { method } })));
  assert.deepEqual(purchased[0], purchased[1], 'Concurrent payments must grant one period');
  const active = purchased[0];
  assert.equal(active.isActive, true);
  assert.equal(active.canPayDemo, false);
  assert.ok(Date.parse(active.expiresAt) - Date.parse(active.startsAt) >= 28 * 86400000);
  assert.ok(Date.parse(active.expiresAt) - Date.parse(active.startsAt) <= 31 * 86400000);
  assert.deepEqual(await expect(subscribe, 200, { token: mt, body: { method: 'CARD' } }), active);
  assert.deepEqual((await expect('/api/master/cabinet', 200, { token: mt })).subscription, active);
  assert.equal((await expect(subscriptionPath, 200, { token: mt })).isActive, true);
  console.log('PASS: monthly subscription, payment validation, ownership, concurrency and retry');

  await expect('/api/admin/categories', 401);
  await expect('/api/admin/categories', 403, { token: mt });
  const category = await expect('/api/admin/categories', 201, {
    token: at, body: { name: prefix, isActive: true }
  });
  const categoryRoute = '/api/catalog/categories/' + category.id + '/masters';
  await expect('/api/master/profile', 204, { token: mt, method: 'PUT', body: {
    city: 'Минск', description: 'Smoke test profile', address: 'Smoke test address', showAddressInCatalog: false
  } });
  const service = await expect('/api/master/services', 201, { token: mt, body: {
    categoryId: category.id, name: prefix + ' Уборка', price: 45, durationMinutes: 60,
    locationType: 'CLIENT_PLACE', isActive: true
  } });
  await expect('/api/master/working-hours', 204, { token: mt, method: 'PUT', body: {
    days: Array.from({ length: 7 }, (_, i) => ({ dayOfWeek: i + 1, startTime: '09:00:00', endTime: '18:00:00' }))
  } });
  assert.deepEqual(await expect(categoryRoute, 200), []);
  const pending = await expect('/api/admin/masters/pending', 200, { token: at });
  assert.equal(pending.find(item => item.id === masterId)?.activeServices, 1);
  await expect('/api/admin/masters/' + masterId + '/approve', 204, { token: at, method: 'POST' });
  const [profile] = await expect(categoryRoute, 200);
  assert.equal(profile.id, masterId);
  assert.equal(profile.publicAddress, null);
  const minsk = new URLSearchParams({ city: 'Минск' });
  const brest = new URLSearchParams({ city: 'Брест' });
  const localCategory = (await expect('/api/catalog/categories?' + minsk, 200)).find(row => row.id === category.id);
  assert.equal(localCategory.masterCount, 1);
  assert.equal(localCategory.serviceCount, 1);
  assert.ok(!(await expect('/api/catalog/categories?' + brest, 200)).some(row => row.id === category.id));
  assert.deepEqual(await expect(categoryRoute + '?' + brest, 200), []);

  const query = new URLSearchParams({ q: prefix + ' уборка', city: 'мИнСк' });
  assert.ok((await expect('/api/catalog/search?' + query, 200)).items.some(item => item.service.id === service.id));

  const favorite = '/api/favorites/' + masterId;
  await expect(favorite, 204, { token: first.accessToken, method: 'PUT' });
  assert.deepEqual(await expect(favorite, 200, { token: second.accessToken }), { isFavorite: false });
  await expect(favorite, 204, { token: first.accessToken, method: 'DELETE' });
  const calendar = await expect('/api/master/calendar', 200, { token: mt });
  assert.equal(calendar.days.length, 7);
  console.log('PASS: roles, cabinet, approval, catalog search, favorites and calendar');

  const slotsPath = '/api/catalog/services/' + service.id + '/availability';
  const available = await expect(slotsPath, 200);
  const [firstSlot] = available.slots;
  assert.ok(firstSlot, 'Expected availability for the new master');
  const bookingBody = { serviceId: service.id, startAt: firstSlot.startAt, address: 'Smoke test address' };
  const clientTokens = [first.accessToken, second.accessToken];
  const attempts = await Promise.all(clientTokens.map(token => request('/api/bookings', { token, body: bookingBody })));
  assert.deepEqual(attempts.map(result => result.status).sort(), [201, 409]);
  const winner = attempts.findIndex(result => result.status === 201);
  const booking = attempts[winner].data;
  const client = clientTokens[winner];
  const other = clientTokens[1 - winner];
  const masterAction = (id, action) => '/api/master/bookings/' + id + '/' + action;
  const payment = '/api/bookings/' + booking.id + '/demo-payment';
  await expect(payment, 409, { token: client, body: { method: 'CARD' } });
  await expect(payment, 404, { token: other, body: { method: 'CARD' } });
  await expect(masterAction(booking.id, 'accept'), 403, { token: client, method: 'POST' });
  await expect(masterAction(booking.id, 'accept'), 204, { token: mt, method: 'POST' });
  assert.equal((await getBookings(client))[0].canPayDemo, true);
  assert.deepEqual(await getBookings(other), []);
  await expect(payment, 400, { token: client, body: { method: 'UNKNOWN' } });
  const receipts = await Promise.all(['CARD', 'ERIP'].map(method => expect(payment, 200, { token: client, body: { method } })));
  assert.deepEqual(receipts[0], receipts[1]);
  assert.equal(receipts[0].amount, 45);
  assert.equal((await getBookings(client))[0].status, 'CONFIRMED');
  const incoming = await expect('/api/master/bookings', 200, { token: mt });
  assert.equal(incoming.find(row => row.id === booking.id)?.status, 'CONFIRMED');
  await expect('/api/bookings/' + booking.id + '/cancel', 409, { token: client, method: 'POST' });

  const notes = await expect('/api/notifications', 200, { token: client });
  const notice = notes.items.find(row => row.bookingId === booking.id && row.type === 'BOOKING_ACCEPTED');
  assert.ok(notice && !notice.isRead);
  const readPath = '/api/notifications/' + notice.id + '/read';
  await expect(readPath, 404, { token: other, method: 'POST' });
  await expect(readPath, 204, { token: client, method: 'POST' });
  console.log('PASS: concurrent booking, ownership, acceptance, notifications and idempotent demo payment');

  const nextSlot = (await expect(slotsPath, 200)).slots[0];
  const secondBody = { ...bookingBody, startAt: nextSlot.startAt };
  const declined = await expect('/api/bookings', 201, { token: other, body: secondBody });
  await expect(masterAction(declined.id, 'decline'), 204, { token: mt, method: 'POST' });
  const withdrawn = await expect('/api/bookings', 201, { token: other, body: secondBody });
  await expect('/api/bookings/' + withdrawn.id + '/cancel', 204, { token: other, method: 'POST' });
  const histories = await getBookings(other);
  assert.equal(histories.find(row => row.id === declined.id)?.cancellationReason, 'MASTER_DECLINED');
  assert.equal(histories.find(row => row.id === withdrawn.id)?.cancellationReason, 'CLIENT_CANCELLED');
  assert.ok((await expect(slotsPath, 200)).slots.some(slot => slot.startAt === nextSlot.startAt));
  console.log('PASS: decline, withdrawal and released time slot');
}

console.log('Test data prefix:', prefix);
await main().catch(error => {
  console.error(error.message);
  process.exitCode = 1;
});
