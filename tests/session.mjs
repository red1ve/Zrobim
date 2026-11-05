import assert from 'node:assert/strict';
import vm from 'node:vm';
import { readFile } from 'node:fs/promises';

const code = await readFile(new URL('../src/Zrobim.Api/wwwroot/app.js', import.meta.url), 'utf8');
const key = 'zrobim.session';
const saved = { accessToken: 'test-token', expiresAt: new Date(Date.now() + 86_400_000).toISOString() };
const user = { id: 'client-id', firstName: 'Анна', lastName: 'Тест', email: 'test@example.test', role: 'CLIENT', phone: null };

class Element {
  constructor(id = '', tag = '') {
    this.id = id; this.tag = tag; this.children = []; this.events = {};
    this.textContent = ''; this.value = ''; this.hidden = false;
    const classes = new Set();
    this.classList = {
      toggle: (n, yes) => yes ? classes.add(n) : classes.delete(n),
      add: (...names) => names.forEach(n => classes.add(n)),
      remove: n => classes.delete(n),
      contains: n => classes.has(n)
    };
  }
  append(...nodes) { this.children.push(...nodes); }
  replaceChildren(...nodes) { this.children = nodes; }
  setAttribute() {}
  removeAttribute() {}
  addEventListener(name, handler) { this.events[name] = handler; }
  reset() { this.fields = {}; }
  querySelector() { return this.button ??= new Element(); }
  getContext() { return new Proxy({}, { get: () => () => {} }); }
}

function storage(map) {
  return {
    getItem: key => map.get(key) ?? null,
    setItem: (key, value) => map.set(key, value),
    removeItem: key => map.delete(key)
  };
}

async function page(persistent, tab = new Map(), profileFailure = false) {
  const nodes = new Map();
  const get = id => {
    if (!nodes.has(id)) nodes.set(id, new Element(id));
    return nodes.get(id);
  };
  const screens = ['home', 'splash', 'login'].map(id => get('screen-' + id));
  const events = {};
  const requests = [];
  let failProfile = profileFailure;
  const context = vm.createContext({
    document: { getElementById: get, createElement: tag => new Element('', tag),
      querySelectorAll: selector => selector === '.screen' ? screens : [] },
    window: { innerWidth: 390, innerHeight: 844, scrollTo() {}, addEventListener: (name, handler) => { events[name] = handler; } },
    localStorage: storage(persistent), sessionStorage: storage(tab),
    requestAnimationFrame() {}, setTimeout() {}, clearTimeout() {}, AbortController, URLSearchParams,
    initNotifications() {}, refreshNotificationBadge() {},
    FormData: class { constructor(form) { return Object.entries(form.fields); } },
    fetch: async (path, options) => {
      requests.push([path, options]);
      if (path === '/api/me' && failProfile) throw new Error('Offline');
      const data = path === '/api/me' ? user
        : path.startsWith('/api/auth/') ? { ...saved, user }
        : path === '/api/catalog/cities' ? ['Минск', 'Брест']
        : [];
      return { ok: true, status: 200, json: async () => data };
    }
  });
  await vm.runInContext(code, context);
  return { context, get, events, requests, profileOnline() { failProfile = false; },
    active: () => screens.find(node => node.classList.contains('active'))?.id };
}

const persistent = new Map();
let first = await page(persistent);
assert.equal(first.active(), 'screen-splash');
const form = first.get('login-form');
form.fields = { email: 'test@example.test', password: 'private-test-password' };
await vm.runInContext('submitAuth', first.context)({ preventDefault() {}, currentTarget: form });
assert.equal(first.active(), 'screen-home');
assert.equal(JSON.parse(persistent.get(key)).accessToken, saved.accessToken);
assert.ok(!persistent.get(key).includes('private-test-password'));
const reopened = await page(persistent);
assert.equal(reopened.active(), 'screen-home', 'new tab restores persistent login');
vm.runInContext('logout()', reopened.context);
assert.ok(!persistent.has(key));
assert.equal(reopened.active(), 'screen-login');
assert.equal((await page(persistent)).active(), 'screen-splash');

const legacy = new Map([[key, JSON.stringify(saved)]]);
const migrated = await page(persistent, legacy);
assert.equal(migrated.active(), 'screen-home');
assert.ok(persistent.has(key) && !legacy.has(key));
persistent.delete(key);
migrated.events.storage({ key });
assert.equal(migrated.active(), 'screen-login');

persistent.set(key, JSON.stringify({ ...saved, expiresAt: '2000-01-01T00:00:00Z' }));
assert.equal((await page(persistent)).active(), 'screen-splash');
assert.ok(!persistent.has(key));

persistent.set(key, JSON.stringify(saved));
const offline = await page(persistent, new Map(), true);
assert.equal(offline.active(), 'screen-splash');
assert.equal(offline.get('restore-retry').hidden, false);
assert.ok(persistent.has(key));
offline.profileOnline();
await offline.get('restore-retry').events.click();
assert.equal(offline.active(), 'screen-home');

const browser = await page(new Map());
browser.get('catalog-city').value = 'Брест';
await browser.get('catalog-city').events.change({ currentTarget: browser.get('catalog-city') });
assert.ok(browser.requests.some(([path]) => path === '/api/catalog/categories?city=' + encodeURIComponent('Брест')));
browser.get('catalog-query').value = 'маникюр';
await browser.get('catalog-search').events.submit({ preventDefault() {} });
assert.ok(browser.requests.some(([path]) => path.startsWith('/api/catalog/search?') &&
  new URL('http://test' + path).searchParams.get('city') === 'Брест'));
await vm.runInContext("showMasters({id:'category',name:'Маникюр'})", browser.context);
assert.ok(browser.requests.some(([path]) => path.startsWith('/api/catalog/categories/category/masters?city=')));
console.log('PASS: persistent login, legacy migration, logout, expiry, offline retry and city-scoped navigation');
