import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { setTimeout as delay } from 'node:timers/promises';

// node scripts/simulate-operations.mjs http://localhost:5075 [normal|priority|lifecycle|learning|all]
// Runs against a test gateway. Configure Operations__ProcessingDelayMilliseconds=100
// for priority; lifecycle uses the gateway's configured timings. All data enters HTTP.
const base = (process.argv[2] ?? 'http://localhost:5075').replace(/\/$/, '');
const mode = process.argv[3] ?? 'normal';
assert.ok(['normal', 'priority', 'lifecycle', 'learning', 'all'].includes(mode));
async function request(path, body, method = 'POST', expected = 200) {
  const response = await fetch(`${base}${path}`, { method, ...(body !== undefined ? { body: JSON.stringify(body), headers: { 'content-type': 'application/json' } } : {}) });
  const text = await response.text(); const data = text ? JSON.parse(text) : null;
  assert.equal(response.status, expected, `${path}: ${text}`); return data;
}
const dashboard = () => request('/api/operations/dashboard', undefined, 'GET');
const nodes = await request('/api/deployment-nodes', undefined, 'GET');
const node = nodes.find(n => n.nodeType === 'Node'); assert.ok(node, 'Seed a Node deployment before running the simulation.');
const suffix = randomUUID().replaceAll('-', '').slice(0, 6).toUpperCase();
const mac = tail => `02:${suffix.slice(0, 2)}:${suffix.slice(2, 4)}:${suffix.slice(4, 6)}:00:${tail}`;
async function register(name, category, property, kind, unit, minimum, maximum, tail) {
  const sensor = { id: randomUUID(), macAddress: mac(tail), friendlyName: `Simulation ${name} ${suffix}`, category, measuredProperty: property, valueKind: kind, unit, deploymentNodeId: node.id, expectedMinimum: minimum, expectedMaximum: maximum };
  await request('/api/sensors', sensor, 'POST', 201); return sensor;
}
const moisture = await register('Moisture B', 'Environmental', 'Soil moisture', 'Float', '%', 30, 70, '01');
const pump = await register('Pump A', 'Actuator', 'Pump running', 'Boolean', '', null, null, '02');
const power = await register('Meter', 'PowerConsumption', 'Electrical load', 'Integer', 'W', 0, 500, '03');
async function reading(sensor, value, expected = 201) {
  return request(`/api/telemetry/${sensor.valueKind.toLowerCase()}`, { id: randomUUID(), sensorId: sensor.id, value, recordedAtUtc: new Date(Date.now() - 100).toISOString() }, 'POST', expected);
}
async function command(value) {
  const result = await request('/api/operations/commands', { id: randomUUID(), sensorId: pump.id, desiredState: value });
  assert.equal(result.successful, true, result.message); return result;
}
async function heartbeat(sensor) { return request(`/api/operations/heartbeat/${sensor.id}`, {}); }
await reading(moisture, 50); await reading(power, 0); await reading(pump, false);
assert.equal((await dashboard()).devices.find(d => d.device.id === pump.id).device.latestReading.booleanValue, false);
await command(true); await delay(5);
assert.equal((await request('/api/operations/undo', { id: randomUUID() })).successful, true);
await reading(moisture, false, 400); await reading(moisture, 50);
console.log('PASS normal: float/int/bool, real command + Undo, type rejection and recovery.');

if (mode === 'priority' || mode === 'all') {
  let finished = 0; const pending = Array.from({ length: 40 }, () => reading(moisture, 50).then(() => { finished++; }));
  let ready = false;
  for (let i = 0; i < 80; i++) { if ((await dashboard()).processing.normalCount >= 10) { ready = true; break; } await delay(10); }
  assert.ok(ready, 'Backlog not observed. Set Operations__ProcessingDelayMilliseconds=100 and queue capacity >= 100.');
  await reading(power, 1000);
  assert.ok(finished < 40, 'Critical item must complete while normal items still wait.');
  await Promise.all(pending);
  assert.ok((await dashboard()).activeIncidents.some(i => i.deviceId === power.id && i.severity === 'critical'));
  await reading(power, 200); console.log('PASS priority: critical power packet bypassed waiting normal traffic.');
}
if (mode === 'lifecycle' || mode === 'all') {
  await reading(moisture, 5); const first = (await dashboard()).activeIncidents.find(i => i.deviceId === moisture.id && i.type === 'OutOfRange');
  await reading(moisture, 5); assert.equal((await dashboard()).activeIncidents.find(i => i.deviceId === moisture.id && i.type === 'OutOfRange').id, first.id);
  const timing = (await dashboard()).disconnectedSeconds;
  console.log(`Waiting ${timing}s for genuine gateway disconnection…`);
  // Short repeated timer waits also keep the script usable with long timing settings.
  const end = Date.now() + (timing + 3) * 1000; while (Date.now() < end) await delay(Math.min(1000, end - Date.now()));
  assert.equal((await dashboard()).devices.find(d => d.device.id === moisture.id).connection.state, 'Disconnected');
  await heartbeat(moisture); assert.ok((await dashboard()).activeIncidents.some(i => i.deviceId === moisture.id && i.type === 'OutOfRange'));
  await reading(moisture, 50); assert.ok(!(await dashboard()).activeIncidents.some(i => i.deviceId === moisture.id));
  await reading(moisture, 5); assert.notEqual((await dashboard()).activeIncidents.find(i => i.deviceId === moisture.id && i.type === 'OutOfRange').id, first.id);
  console.log('PASS lifecycle: unique episode, timeout, heartbeat recovery, range recovery and recurrence.');
}
if (mode === 'learning' || mode === 'all') {
  await reading(pump, false); await reading(moisture, 5);
  const tips = data => data.suggestions.filter(s => s.targetId === pump.id && s.action === 'command');
  assert.equal(tips(await dashboard()).length, 0, 'Newly registered target must start without learned evidence.');
  for (let i = 0; i < 3; i++) {
    await request('/api/operations/interactions', { id: randomUUID(), targetId: pump.id, kind: 'search', query: pump.friendlyName });
    await command(true); await delay(5);
    assert.equal((await request('/api/operations/undo', { id: randomUUID() })).successful, true);
  }
  const learned = tips(await dashboard())[0]; assert.ok(learned, 'Repeated context-action history should produce a suggestion.'); assert.equal(learned.support, 3);
  await command(true); await delay(5); await command(false);
  const changed = (await dashboard()).suggestions.find(s => s.targetId === pump.id);
  assert.ok(changed); assert.ok(changed.action !== learned.action || changed.confidence < learned.confidence);
  console.log(`PASS learning: cold start → ${learned.action} support ${learned.support}, confidence ${learned.confidence} → revised top action ${changed.action}, confidence ${changed.confidence}.`);
}
console.log(`Simulation finished. Devices retained for inspection: ${suffix}`);
