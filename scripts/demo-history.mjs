import assert from 'node:assert/strict';
import { randomBytes, randomUUID } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';

// Every sensor/reading enters through the HTTP API. Run against a test database.
// seed [10000] creates a reproducible case; verify reuses its manifest after restart.
const base = (process.argv[2] ?? 'http://localhost:5075').replace(/\/$/, '');
const mode = process.argv[3] ?? 'seed';
assert.ok(['seed', 'verify'].includes(mode), 'Mode must be seed or verify.');
const manifestPath = resolve('artifacts/pillar1-demo.json');
async function request(path, body, expected = 200) {
  const response = await fetch(`${base}${path}`, {
    method: body === undefined ? 'GET' : 'POST', signal: AbortSignal.timeout(120000),
    ...(body === undefined ? {} : { headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) }),
  });
  const text = await response.text();
  assert.equal(response.status, expected, `${path}: ${text}`);
  return text ? JSON.parse(text) : null;
}
function historyUrl(id, filters = {}) {
  return `/api/telemetry/sensors/${id}?${new URLSearchParams({ pageSize: '500', ...filters })}`;
}
async function verify(data) {
  const range = { fromUtc: data.fromUtc, toUtc: data.toUtc };
  const result = await request(historyUrl(data.sensorId, range));
  assert.equal(result.tree?.structure, 'AVL', 'History must report an AVL query.');
  assert.equal(result.totalCount, data.expectedIds.length);
  assert.deepEqual(result.readings.map(r => r.id).sort(), [...data.expectedIds].sort());
  assert.ok(result.readings.every(r => r.sensorId === data.sensorId));
  const equal = await request(historyUrl(data.sensorId, { fromUtc: data.equalUtc, toUtc: data.equalUtc }));
  assert.equal(equal.totalCount, 2, 'Equal timestamps must preserve both packet IDs.');
  const invalid = await request(historyUrl(data.sensorId, { ...range, isValid: 'false' }));
  assert.equal(invalid.totalCount, 1);
  const valid = await request(historyUrl(data.sensorId, { ...range, isValid: 'true' }));
  assert.equal(valid.totalCount, data.expectedIds.length - 1);
  const paged = [];
  for (let page = 1; page <= Math.ceil(result.totalCount / 2); page++) {
    const slice = await request(historyUrl(data.sensorId, { ...range, page: String(page), pageSize: '2' }));
    paged.push(...slice.readings.map(r => r.id));
  }
  assert.deepEqual(paged, result.readings.map(r => r.id), 'Pagination must have no repeats or omissions.');
  const offsetRange = { fromUtc: `${data.date}T14:00:00+02:00`, toUtc: `${data.date}T16:00:00+02:00` };
  assert.deepEqual((await request(historyUrl(data.sensorId, offsetRange))).readings.map(r => r.id), result.readings.map(r => r.id));
  const full = await request(historyUrl(data.sensorId));
  assert.equal(full.totalCount, data.expectedTotal);
  assert.equal(full.tree.indexedReadings, data.expectedTotal);
  await request(historyUrl(data.sensorId, { fromUtc: data.toUtc, toUtc: data.fromUtc }), undefined, 400);
  const before = await request(historyUrl(data.sensorId, { toUtc: '2000-01-01T00:00:00Z' }));
  assert.equal(before.totalCount, 0);
  console.log(`PASS ${mode}: ${data.date} Node A moisture 14:00-16:00 SAST; ${result.totalCount} exact readings.`);
  console.log(`PASS duplicates, out-of-order arrival, inclusive endpoints, sensor isolation, validity, pagination, offset equivalence, validation and ${full.totalCount} retained readings.`);
  console.log(`Tree evidence: ${JSON.stringify(result.tree)}`);
  console.log(`UI: search for "${data.sensorName}"; From ${data.date} 14:00:00; To ${data.date} 16:00:00.`);
  return result;
}

