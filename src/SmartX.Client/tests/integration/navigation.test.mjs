import assert from 'node:assert/strict';
import { before, after, beforeEach, afterEach, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { setTimeout as delay } from 'node:timers/promises';
import { JSDOM } from 'jsdom';
import { createServer } from 'vite';
import { act, createElement, StrictMode } from 'react';

let dom;
let vite;
let App;
let createRoot;
let root;
let requests;
let registered;
let override;
const originalFetch = globalThis.fetch;
const apiBase = 'http://127.0.0.1:5075';
const location = { id: 'node-reservoir', name: 'Reservoir 1', code: 'RES-1', nodeType: 'Node' };
const sensor = { id: 'ph', friendlyName: 'Nutrient pH', macAddress: 'A4:CF:12:8B:40:01', category: 'Environmental', measuredProperty: 'Nutrient pH', valueKind: 'Float', unit: 'pH', expectedMinimum: 5.5, expectedMaximum: 6.5, deploymentLocation: location };
const power = { ...sensor, id: 'power', friendlyName: 'Pump Power Draw', valueKind: 'Integer', expectedMinimum: null, expectedMaximum: null, unit: 'W' };
const savedGlobals = new Map();

before(async () => {
  dom = new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>', { url: 'http://localhost:5173', pretendToBeVisual: true });
  for (const name of ['window', 'document', 'navigator', 'HTMLElement', 'HTMLInputElement', 'HTMLSelectElement', 'Node', 'Event', 'MouseEvent', 'KeyboardEvent', 'File', 'FormData']) {
    savedGlobals.set(name, Object.getOwnPropertyDescriptor(globalThis, name));
    Object.defineProperty(globalThis, name, { configurable: true, writable: true, value: dom.window[name] });
  }
  globalThis.IS_REACT_ACT_ENVIRONMENT = true;
  ({ createRoot } = await import('react-dom/client'));
  vite = await createServer({
    root: fileURLToPath(new URL('../../', import.meta.url)),
    server: { middlewareMode: true },
    appType: 'custom',
    define: { 'import.meta.env.VITE_API_BASE_URL': JSON.stringify(apiBase) },
  });
  ({ default: App } = await vite.ssrLoadModule('/src/App.jsx'));
});

after(async () => {
  await vite?.close();
  dom?.window.close();
  globalThis.fetch = originalFetch;
  delete globalThis.IS_REACT_ACT_ENVIRONMENT;
  for (const [name, descriptor] of savedGlobals) {
    if (descriptor) Object.defineProperty(globalThis, name, descriptor);
    else delete globalThis[name];
  }
});

function response(body, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } });
}

beforeEach(async () => {
  requests = [];
  registered = [];
  override = null;
  globalThis.fetch = async (address, options = {}) => {
    const url = new URL(address);
    requests.push({ url, ...options });
    const special = await override?.(url, options);
    if (special) return special;
    const path = url.pathname;
    if (path === '/api/health') {
      if (process.env.SMARTX_REAL_API_HEALTH_URL) return originalFetch(process.env.SMARTX_REAL_API_HEALTH_URL, options);
      return response({ status: 'Healthy' });
    }
    if (path === '/api/deployment-nodes') return response([location]);
    if (path === '/api/telemetry/diagnostics/health-summary') return response({ totalSensorCount: 2, connectedSensorCount: 2, staleSensorCount: 0, disconnectedSensorCount: 0, invalidLatestReadingCount: 1, noDataSensorCount: 0, connectedThresholdMinutes: 5, disconnectedThresholdMinutes: 15, evaluatedAtUtc: '2026-10-07T09:00:00Z' });
    if (path === '/api/sensors' && options.method === 'POST') {
      const result = { ...JSON.parse(options.body), deploymentLocation: location };
      registered.push(result);
      return response(result, 201);
    }
    if (path === '/api/sensors') {
      const query = (url.searchParams.get('search') ?? '').toLowerCase();
      return response([sensor, power, ...registered].filter((item) => !query || item.friendlyName.toLowerCase().includes(query)));
    }
    if (path.endsWith('/connection-status')) return response({ status: 'Connected', lastRecordedAtUtc: '2026-10-07T09:00:00Z', connectedThresholdMinutes: 5, disconnectedThresholdMinutes: 15 });
    if (path.startsWith('/api/telemetry/sensors/')) {
      const selected = [sensor, power, ...registered].find((item) => path.endsWith('/' + item.id));
      const page = Number(url.searchParams.get('page'));
      return response({ unit: selected.unit, page, pageSize: 25, totalCount: 60, readings: [{ id: `${selected.id}-p${page}`, recordedAtUtc: '2026-10-07T09:00:00Z', receivedAtUtc: '2026-10-07T09:00:01Z', valueKind: selected.valueKind, floatValue: 5.1, integerValue: 320, booleanValue: false, isValid: false, validationMessage: 'Below the configured expected range.' }] });
    }
    if (path.endsWith('/attachments')) return response([]);
    if (path.startsWith('/api/sensors/')) {
      const selected = [sensor, power, ...registered].find((item) => path.endsWith('/' + item.id));
      return response(selected ?? { detail: 'Sensor not found.' }, selected ? 200 : 404);
    }
    throw new Error(`Unexpected endpoint: ${path}`);
  };
  root = createRoot(document.getElementById('root'));
  await act(async () => root.render(createElement(StrictMode, null, createElement(App))));
});

