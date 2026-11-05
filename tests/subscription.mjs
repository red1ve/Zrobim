import assert from 'node:assert/strict';
import vm from 'node:vm';
import { readFile } from 'node:fs/promises';

const code = await readFile(new URL('../src/Zrobim.Api/wwwroot/subscription.js', import.meta.url), 'utf8');
class Element {
  constructor(tag = '', text = '') {
    this.tag = tag; this.textContent = text; this.children = []; this.events = {};
    this.isConnected = true; this.classList = { contains: () => true };
  }
  append(...items) { this.children.push(...items); }
  replaceChildren(...items) { this.children = items; }
  addEventListener(name, handler) { this.events[name] = handler; }
  setAttribute() {}
  querySelector(tag) { return this.children.find(child => child.tag === tag); }
  focus() { this.focused = true; }
}
const nodes = new Map();
const get = id => {
  if (!nodes.has(id)) nodes.set(id, new Element());
  return nodes.get(id);
};
const inactive = { priceMonthly: 15, currency: 'BYN', isActive: false, expiresAt: null, canPayDemo: true };
const active = { ...inactive, isActive: true, canPayDemo: false,
  startsAt: '2026-11-05T10:00:00Z', expiresAt: '2026-12-05T10:00:00Z' };
let data = inactive;
let response;
let requests = [];
let screen;
const element = (tag, _, text) => new Element(tag, text);
const button = (cls, text, action) => {
  const node = element('button', cls, text);
  node.addEventListener('click', action);
  return node;
};
const context = vm.createContext({
  session: { accessToken: 'master-token' }, currentRole: 'MASTER',
  document: { getElementById: get }, catalogElement: element, catalogButton: button,
  formatPrice: (amount, currency) => `${amount} ${currency}`,
  demoPaymentMethods: { CARD: 'Банковская карта', ERIP: 'ЕРИП' },
  FormData: class { get() { return 'ERIP'; } },
  showScreen: value => { screen = value; },
  loadScreenContent: async (_, content, render) => { content.replaceChildren(); render(data); },
  apiRequest: async (path, options) => { requests.push([path, options]); return response; }
});
vm.runInContext(code, context);
const open = () => vm.runInContext('openMasterSubscription()', context);
const text = node => [node.textContent, ...node.children.map(text)].join(' ');

await open();
assert.equal(screen, 'payment');
const content = get('payment-content');
assert.match(text(content), /15 BYN \/ месяц/);
assert.match(text(content), /Деньги не списываются/);
const form = content.children[0];
const submit = form.children.find(child => child.type === 'submit');
let resolve;
response = new Promise(done => { resolve = done; });
const first = form.events.submit({ preventDefault() {} });
await form.events.submit({ preventDefault() {} });
assert.equal(requests.length, 1, 'Double click must send only one request');
assert.equal(submit.disabled, true);
assert.equal(requests[0][1].body.method, 'ERIP');
assert.equal(requests[0][1].token, 'master-token');
resolve(active);
await first;
assert.match(text(content), /Подписка активна/);
assert.match(text(content), /2026/);
assert.ok(content.children[0].querySelector('h3').focused);

data = active;
await open();
assert.ok(!content.children.some(child => child.tag === 'form'), 'Active members must not get another checkout');
assert.match(text(content), /Подписка активна/);
get('payment-back').events.click();
assert.equal(screen, 'cabinet');

data = inactive;
await open();
const abandoned = content.children[0];
response = new Promise(done => { resolve = done; });
const pending = abandoned.events.submit({ preventDefault() {} });
abandoned.isConnected = false;
resolve(active);
await pending;
assert.equal(content.children[0], abandoned, 'Late responses must not replace a screen the user left');

context.currentRole = 'CLIENT';
get('payment-back').events.click();
assert.equal(screen, 'bookings');
screen = 'profile';
await open();
assert.equal(screen, 'profile', 'Client must not open master checkout');
console.log('PASS: subscription checkout, amount, ERIP, double click, success, active state and navigation');