if (mode === 'verify') {
  await verify(JSON.parse(await readFile(manifestPath, 'utf8')));
  console.log('Restart check passed only if the API process was stopped and restarted before this command.');
} else {
  const extraCount = Number(process.argv[4] ?? 10000);
  assert.ok(Number.isInteger(extraCount) && extraCount >= 0 && extraCount <= 100000, 'Extra history must be 0-100000 readings.');
  const nodes = await request('/api/deployment-nodes');
  const location = nodes.find(n => n.nodeType === 'Node');
  assert.ok(location, 'Start the Development API with the existing facility seed first.');
  const stamp = randomBytes(4).toString('hex').toUpperCase().match(/../g);
  const suffix = stamp.join('');
  const sensorId = randomUUID();
  const otherId = randomUUID();
  const sensorName = `Pillar1 Node A moisture ${suffix}`;
  for (const [id, name, tail] of [[sensorId, sensorName, '01'], [otherId, `Pillar1 Node B moisture ${suffix}`, '02']]) {
    await request('/api/sensors', { id, friendlyName: name, macAddress: `02:${stamp.join(':')}:${tail}`,
      category: 'Environmental', measuredProperty: 'Soil moisture', valueKind: 'Float', unit: '%',
      deploymentNodeId: location.id, expectedMinimum: 30, expectedMaximum: 70 }, 201);
  }
  const yesterday = new Date(Date.now() - 86400000).toISOString().slice(0, 10);
  const at = time => new Date(`${yesterday}T${time}+02:00`).toISOString();
  const sample = [['16:00:01', 50], ['15:00:00', 10], ['14:00:00', 45],
    ['13:59:59', 50], ['16:00:00', 55], ['15:00:00', 52], ['14:30:00', 48]].map(([time, value]) => ({
    id: randomUUID(), sensorId, valueKind: 'Float', floatValue: value,
    recordedAtUtc: at(time), receivedAtUtc: new Date().toISOString(),
  }));
  await request('/api/telemetry/bulk', { readings: [...sample,
    { ...sample[2], id: randomUUID(), sensorId: otherId }] }, 201);
  await request('/api/telemetry/bulk', { readings: [sample[0]] }, 409);
  const data = { sensorId, otherId, sensorName, date: yesterday, fromUtc: at('14:00:00'), toUtc: at('16:00:00'),
    equalUtc: at('15:00:00'), expectedIds: sample.filter(r => r.recordedAtUtc >= at('14:00:00') && r.recordedAtUtc <= at('16:00:00')).map(r => r.id),
    expectedTotal: sample.length, measurements: [] };
  const oldStart = Date.parse(data.fromUtc) - 10 * 86400000;
  // Measurements include real HTTP/API overhead. They are not pure AVL timings.
  const milestones = [...new Set([0, Math.min(1000, extraCount), Math.min(5000, extraCount), extraCount])].sort((a, b) => a - b);
  let sent = 0;
  for (const milestone of milestones) {
    while (sent < milestone) {
      const length = Math.min(500, milestone - sent);
      const readings = Array.from({ length }, (_, j) => ({ id: randomUUID(), sensorId,
        valueKind: 'Float', floatValue: 40 + ((sent + j) % 25),
        recordedAtUtc: new Date(oldStart + (sent + j) * 1000).toISOString(), receivedAtUtc: new Date().toISOString() }));
      await request('/api/telemetry/bulk', { readings }, 201);
      sent += length;
    }
    data.expectedTotal = sample.length + sent;
    const times = [];
    let result;
    for (let repeat = 0; repeat < 10; repeat++) {
      const start = performance.now();
      result = await request(historyUrl(sensorId, { fromUtc: data.fromUtc, toUtc: data.toUtc }));
      if (repeat > 0) times.push(performance.now() - start); // Exclude warm-up/initial hydration.
    }
    times.sort((a, b) => a - b);
    data.measurements.push({ indexed: data.expectedTotal, height: result.tree.height,
      matched: result.totalCount, visited: result.tree.visitedNodes, medianHttpMs: Number(times[4].toFixed(3)) });
    await mkdir(dirname(manifestPath), { recursive: true });
    await writeFile(manifestPath, `${JSON.stringify(data, null, 2)}\n`);
  }
  await verify(data);
  console.table(data.measurements);
  console.log(`Saved ${manifestPath}. Stop/restart the API using the SAME database, then run:`);
  console.log(`node scripts/demo-history.mjs ${base} verify`);
}