afterEach(async () => {
  await act(async () => root.unmount());
  await act(async () => delay(5));
  document.getElementById('root').replaceChildren();
});

function button(label, within = document) {
  const result = [...within.querySelectorAll('button')].find((item) => item.textContent.trim() === label || item.textContent.trim() === `← ${label}`);
  assert.ok(result, `Missing button: ${label}`);
  return result;
}

function field(label) {
  const element = [...document.querySelectorAll('label')].find((item) => item.querySelector('span')?.textContent.trim() === label);
  assert.ok(element, `Missing field: ${label}`);
  return element.querySelector('input,select');
}

async function click(label, within) {
  await act(async () => button(label, within).click());
}

async function navigate(label) {
  await click(label, document.querySelector('nav'));
}

async function input(label, value) {
  const element = field(label);
  const prototype = element.tagName === 'SELECT' ? HTMLSelectElement.prototype : HTMLInputElement.prototype;
  await act(async () => {
    Object.getOwnPropertyDescriptor(prototype, 'value').set.call(element, value);
    element.dispatchEvent(new Event(element.tagName === 'SELECT' ? 'change' : 'input', { bubbles: true }));
  });
}

async function waitUntil(predicate) {
  for (let attempt = 0; attempt < 100; attempt++) {
    if (predicate()) return;
    await act(async () => delay(10));
  }
  assert.ok(predicate(), 'Expected UI state did not appear');
}

async function openPh() {
  await navigate('Telemetry');
  await waitUntil(() => document.querySelector('.sensor-card'));
  const card = [...document.querySelectorAll('.sensor-card')].find((item) => item.querySelector('h2')?.textContent === sensor.friendlyName);
  await click('View sensor details', card);
  await waitUntil(() => document.querySelector('.pagination'));
}

test('App exposes enabled Part 1 and Part 2 pillars and keeps final topology disabled', async () => {
  assert.equal(button('Coming in Final PoE').disabled, true);
  await click('Open Command Stream');
  assert.match(document.querySelector('h1').textContent, /Real-Time Command Stream/);
  assert.equal(document.querySelectorAll('.command-shell-panel').length, 6);
  assert.equal(button('Undo unavailable').disabled, true);
  await navigate('Home');
  await click('Open Telemetry');
  assert.equal(document.querySelector('h1').textContent, 'Sensor directory');
});

test('Rendered filters and selected anomaly/history survive full module unmounts', async () => {
  await navigate('Telemetry');
  await input('Search sensors', 'Nutrient');
  await input('Category', 'Environmental');
  await waitUntil(() => field('Location').options.length > 1);
  await input('Location', location.id);
  await click('View sensor details');
  await waitUntil(() => document.querySelector('.pagination'));
  await input('Reading status', 'invalid');
  await click('Next');
  await waitUntil(() => document.querySelector('.pagination').textContent.includes('Page 2'));
  await act(async () => document.querySelector('.chart-anomaly').dispatchEvent(new MouseEvent('click', { bubbles: true })));
  assert.ok(document.querySelector('.anomaly-details'));
  await navigate('Command Stream');
  await navigate('Home');
  await navigate('Telemetry');
  await waitUntil(() => document.querySelector('.pagination')?.textContent.includes('Page 2'));
  assert.equal(field('Reading status').value, 'invalid');
  assert.ok(document.querySelector('.anomaly-details'));
  await click('Back to sensors');
  assert.equal(field('Search sensors').value, 'Nutrient');
  assert.equal(field('Category').value, 'Environmental');
  assert.equal(field('Location').value, location.id);
});

test('Command Stream selections remain separate from Telemetry selections', async () => {
  await navigate('Command Stream');
  await input('Search devices', 'pump');
  await input('Device category', 'Actuator');
  await input('Alert state', 'active');
  await input('History view', 'selected');
  await navigate('Telemetry');
  await input('Search sensors', 'Nutrient');
  await navigate('Home');
  await navigate('Command Stream');
  assert.equal(field('Search devices').value, 'pump');
  assert.equal(field('Device category').value, 'Actuator');
  assert.equal(field('Alert state').value, 'active');
  assert.equal(field('History view').value, 'selected');
});

