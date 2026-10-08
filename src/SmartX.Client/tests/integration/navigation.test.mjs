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
    if (path === '/api/operations/dashboard') return response(operationSnapshot());
    if (path === '/api/operations/interactions') return response({});
    if (path === '/api/health') {
      if (process.env.SMARTX_REAL_API_HEALTH_URL) return originalFetch(process.env.SMARTX_REAL_API_HEALTH_URL, options);
      return response({ status: 'Healthy' });
    }
    if (path === '/api/deployment-nodes') return response([location]);
    if (path === '/api/telemetry/diagnostics/health-summary') return response({ totalSensorCount: 2, connectedSensorCount: 2, staleSensorCount: 0, disconnectedSensorCount: 0, invalidLatestReadingCount: 1, noDataSensorCount: 0, connectedThresholdMinutes: .5, disconnectedThresholdMinutes: 1.5, staleSeconds: 30, disconnectedSeconds: 90, unknownSensorCount: 0, evaluatedAtUtc: '2026-10-07T09:00:00Z' });
    if (path === '/api/sensors' && options.method === 'POST') {
      const result = { ...JSON.parse(options.body), deploymentLocation: location };
      registered.push(result);
      return response(result, 201);
    }
    if (path === '/api/sensors') {
      const query = (url.searchParams.get('search') ?? '').toLowerCase();
      return response([sensor, power, ...registered].filter((item) => !query || item.friendlyName.toLowerCase().includes(query)));
    }
    if (path.endsWith('/connection-status')) return response({ status: 'Connected', lastSeenAtUtc: '2026-10-07T09:00:01Z', lastRecordedAtUtc: '2026-10-07T09:00:00Z', connectedThresholdMinutes: .5, disconnectedThresholdMinutes: 1.5, staleSeconds: 30, disconnectedSeconds: 90, unknownSensorCount: 0 });
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
  assert.equal(document.querySelectorAll('.command-shell-panel').length, 7);
  assert.equal(button('Undo latest successful command').disabled, true);
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

function operationSnapshot() {
  const pump = { ...sensor, id: 'pump', friendlyName: 'Circulation Pump', category: 'Actuator', valueKind: 'Boolean', measuredProperty: 'Pump state', unit: '', expectedMinimum: null, expectedMaximum: null, latestReading: { booleanValue: false, isValid: true, recordedAtUtc: '2026-10-07T09:00:00Z' } };
  return { devices: [{ device: pump, connection: { state: 'Connected', lastSeenAtUtc: '2026-10-07T09:00:00Z' } }], activeIncidents: [], events: [], processing: { normalCount: 0, priorityCount: 0, pendingReadings: 0, workerState: 'Waiting', completed: 2, failed: 0, rejected: 0 }, undoCount: 0, commands: [], history: [], recentCapacity: 2000, staleSeconds: 30, suggestions: [] };
}

test('Operational dashboard sends manual commands and retains pending feedback across navigation', async () => {
  let resolveCommand;
  override = async (url) => {
    if (url.pathname === '/api/operations/commands') return new Promise(resolve => { resolveCommand = resolve; });
    if (url.pathname === '/api/operations/interactions') return response({});
  };
  await navigate('Command Stream');
  await waitUntil(() => document.querySelector('.operation-device'));
  await act(async () => document.querySelector('.operation-device').dispatchEvent(new MouseEvent('click', { bubbles: true })));
  assert.equal(button('Set Off').disabled, true);
  await click('Set On');
  await waitUntil(() => resolveCommand);
  assert.equal(button('Set On').disabled, true);
  assert.equal(requests.filter(r => r.url.pathname === '/api/operations/commands').length, 1);
  const body = JSON.parse(requests.find(r => r.url.pathname === '/api/operations/commands').body);
  assert.equal(body.sensorId, 'pump'); assert.equal(body.desiredState, true);
  await navigate('Home');
  await act(async () => { resolveCommand(response({ successful: true, message: 'Acknowledged' })); await delay(10); });
  await navigate('Command Stream');
  await waitUntil(() => document.body.textContent.includes('Success: Acknowledged'));
  assert.match(document.body.textContent, /No supported pattern/);
});

test('Operational retry recovers and cold-start data never enables unsafe controls', async () => {
  override = url => url.pathname === '/api/operations/dashboard' ? response({ detail: 'Gateway unavailable' }, 503) : null;
  await navigate('Command Stream');
  await waitUntil(() => document.querySelector('[role="alert"]'));
  assert.equal(button('Undo latest successful command').disabled, true);
  override = null; await click('Retry');
  await waitUntil(() => document.querySelector('.operation-device'));
  assert.equal(document.querySelector('[role="alert"]'), null);
});

test('Learned actions require approval and Undo goes through the validated API', async () => {
  const data = operationSnapshot();
  data.undoCount = 1;
  data.suggestions = [{ targetId: 'pump', action: 'command', desiredState: true, support: 3, contextObservations: 4, confidence: .75, reason: 'Operators activate this pump after low moisture.' }];
  override = (url) => {
    if (url.pathname === '/api/operations/dashboard') return response(data);
    if (url.pathname === '/api/operations/commands') return response({ successful: true, message: 'Approved and committed' });
    if (url.pathname === '/api/operations/undo') return response({ successful: true, message: 'Previous state restored' });
  };
  await navigate('Command Stream');
  await waitUntil(() => document.body.textContent.includes('Confidence: 75%'));
  assert.equal(requests.filter(r => r.url.pathname.endsWith('/commands')).length, 0);
  await click('Approve: set On');
  await waitUntil(() => document.body.textContent.includes('Approved and committed'));
  const command = JSON.parse(requests.find(r => r.url.pathname.endsWith('/commands')).body);
  assert.equal(command.desiredState, true); assert.equal(command.sensorId, 'pump');
  await click('Undo latest successful command');
  await waitUntil(() => document.body.textContent.includes('Previous state restored'));
  assert.equal(requests.filter(r => r.url.pathname.endsWith('/undo')).length, 1);
});

