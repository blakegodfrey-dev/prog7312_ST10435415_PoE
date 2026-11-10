import assert from 'node:assert/strict';
import { randomUUID, randomBytes } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';

// Exercises actual HTTP controllers and algorithms. Use a test database.
// node scripts/demo-mesh.mjs http://localhost:5075 [demo|verify|reset|watch]
const base = (process.argv[2] ?? 'http://localhost:5075').replace(/\/$/, '');
const mode = process.argv[3] ?? 'demo';
assert.ok(['demo', 'verify', 'reset', 'watch'].includes(mode), 'Mode must be demo, verify, reset or watch.');
const manifestPath = resolve('artifacts/pillar2-demo.json');
async function request(path, method = 'GET', body, expected = 200) {
  const response = await fetch(`${base}${path}`, { method, signal: AbortSignal.timeout(120000),
    ...(body === undefined ? {} : { headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) }) });
  const text = await response.text();
  assert.equal(response.status, expected, `${method} ${path}: ${text}`);
  return text ? JSON.parse(text) : null;
}
const mesh = () => request('/api/mesh');
const route = id => request(`/api/mesh/route?nodeId=${encodeURIComponent(id)}`);
const nodeAvailable = (id, value) => request(`/api/mesh/nodes/${id}/availability`, 'PUT', { isAvailable: value });
const linkAvailable = (id, value) => request(`/api/mesh/links/${id}/availability`, 'PUT', { isAvailable: value });
const equalCost = (a, b) => assert.ok(Math.abs(a - b) < 0.00001, `Expected cost ${b}, received ${a}`);
const sameIds = (a, b) => assert.deepEqual([...a].sort(), [...b].sort());

// Independent adjacency traversal and Kruskal/union-find oracle; no Prim code is copied.
function inspect(snapshot) {
  const nodes = new Map(snapshot.nodes.map(n => [n.id, n]));
  const available = new Set(snapshot.nodes.filter(n => n.isAvailable).map(n => n.id));
  const links = new Map(snapshot.links.map(e => [e.id, e]));
  const parents = new Map([...available].map(id => [id, id]));
  function root(id) { while (parents.get(id) !== id) id = parents.get(id); return id; }
  const active = snapshot.links.filter(e => e.isAvailable);
  for (const edge of active) assert.ok(available.has(edge.sourceId) && available.has(edge.targetId));
  let oracleCost = 0;
  let oracleEdges = 0;
  for (const edge of [...active].sort((a, b) => a.powerCostMilliJoules - b.powerCostMilliJoules || a.id.localeCompare(b.id))) {
    const a = root(edge.sourceId), b = root(edge.targetId);
    if (a === b) continue;
    parents.set(a, b); oracleCost += edge.powerCostMilliJoules; oracleEdges++;
  }
  equalCost(snapshot.forest.totalPowerCostMilliJoules, oracleCost);
  assert.equal(snapshot.forest.edges.length, oracleEdges);
  assert.equal(snapshot.forest.components.length, available.size - oracleEdges);
  assert.equal(snapshot.forest.availableNodeCount, available.size);
  assert.equal(snapshot.forest.algorithm, 'Prim');
  assert.match(snapshot.forest.frontier, /PriorityQueue/);
  const selected = new Map(snapshot.forest.edges.map(e => [e.id, e]));
  let reportedCost = 0;
  for (const edge of selected.values()) {
    const original = links.get(edge.id);
    assert.ok(original?.isAvailable, 'Selected tree edge must be an available physical link.');
    assert.equal(edge.sourceId, original.sourceId); assert.equal(edge.targetId, original.targetId);
    equalCost(edge.powerCostMilliJoules, original.powerCostMilliJoules);
    reportedCost += edge.powerCostMilliJoules;
  }
  equalCost(reportedCost, oracleCost);
  const covered = snapshot.forest.components.flatMap(c => c.nodeIds);
  assert.equal(new Set(covered).size, covered.length);
  sameIds(covered, available);
  for (const component of snapshot.forest.components) {
    assert.equal(component.edgeIds.length, component.nodeIds.length - 1);
    assert.ok(component.nodeIds.includes(component.rootId));
    const componentNodes = new Set(component.nodeIds);
    for (const id of component.edgeIds) {
      const edge = selected.get(id);
      assert.ok(edge && componentNodes.has(edge.sourceId) && componentNodes.has(edge.targetId));
    }
  }
  const adjacency = new Map([...nodes.keys()].map(id => [id, []]));
  for (const edge of active) {
    adjacency.get(edge.sourceId).push(edge.targetId); adjacency.get(edge.targetId).push(edge.sourceId);
  }
  const visited = new Set();
  if (available.has(snapshot.gatewayId)) {
    const queue = [snapshot.gatewayId]; visited.add(snapshot.gatewayId);
    for (let i = 0; i < queue.length; i++)
      for (const next of adjacency.get(queue[i])) if (!visited.has(next)) { visited.add(next); queue.push(next); }
  }
  sameIds(snapshot.reachability.reachableNodeIds, visited);
  sameIds(snapshot.reachability.unreachableNodeIds, [...nodes.keys()].filter(id => !visited.has(id)));
  return { nodes, links, selected, reachable: visited };
}

