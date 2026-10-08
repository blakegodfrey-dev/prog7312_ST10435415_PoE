import assert from 'node:assert/strict';
import { before, after, test } from 'node:test';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { setTimeout as delay } from 'node:timers/promises';
import { chromium } from 'playwright';

const clientRoot = fileURLToPath(new URL('../../', import.meta.url));
const baseUrl = 'http://127.0.0.1:4174';
const apiBaseUrl = 'http://127.0.0.1:5075';
let browser;
let server;
let serverLog = '';

before(async () => {
  server = spawn(process.execPath, ['node_modules/vite/bin/vite.js', '--host', '127.0.0.1', '--port', '4174', '--strictPort'], {
    cwd: clientRoot,
    env: { ...process.env, VITE_API_BASE_URL: apiBaseUrl },
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  server.stdout.on('data', (chunk) => { serverLog += chunk; });
  server.stderr.on('data', (chunk) => { serverLog += chunk; });
  let ready = false;
  for (let attempt = 0; attempt < 100; attempt++) {
    try {
      if ((await fetch(baseUrl)).ok) { ready = true; break; }
    } catch { /* The development server is still starting. */ }
    if (server.exitCode != null) break;
    await delay(100);
  }
  assert.ok(ready, `Vite could not start: ${serverLog}`);
  browser = await chromium.launch({
    headless: true,
    ...(process.env.SMARTX_CHROMIUM_EXECUTABLE ? { executablePath: process.env.SMARTX_CHROMIUM_EXECUTABLE } : {}),
  });
});

after(async () => {
  await browser?.close();
  if (server && server.exitCode == null) {
    const exited = new Promise((resolve) => server.once('exit', resolve));
    server.kill();
    await exited;
  }
});

const location = { id: 'node-reservoir', name: 'Reservoir 1', code: 'RES-1', nodeType: 'Node' };
const sensors = [
  { id: 'sensor-ph', friendlyName: 'Nutrient pH', macAddress: 'A4:CF:12:8B:40:01', category: 'Environmental', measuredProperty: 'Nutrient pH', valueKind: 'Float', unit: 'pH', expectedMinimum: 5.5, expectedMaximum: 6.5, deploymentLocation: location },
  { id: 'sensor-pump', friendlyName: 'Circulation Pump', macAddress: 'A4:CF:12:8B:40:02', category: 'Actuator', measuredProperty: 'Pump State', valueKind: 'Boolean', unit: '', expectedMinimum: null, expectedMaximum: null, deploymentLocation: location },
  { id: 'sensor-power', friendlyName: 'Pump Power Draw', macAddress: 'A4:CF:12:8B:40:03', category: 'PowerConsumption', measuredProperty: 'Power', valueKind: 'Integer', unit: 'W', expectedMinimum: null, expectedMaximum: null, deploymentLocation: location },
];

async function fixture(t, options = {}) {
  const context = await browser.newContext();
  const page = await context.newPage();
  const errors = [];
  const requests = [];
  const registered = [];
  const attachments = [];
  let navigations = 0;
  page.on('pageerror', (error) => errors.push(error.message));
  page.on('framenavigated', (frame) => { if (frame === page.mainFrame()) navigations++; });
  page.setDefaultTimeout(5000);
  await page.route(`${apiBaseUrl}/**`, async (route) => {
    const request = route.request();
    const fulfill = (response) => route.fulfill({
      ...response,
      headers: { 'access-control-allow-origin': '*', ...response.headers },
    });
    if (request.method() === 'OPTIONS') {
      return fulfill({ status: 204, headers: {
        'access-control-allow-methods': 'GET,POST,DELETE,OPTIONS',
        'access-control-allow-headers': 'content-type,accept',
      } });
    }
    const url = new URL(request.url());
    const path = url.pathname;
    requests.push({ path, method: request.method(), query: url.searchParams.toString() });
    const override = await options.override?.(request, url);
    if (override) {
      await fulfill(override);
      return;
    }
    const json = async (value, status = 200) => fulfill({ status, contentType: 'application/json', body: JSON.stringify(value) });
    if (path === '/api/operations/dashboard') return json({ devices: [], activeIncidents: [], events: [], processing: {}, undoCount: 0, commands: [], history: [], suggestions: [], recentCapacity: 2000 });
    if (path === '/api/health') return json({ status: 'Healthy' });
    if (path === '/api/deployment-nodes') return json([location]);
    if (path === '/api/telemetry/diagnostics/health-summary') return json({ totalSensorCount: 3, connectedSensorCount: 3, staleSensorCount: 0, disconnectedSensorCount: 0, invalidLatestReadingCount: 1, noDataSensorCount: 0, connectedThresholdMinutes: 5, disconnectedThresholdMinutes: 15, evaluatedAtUtc: '2026-10-07T09:00:00Z' });
    if (path === '/api/sensors' && request.method() === 'POST') {
      const body = request.postDataJSON();
      const sensor = { ...body, deploymentLocation: location };
      registered.push(sensor);
      return json(sensor, 201);
    }
    if (path === '/api/sensors') {
      const query = (url.searchParams.get('search') ?? '').toLowerCase();
      const category = url.searchParams.get('category');
      const deployment = url.searchParams.get('deploymentNodeId');
      return json([...sensors, ...registered].filter((sensor) => (!query || `${sensor.friendlyName} ${sensor.macAddress} ${sensor.measuredProperty}`.toLowerCase().includes(query)) && (!category || sensor.category === category) && (!deployment || sensor.deploymentLocation.id === deployment)));
    }
    if (path.endsWith('/connection-status')) return json({ status: 'Connected', lastRecordedAtUtc: '2026-10-07T09:00:00Z', connectedThresholdMinutes: 5, disconnectedThresholdMinutes: 15 });
    if (path.startsWith('/api/telemetry/sensors/')) {
      const sensor = [...sensors, ...registered].find((item) => path.endsWith(`/${item.id}`));
      const pageNumber = Number(url.searchParams.get('page') ?? 1);
      const validity = url.searchParams.get('isValid');
      const kinds = { Float: { floatValue: 5.1 }, Integer: { integerValue: 320 }, Boolean: { booleanValue: false } };
      const readings = [0, 1].map((index) => ({ id: `${sensor.id}-p${pageNumber}-${index}`, recordedAtUtc: `2026-10-07T09:0${index}:00Z`, receivedAtUtc: `2026-10-07T09:0${index}:01Z`, valueKind: sensor.valueKind, ...kinds[sensor.valueKind], isValid: validity === 'false' ? false : validity === 'true' ? true : index === 1, validationMessage: 'Below the configured expected range.' }));
      return json({ sensorId: sensor.id, unit: sensor.unit, valueKind: sensor.valueKind, totalCount: 60, page: pageNumber, pageSize: 25, readings });
    }
    if (path.endsWith('/attachments') && request.method() === 'POST') {
      assert.ok(request.postData().includes('device.json'));
      const attachment = { id: 'attachment-1', originalFileName: 'device.json', category: 'ConfigurationFile', sizeBytes: 12, uploadedAtUtc: '2026-10-07T09:00:00Z' };
      attachments.push(attachment);
      return json(attachment, 201);
    }
    if (path.endsWith('/attachments')) return json(attachments);
    if (path.endsWith('/attachments/attachment-1') && request.method() === 'DELETE') {
      attachments.length = 0;
      return fulfill({ status: 204 });
    }
    if (path.endsWith('/attachments/attachment-1/content')) return fulfill({ status: 200, contentType: 'application/json', body: '{"node":"1"}' });
    if (path.startsWith('/api/sensors/')) {
      const sensor = [...sensors, ...registered].find((item) => path.endsWith(`/${item.id}`));
      return sensor ? json(sensor) : json({ detail: 'Sensor not found.' }, 404);
    }
    return json({ detail: `Unexpected test endpoint ${path}` }, 404);
  });
  t.after(async () => {
    await context.close();
    assert.deepEqual(errors, [], 'No uncaught browser errors during the workflow');
  });
  await page.goto(baseUrl);
  return { page, requests, registered, attachments, navigationCount: () => navigations };
}

function nav(page, label) {
  return page.getByRole('navigation', { name: 'Smart-X modules' }).getByRole('button', { name: label, exact: true });
}

async function openPh(page) {
  await nav(page, 'Telemetry').click();
  await page.getByRole('article').filter({ has: page.getByRole('heading', { name: 'Nutrient pH', exact: true }) }).getByRole('button').click();
  await page.getByRole('heading', { name: 'Telemetry history', exact: true }).waitFor();
  await page.getByRole('button', { name: 'Next', exact: true }).waitFor();
}

async function value(locator, expected) {
  assert.equal(await locator.inputValue(), expected);
}

test('Home enables both active pillars while topology stays visibly disabled', async (t) => {
  const { page, navigationCount } = await fixture(t);
  assert.equal(await page.getByRole('button', { name: 'Coming in Final PoE' }).isDisabled(), true);
  await page.getByRole('button', { name: 'Open Command Stream' }).click();
  await page.getByRole('heading', { name: 'Real-Time Command Stream and History', exact: true }).waitFor();
  assert.equal(await nav(page, 'Command Stream').getAttribute('aria-current'), 'page');
  assert.equal(await page.getByRole('button', { name: 'Undo latest successful command' }).isDisabled(), true);
  assert.equal(await page.locator('.command-shell-panel').count(), 7);
  await nav(page, 'Home').click();
  await page.getByRole('button', { name: 'Open Telemetry' }).click();
  await page.getByRole('heading', { name: 'Sensor directory' }).waitFor();
  assert.equal(navigationCount(), 1, 'Module changes must not reload the document');
});

test('Directory filters, selected sensor, validity, page and anomaly survive a round trip', async (t) => {
  const { page, navigationCount } = await fixture(t);
  await nav(page, 'Telemetry').click();
  await page.getByLabel('Search sensors').fill('Nutrient');
  await page.getByLabel('Category', { exact: true }).selectOption('Environmental');
  await page.getByLabel('Location', { exact: true }).selectOption(location.id);
  await page.getByRole('button', { name: 'View sensor details' }).click();
  await page.getByLabel('Reading status').selectOption('invalid');
  await page.getByRole('button', { name: 'Next', exact: true }).click();
  await page.getByText('Page 2 of 3', { exact: false }).waitFor();
  await page.getByRole('button', { name: /^Invalid reading\./ }).first().click();
  await page.getByRole('complementary', { name: 'Selected anomaly details' }).waitFor();
  await nav(page, 'Command Stream').click();
  await nav(page, 'Home').click();
  await nav(page, 'Telemetry').click();
  await page.getByText('Page 2 of 3', { exact: false }).waitFor();
  await value(page.getByLabel('Reading status'), 'invalid');
  await page.getByRole('complementary', { name: 'Selected anomaly details' }).waitFor();
  await page.getByRole('button', { name: 'Back to sensors', exact: false }).click();
  await value(page.getByLabel('Search sensors'), 'Nutrient');
  await value(page.getByLabel('Category', { exact: true }), 'Environmental');
  await value(page.getByLabel('Location', { exact: true }), location.id);
  assert.equal(navigationCount(), 1);
});

test('Command Stream filters and history choice survive Telemetry and Home independently', async (t) => {
  const { page } = await fixture(t);
  await nav(page, 'Command Stream').click();
  await page.getByLabel('Search devices').fill('pump');
  await page.getByLabel('Device category').selectOption('Actuator');
  await page.getByLabel('Alert state').selectOption('active');
  await page.getByLabel('History view').selectOption('selected');
  await nav(page, 'Telemetry').click();
  await page.getByLabel('Search sensors').fill('Nutrient');
  await nav(page, 'Home').click();
  await nav(page, 'Command Stream').click();
  await value(page.getByLabel('Search devices'), 'pump');
  await value(page.getByLabel('Device category'), 'Actuator');
  await value(page.getByLabel('Alert state'), 'active');
  await value(page.getByLabel('History view'), 'selected');
  await nav(page, 'Telemetry').click();
  await value(page.getByLabel('Search sensors'), 'Nutrient');
});

test('Rapid switching with slow and superseded requests remains responsive', async (t) => {
  const { page, navigationCount } = await fixture(t, { override: async (request, url) => {
    if (url.pathname === '/api/sensors' && request.method() === 'GET' && url.searchParams.get('search') === 'slow') {
      await delay(400);
      return { status: 500, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Old slow request failed.' }) };
    }
  } });
  await nav(page, 'Telemetry').click();
  const slow = page.waitForRequest((request) => new URL(request.url()).searchParams.get('search') === 'slow');
  await page.getByLabel('Search sensors').fill('slow');
  await slow;
  for (let index = 0; index < 8; index++) {
    await nav(page, 'Command Stream').click();
    await nav(page, 'Telemetry').click();
  }
  await page.getByLabel('Search sensors').fill('Nutrient');
  await page.getByRole('heading', { name: 'Nutrient pH', exact: true }).waitFor();
  await delay(450);
  assert.equal(await page.getByRole('heading', { name: 'Unable to load sensors' }).count(), 0);
  assert.equal(navigationCount(), 1);
});

test('Registration draft survives navigation and successful submission still opens its profile', async (t) => {
  const { page, registered } = await fixture(t);
  await nav(page, 'Telemetry').click();
  await page.getByRole('heading', { name: 'Nutrient pH', exact: true }).waitFor();
  await page.getByRole('button', { name: 'Register sensor', exact: true }).click();
  await page.getByLabel('Friendly name').fill('Pump B');
  await page.getByLabel('MAC address').fill('a4:cf:12:8b:40:99');
  await page.getByLabel('Category', { exact: true }).selectOption('Actuator');
  await page.getByLabel('Measured property').fill('Pump state');
  await page.getByLabel('Expected minimum').fill('10');
  await page.getByLabel('Expected maximum').fill('2');
  await page.getByLabel('Telemetry value type').selectOption('Boolean');
  await page.getByLabel('Deployment location').selectOption(location.id);
  await nav(page, 'Command Stream').click();
  await nav(page, 'Telemetry').click();
  await value(page.getByLabel('Friendly name'), 'Pump B');
  await value(page.getByLabel('Telemetry value type'), 'Boolean');
  await page.getByRole('button', { name: 'Register sensor', exact: true }).click();
  await page.getByRole('heading', { name: 'Pump B', exact: true }).waitFor();
  assert.equal(registered.length, 1);
  assert.equal(registered[0].expectedMinimum, null);
  assert.equal(registered[0].expectedMaximum, null);
  assert.equal(registered[0].macAddress, 'A4:CF:12:8B:40:99');
});

test('An in-flight registration retains its busy state across module changes', async (t) => {
  const { page } = await fixture(t, { override: async (request, url) => {
    if (url.pathname === '/api/sensors' && request.method() === 'POST') await delay(700);
  } });
  await nav(page, 'Telemetry').click();
  await page.getByRole('heading', { name: 'Nutrient pH', exact: true }).waitFor();
  await page.getByRole('button', { name: 'Register sensor', exact: true }).click();
  await page.getByLabel('Friendly name').fill('Pending Pump');
  await page.getByLabel('MAC address').fill('A4:CF:12:8B:40:98');
  await page.getByLabel('Measured property').fill('State');
  await page.getByLabel('Deployment location').selectOption(location.id);
  await page.getByRole('button', { name: 'Register sensor', exact: true }).click();
  await nav(page, 'Command Stream').click();
  await nav(page, 'Telemetry').click();
  assert.equal(await page.getByRole('button', { name: 'Registering...' }).isDisabled(), true);
  await nav(page, 'Command Stream').click();
  await delay(800);
  await page.getByRole('heading', { name: 'Real-Time Command Stream and History', exact: true }).waitFor();
  await nav(page, 'Telemetry').click();
  await page.getByRole('heading', { name: 'Pending Pump', exact: true }).waitFor();
});

test('Anomaly marker is keyboard accessible and absent bounds do not become a zero range', async (t) => {
  const { page } = await fixture(t);
  await openPh(page);
  const marker = page.getByRole('button', { name: /^Invalid reading\./ }).first();
  await marker.focus();
  await page.keyboard.press('Enter');
  await page.getByRole('complementary', { name: 'Selected anomaly details' }).waitFor();
  assert.ok(await page.getByRole('complementary', { name: 'Selected anomaly details' }).getByText('Below the configured expected range.').isVisible());
  await page.getByRole('button', { name: 'Back to sensors', exact: false }).click();
  await page.getByRole('article').filter({ has: page.getByRole('heading', { name: 'Pump Power Draw', exact: true }) }).getByRole('button').click();
  await page.getByRole('img', { name: /^Recent telemetry trend/ }).waitFor();
  assert.equal(await page.locator('.chart-range-band').count(), 0);
});

test('Attachment validation, upload, download and delete remain usable after navigation', async (t) => {
  const { page } = await fixture(t);
  await openPh(page);
  await nav(page, 'Command Stream').click();
  await nav(page, 'Telemetry').click();
  const file = page.locator('input[type=file]');
  await file.setInputFiles({ name: 'bad.exe', mimeType: 'application/octet-stream', buffer: Buffer.from('bad') });
  await page.getByRole('button', { name: 'Upload Configuration file' }).click();
  await page.locator('#attachment-error').waitFor();
  await file.setInputFiles({ name: 'device.json', mimeType: 'application/json', buffer: Buffer.from('{"node":"1"}') });
  await page.getByRole('button', { name: 'Upload Configuration file' }).click();
  const list = page.locator('.attachment-list');
  await list.getByText('device.json', { exact: true }).waitFor();
  const download = page.waitForEvent('download');
  await list.getByRole('button', { name: 'Download' }).click();
  assert.equal((await download).suggestedFilename(), 'device.json');
  page.once('dialog', (dialog) => dialog.accept());
  await list.getByRole('button', { name: 'Delete' }).click();
  await page.getByText('No files have been attached to this sensor.').waitFor();
});

test('API errors allow navigation and a retry recovers without restarting the client', async (t) => {
  let unavailable = true;
  const { page, navigationCount } = await fixture(t, { override: async (request, url) => {
    if (url.pathname === '/api/sensors' && request.method() === 'GET' && unavailable) return { status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Gateway temporarily unavailable.', traceId: 'test-trace' }) };
  } });
  await nav(page, 'Telemetry').click();
  await page.getByRole('heading', { name: 'Unable to load sensors' }).waitFor();
  await nav(page, 'Command Stream').click();
  await nav(page, 'Telemetry').click();
  await page.getByRole('heading', { name: 'Unable to load sensors' }).waitFor();
  unavailable = false;
  await page.getByRole('button', { name: 'Try again', exact: true }).click();
  await page.getByRole('heading', { name: 'Nutrient pH', exact: true }).waitFor();
  assert.equal(navigationCount(), 1);
});

test('Mobile navigation and shell fit the viewport', async (t) => {
  const { page } = await fixture(t);
  await page.setViewportSize({ width: 390, height: 844 });
  await nav(page, 'Command Stream').click();
  assert.ok(await page.getByLabel('Search devices').isVisible());
  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true);
  if (process.env.SMARTX_SCREENSHOT_DIRECTORY) {
    const { mkdir } = await import('node:fs/promises');
    await mkdir(process.env.SMARTX_SCREENSHOT_DIRECTORY, { recursive: true });
    await page.screenshot({ path: `${process.env.SMARTX_SCREENSHOT_DIRECTORY}/command-stream-mobile.png`, fullPage: true });
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.screenshot({ path: `${process.env.SMARTX_SCREENSHOT_DIRECTORY}/command-stream-desktop.png`, fullPage: true });
  }
});