test('Operational anomaly drill-down shows native zero, range context and missing intervals', async () => {
  const data = operationSnapshot();
  const abnormal = { ...sensor, latestReading: { floatValue: 0, valueKind: 'Float', isValid: false, recordedAtUtc: '2026-10-07T09:02:00Z' } };
  data.devices = [{ device: abnormal, connection: { state: 'Connected', lastSeenAtUtc: '2026-10-07T09:02:00Z' } }];
  data.history = [
    { id: 'p1', sensorId: sensor.id, valueKind: 'Float', floatValue: 6, isValid: true, recordedAtUtc: '2026-10-07T09:00:00Z', receivedAtUtc: '2026-10-07T09:00:01Z' },
    { id: 'p2', sensorId: sensor.id, valueKind: 'Float', floatValue: 0, isValid: false, validationMessage: 'Below configured minimum', recordedAtUtc: '2026-10-07T09:02:00Z', receivedAtUtc: '2026-10-07T09:02:01Z' },
  ];
  data.activeIncidents = [{ id: 'alarm', deviceId: sensor.id, type: 'OutOfRange', severity: 'critical', reason: 'pH below expected range', observations: 2, openedAtUtc: '2026-10-07T09:02:01Z', label: 'unresolved', note: null }];
  override = url => url.pathname === '/api/operations/dashboard' ? response(data) : null;
  await navigate('Command Stream');
  await waitUntil(() => document.querySelector('.operation-device'));
  await input('Alert state', 'critical');
  await act(async () => document.querySelector('.operation-device').dispatchEvent(new MouseEvent('click', { bubbles: true })));
  assert.match(document.body.textContent, /Missing interval/);
  assert.match(document.body.textContent, /Expected 5.5–6.5 pH/);
  assert.equal(document.querySelectorAll('.operation-chart polyline').length, 2);
  await act(async () => document.querySelector('.operation-chart circle[aria-label^="Anomaly"]').dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true })));
  assert.match(document.body.textContent, /Selected anomaly/);
  assert.match(document.body.textContent, /Below configured minimum/);
  assert.equal(document.querySelectorAll('.operation-incident').length, 1);
});

test('Telemetry health polls gateway status without resetting filters, page or selected anomaly', async () => {
  let silent = false;
  override = url => {
    if (url.pathname.endsWith('/connection-status')) return response({
      status: silent ? 'Disconnected' : 'Connected', lastRecordedAtUtc: '2026-10-07T09:00:00Z',
      lastSeenAtUtc: '2026-10-07T09:00:01Z', staleSeconds: 30, disconnectedSeconds: 90 });
  };
  await openPh();
  await input('Reading status', 'invalid');
  await click('Next');
  await waitUntil(() => document.querySelector('.pagination')?.textContent.includes('Page 2'));
  await act(async () => document.querySelector('.chart-anomaly').dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true })));
  assert.ok(document.querySelector('.anomaly-details'));
  assert.match(document.querySelector('.connection-summary').textContent, /Gateway last seen/);
  assert.match(document.querySelector('.connection-summary').textContent, /Connected <30 s/);
  silent = true;
  await act(async () => delay(2100));
  await waitUntil(() => document.querySelector('.connection-badge')?.textContent.trim() === 'Disconnected');
  assert.equal(field('Reading status').value, 'invalid');
  assert.match(document.querySelector('.pagination').textContent, /Page 2/);
  assert.ok(document.querySelector('.anomaly-details'));
  await navigate('Command Stream');
  const count = requests.filter(r => r.url.pathname.endsWith('/connection-status')).length;
  await act(async () => delay(2100));
  assert.equal(requests.filter(r => r.url.pathname.endsWith('/connection-status')).length, count);
});

test('Facility health refreshes shared second-based thresholds without a manual reload', async () => {
  let silent = false;
  override = url => {
    if (url.pathname === '/api/telemetry/diagnostics/health-summary') return response({
      totalSensorCount: 2, connectedSensorCount: silent ? 0 : 2, staleSensorCount: 0,
      disconnectedSensorCount: silent ? 2 : 0, invalidLatestReadingCount: 0,
      noDataSensorCount: 0, unknownSensorCount: 0, staleSeconds: 30, disconnectedSeconds: 90 });
  };
  await navigate('Telemetry');
  await waitUntil(() => document.querySelector('.health-card-connected .health-count')?.textContent === '2');
  assert.match(document.querySelector('.health-card-connected').textContent, /30 seconds/);
  assert.match(document.querySelector('.health-card-disconnected').textContent, /90 seconds/);
  silent = true;
  await act(async () => delay(2100));
  await waitUntil(() => document.querySelector('.health-card-disconnected .health-count')?.textContent === '2');
  assert.equal(document.querySelector('.health-card-connected .health-count').textContent, '0');
});