test('Superseded slow responses are ignored even when the transport resolves after abort', async () => {
  override = async (url) => {
    if (url.pathname === '/api/sensors' && url.searchParams.get('search') === 'slow') {
      await delay(120);
      return response({ detail: 'Obsolete failure.' }, 500);
    }
  };
  await navigate('Telemetry');
  await input('Search sensors', 'slow');
  await input('Search sensors', 'Nutrient');
  await waitUntil(() => document.querySelector('.sensor-card h2')?.textContent === 'Nutrient pH');
  await act(async () => delay(160));
  assert.equal(document.querySelector('.error-panel'), null);
  const slow = requests.find((request) => request.url.searchParams.get('search') === 'slow');
  assert.equal(slow.signal.aborted, true);
});

test('Repeated navigation aborts inactive reads and preserves a usable Telemetry workspace', async () => {
  override = async (url) => {
    if (url.pathname === '/api/sensors') await delay(60);
  };
  await navigate('Telemetry');
  for (let round = 0; round < 10; round++) {
    await navigate('Command Stream');
    await navigate('Telemetry');
  }
  await waitUntil(() => document.querySelector('.sensor-card'));
  assert.equal(document.querySelector('.error-panel'), null);
  const reads = requests.filter((request) => request.url.pathname === '/api/sensors');
  assert.ok(reads.some((request) => request.signal.aborted));
  assert.ok(reads.some((request) => !request.signal.aborted));
});

test('Registration draft and Boolean validation survive navigation and still post through the API', async () => {
  await navigate('Telemetry');
  await waitUntil(() => document.querySelector('.sensor-card'));
  await click('Register sensor');
  await input('Friendly name', 'Pump B');
  await input('MAC address', 'a4:cf:12:8b:40:99');
  await input('Measured property', 'Pump state');
  await input('Deployment location', location.id);
  await input('Expected minimum', '10');
  await input('Expected maximum', '2');
  await input('Telemetry value type', 'Boolean');
  await navigate('Command Stream');
  await navigate('Telemetry');
  assert.equal(field('Friendly name').value, 'Pump B');
  assert.equal(field('Expected minimum').disabled, true);
  await click('Register sensor');
  await waitUntil(() => document.querySelector('.detail-header h2')?.textContent === 'Pump B');
  assert.equal(registered.length, 1);
  assert.equal(registered[0].expectedMinimum, null);
  assert.equal(registered[0].macAddress, 'A4:CF:12:8B:40:99');
});

test('Pending registration survives navigation without duplicate submissions or a forced module change', async () => {
  override = async (url, options) => {
    if (url.pathname === '/api/sensors' && options.method === 'POST') await delay(120);
  };
  await navigate('Telemetry');
  await waitUntil(() => document.querySelector('.sensor-card'));
  await click('Register sensor');
  await input('Friendly name', 'Pending Pump');
  await input('MAC address', 'A4:CF:12:8B:40:98');
  await input('Measured property', 'State');
  await input('Deployment location', location.id);
  const submit = document.querySelector('form');
  await act(async () => submit.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })));
  await navigate('Command Stream');
  await navigate('Telemetry');
  assert.equal(button('Registering...').disabled, true);
  await navigate('Command Stream');
  await act(async () => delay(160));
  assert.match(document.querySelector('h1').textContent, /Real-Time Command Stream/);
  await navigate('Telemetry');
  await waitUntil(() => document.querySelector('.detail-header h2')?.textContent === 'Pending Pump');
  assert.equal(registered.length, 1);
});

test('Anomaly keyboard selection works and unconfigured ranges are omitted', async () => {
  await openPh();
  await act(async () => document.querySelector('.chart-anomaly').dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true })));
  assert.match(document.querySelector('.anomaly-details').textContent, /Below the configured expected range/);
  await click('Back to sensors');
  await waitUntil(() => document.querySelectorAll('.sensor-card').length === 2);
  const card = [...document.querySelectorAll('.sensor-card')].find((item) => item.querySelector('h2').textContent === power.friendlyName);
  await click('View sensor details', card);
  await waitUntil(() => document.querySelector('.telemetry-chart'));
  assert.equal(document.querySelector('.chart-range-band'), null);
});

test('API failure does not block module navigation and retry recovers the directory', async () => {
  let failed = true;
  override = async (url) => url.pathname === '/api/sensors' && failed ? response({ detail: 'Gateway temporarily unavailable.' }, 503) : undefined;
  await navigate('Telemetry');
  await waitUntil(() => document.querySelector('.error-panel'));
  await navigate('Command Stream');
  await navigate('Telemetry');
  await waitUntil(() => document.querySelector('.error-panel'));
  failed = false;
  await click('Try again', document.querySelector('.error-panel'));
  await waitUntil(() => document.querySelector('.sensor-card'));
  assert.equal(document.querySelector('.error-panel'), null);
});