function checkRoute(snapshot, result) {
  const inspected = inspect(snapshot);
  assert.match(result.qualification, /not every individual/);
  if (!result.isReachable) {
    assert.equal(inspected.reachable.has(result.nodeId), false);
    assert.deepEqual(result.nodeIds, []); assert.deepEqual(result.edgeIds, []);
    assert.equal(result.hopCount, null); assert.equal(result.totalPowerCostMilliJoules, null);
    return;
  }
  assert.equal(result.nodeIds[0], result.nodeId);
  assert.equal(result.nodeIds.at(-1), snapshot.gatewayId);
  assert.equal(result.hopCount, result.edgeIds.length);
  assert.equal(result.nodeIds.length, result.edgeIds.length + 1);
  assert.equal(new Set(result.nodeIds).size, result.nodeIds.length, 'Route must not contain a cycle.');
  let cost = 0;
  for (let i = 0; i < result.edgeIds.length; i++) {
    const edge = inspected.selected.get(result.edgeIds[i]);
    assert.ok(edge, 'A claimed MST route must use selected Prim edges.');
    assert.ok(inspected.nodes.get(result.nodeIds[i]).isAvailable);
    assert.ok((edge.sourceId === result.nodeIds[i] && edge.targetId === result.nodeIds[i + 1]) ||
      (edge.targetId === result.nodeIds[i] && edge.sourceId === result.nodeIds[i + 1]));
    cost += edge.powerCostMilliJoules;
  }
  equalCost(result.totalPowerCostMilliJoules, cost);
}

async function refreshContacts(ids) {
  for (const id of ids) await request(`/api/operations/heartbeat/${id}`, 'POST', {});
}

if (mode === 'watch') {
  const data = JSON.parse(await readFile(manifestPath, 'utf8'));
  console.log('Keeping simulated gateway contacts current through the API every 10 seconds. Ctrl+C stops the watcher.');
  while (true) {
    await refreshContacts(data.sensorIds);
    console.log(`Heartbeat contacts refreshed at ${new Date().toISOString()}. Routing failure overrides remain unchanged.`);
    await delay(10000);
  }
} else if (mode === 'reset') {
  const snapshot = await request('/api/mesh/simulation', 'DELETE');
  inspect(snapshot);
  console.log('PASS: simulated failures cleared. Actual heartbeat health is still authoritative.');
} else if (mode === 'verify') {
  const data = JSON.parse(await readFile(manifestPath, 'utf8'));
  await refreshContacts(data.sensorIds); // Genuine API heartbeat contacts, not forced availability.
  const snapshot = await mesh(); inspect(snapshot);
  equalCost(snapshot.forest.totalPowerCostMilliJoules, data.baselineCost);
  const result = await route(data.targetId); checkRoute(snapshot, result); assert.ok(result.isReachable);
  console.log(`PASS: configured topology, BFS, Prim forest and target route rechecked; cost ${snapshot.forest.totalPowerCostMilliJoules} mJ.`);
  console.log('Verify refreshed real gateway heartbeats. Demonstration failure overrides intentionally reset on process restart.');
} else {
  const initial = await mesh();
  assert.ok(initial.simulationEnabled, 'Use the Development API launch profile; MeshNetwork:AllowSimulation must be enabled for this demo.');
  await request('/api/mesh/simulation', 'DELETE');
  const devices = await request('/api/live/devices');
  const configuredIds = new Set(initial.links.flatMap(e => [e.sourceId, e.targetId]).filter(id => id !== initial.gatewayId));
  const configuredDevices = devices.filter(d => configuredIds.has(d.id));
  assert.equal(configuredDevices.length, configuredIds.size, 'Some configured mesh endpoints are not registered. Use the seeded hydroponic test database.');
  const locations = await request('/api/deployment-nodes');
  const location = locations.find(n => n.nodeType === 'Node'); assert.ok(location);
  const suffix = randomBytes(4).toString('hex').toUpperCase();
  const isolatedId = randomUUID();
  await request('/api/sensors', 'POST', { id: isolatedId, macAddress: `02:${suffix.match(/../g).join(':')}:F0`,
    friendlyName: `Pillar2 isolated sensor ${suffix}`, category: 'Environmental', measuredProperty: 'Soil moisture',
    valueKind: 'Float', unit: '%', deploymentNodeId: location.id, expectedMinimum: 30, expectedMaximum: 70 }, 201);
  const isolated = (await request('/api/live/devices')).find(d => d.id === isolatedId); assert.ok(isolated);
  const sensorIds = [...configuredDevices.map(d => d.id), isolatedId];
  const nativeReadings = [...configuredDevices, isolated].map(sensor => {
    const mid = sensor.expectedMinimum != null && sensor.expectedMaximum != null
      ? (sensor.expectedMinimum + sensor.expectedMaximum) / 2 : 50;
    const payload = { id: randomUUID(), sensorId: sensor.id, valueKind: sensor.valueKind,
      recordedAtUtc: new Date(Date.now() - 1000).toISOString(), receivedAtUtc: new Date().toISOString() };
    if (sensor.valueKind === 'Float') payload.floatValue = mid;
    else if (sensor.valueKind === 'Integer') payload.integerValue = Math.round(mid);
    else payload.booleanValue = sensor.latestReading?.booleanValue ?? false;
    return payload;
  });
  await request('/api/telemetry/bulk', 'POST', { readings: nativeReadings }, 201);
  const baseline = await mesh(); inspect(baseline);
  assert.ok(configuredDevices.every(d => baseline.reachability.reachableNodeIds.includes(d.id)), 'All configured sensors must initially reach the gateway.');
  const noRoute = await route(isolatedId); checkRoute(baseline, noRoute); assert.equal(noRoute.isReachable, false);
  assert.equal(baseline.nodes.find(n => n.id === isolatedId).isAvailable, true, 'The isolated node is online but has no wireless link.');
  const defaults = baseline.links.length === 18 && baseline.links.every(e => e.id.startsWith('50000000-0000-0000-0000-'));
  if (defaults) equalCost(baseline.forest.totalPowerCostMilliJoules, 1.002);
  console.log(`PASS baseline: ${configuredDevices.length} configured sensors reach the gateway; Prim cost ${baseline.forest.totalPowerCostMilliJoules} mJ; ${baseline.forest.components.length} components including an online isolated node.`);
  console.log(`Heap evidence: enqueues=${baseline.forest.heapEnqueues}, dequeues=${baseline.forest.heapDequeues}, maximumFrontier=${baseline.forest.maximumFrontierSize}.`);
  let target;
  let original;
  for (const device of configuredDevices) {
    const candidate = await route(device.id); checkRoute(baseline, candidate);
    if (candidate.nodeIds.length >= 3) { target = device; original = candidate; break; }
  }
  assert.ok(target, 'The demonstration needs a sensor with a multi-hop MST path.');
  const byMac = await request(`/api/mesh/route?macAddress=${encodeURIComponent(target.macAddress.toLowerCase())}`);
  assert.deepEqual(byMac.nodeIds, original.nodeIds); assert.deepEqual(byMac.edgeIds, original.edgeIds);
  console.log(`PASS MAC/ID lookup: ${target.friendlyName}; path ${original.nodeIds.join(' -> ')}.`);
  const relay = original.nodeIds[1];
  try {
    let changed = await nodeAvailable(relay, false); inspect(changed);
    let result = await route(target.id); checkRoute(changed, result);
    assert.ok(result.isReachable, 'Bundled mesh must have an available alternative around the failed relay.');
    assert.ok(!result.nodeIds.includes(relay));
    console.log(`PASS node failure: recomputed path avoids relay ${relay}; cost ${result.totalPowerCostMilliJoules} mJ.`);
    await nodeAvailable(relay, true);
    const failingLink = original.edgeIds[0];
    changed = await linkAvailable(failingLink, false); inspect(changed);
    result = await route(target.id); checkRoute(changed, result);
    assert.ok(result.isReachable); assert.ok(!result.edgeIds.includes(failingLink));
    console.log('PASS link failure: recomputed route uses an alternative declared neighbour.');
    await linkAvailable(failingLink, true);
    const incident = baseline.links.filter(e => e.sourceId === target.id || e.targetId === target.id);
    for (const edge of incident) await linkAvailable(edge.id, false);
    changed = await mesh(); result = await route(target.id); checkRoute(changed, result);
    assert.equal(result.isReachable, false); assert.equal(result.reason, 'NoAvailablePathToGateway');
    console.log('PASS disconnected target: no route, null hop/cost and a separate forest component.');
    for (const edge of incident) await linkAvailable(edge.id, true);
    changed = await nodeAvailable(baseline.gatewayId, false); inspect(changed);
    assert.deepEqual(changed.reachability.reachableNodeIds, []);
    result = await route(target.id); checkRoute(changed, result);
    assert.equal(result.reason, 'GatewayUnavailable');
    console.log('PASS gateway failure: no sensor-to-gateway route is fabricated.');
  } finally {
    await request('/api/mesh/simulation', 'DELETE');
  }
  await refreshContacts(sensorIds);
  const restored = await mesh(); inspect(restored);
  const restoredRoute = await route(target.id); checkRoute(restored, restoredRoute); assert.ok(restoredRoute.isReachable);
  equalCost(restored.forest.totalPowerCostMilliJoules, baseline.forest.totalPowerCostMilliJoules);
  await request('/api/mesh/route?macAddress=not-a-mac', 'GET', undefined, 400);
  await request(`/api/mesh/route?nodeId=${randomUUID()}`, 'GET', undefined, 404);
  const data = { gatewayId: baseline.gatewayId, subnetId: baseline.subnetId, sensorIds, isolatedId,
    targetId: target.id, targetName: target.friendlyName, targetMac: target.macAddress,
    baselineCost: baseline.forest.totalPowerCostMilliJoules, originalRoute: original, restoredRoute,
    checkedAtUtc: new Date().toISOString() };
  await mkdir(dirname(manifestPath), { recursive: true });
  await writeFile(manifestPath, `${JSON.stringify(data, null, 2)}\n`);
  console.log('PASS recovery: simulated failures cleared, original minimum cost restored and real route available.');
  console.log(`Saved ${manifestPath}. Run "node scripts/demo-mesh.mjs ${base} verify" to recheck.`);
  console.log('All telemetry entered the API. Wireless links/power estimates are simulated; no physical radio transmission is claimed.');
}
