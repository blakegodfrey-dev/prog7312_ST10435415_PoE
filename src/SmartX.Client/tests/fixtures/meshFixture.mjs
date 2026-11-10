// Controlled UI fixture, independent of production algorithms. Real algorithm
// behaviour is covered by backend tests and the API-fed mesh demo.
export function meshFixture({ selectedId = null, failedNodes = [], failedLinks = [], simulationEnabled = true,
  sensorId = 'ph', sensorName = 'Nutrient pH', sensorMac = 'A4:CF:12:8B:40:01', powerId = 'power' } = {}) {
  const definitions = [
    { id: 'gw', name: 'Hydroponic gateway', macAddress: null, isGateway: true, isRegistered: false },
    { id: sensorId, name: sensorName, macAddress: sensorMac, isGateway: false, isRegistered: true },
    { id: powerId, name: 'Pump Power Draw', macAddress: 'A4:CF:12:8B:40:03', isGateway: false, isRegistered: true },
    { id: 'isolated', name: 'Unlinked moisture', macAddress: '02:00:00:00:00:99', isGateway: false, isRegistered: true },
  ];
  const nodes = definitions.map(n => ({ ...n, sensorId: n.isGateway ? null : n.id, subnetId: 'hydroponic-main',
    isAvailable: !failedNodes.includes(n.id), connectionState: n.isGateway ? 'GatewayReady' : 'Connected',
    lastSeenAtUtc: '2026-11-08T12:00:00Z', availabilityReason: failedNodes.includes(n.id) ? 'SimulatedNodeFailure' : n.isGateway ? 'GatewayReady' : 'Available' }));
  const byId = new Map(nodes.map(n => [n.id, n]));
  const links = [
    { id: 'ga', sourceId: 'gw', targetId: sensorId, powerCostMilliJoules: .1, distanceMetres: 10 },
    { id: 'ab', sourceId: sensorId, targetId: powerId, powerCostMilliJoules: .082, distanceMetres: 8 },
    { id: 'gb', sourceId: 'gw', targetId: powerId, powerCostMilliJoules: .212, distanceMetres: 18 },
  ].map(e => ({ ...e, deliveryProbability: 1,
    isAvailable: !failedLinks.includes(e.id) && byId.get(e.sourceId).isAvailable && byId.get(e.targetId).isAvailable,
    availabilityReason: failedLinks.includes(e.id) ? 'SimulatedLinkFailure' : !byId.get(e.sourceId).isAvailable || !byId.get(e.targetId).isAvailable ? 'EndpointUnavailable' : 'Available' }));
  const parents = new Map(nodes.filter(n => n.isAvailable).map(n => [n.id, n.id]));
  const root = id => { while (parents.get(id) !== id) id = parents.get(id); return id; };
  const selected = [];
  for (const edge of [...links].filter(e => e.isAvailable).sort((a, b) => a.powerCostMilliJoules - b.powerCostMilliJoules)) {
    const a = root(edge.sourceId), b = root(edge.targetId);
    if (a !== b) { parents.set(a, b); selected.push(edge); }
  }
  const groups = new Map();
  for (const id of parents.keys()) { const key = root(id); if (!groups.has(key)) groups.set(key, []); groups.get(key).push(id); }
  const components = [...groups.values()].map(ids => ({ rootId: ids.includes('gw') ? 'gw' : ids[0], nodeIds: ids,
    edgeIds: selected.filter(e => ids.includes(e.sourceId)).map(e => e.id),
    totalPowerCostMilliJoules: selected.filter(e => ids.includes(e.sourceId)).reduce((sum, e) => sum + e.powerCostMilliJoules, 0) }));
  const reachable = byId.get('gw').isAvailable ? components.find(c => c.nodeIds.includes('gw')).nodeIds : [];
  let route = null;
  if (selectedId) {
    route = { nodeId: selectedId, gatewayId: 'gw', isReachable: reachable.includes(selectedId), nodeIds: [], edgeIds: [],
      hopCount: null, totalPowerCostMilliJoules: null, qualification: 'The MST minimises total tree cost, not every individual route cost.' };
    if (!route.isReachable) route.reason = !byId.get('gw').isAvailable ? 'GatewayUnavailable'
      : !byId.get(selectedId)?.isAvailable ? 'NodeUnavailable' : 'NoAvailablePathToGateway';
    else {
      const queue = ['gw'], previous = new Map();
      for (let i = 0; i < queue.length; i++) for (const edge of selected.filter(e => e.sourceId === queue[i] || e.targetId === queue[i])) {
        const next = edge.sourceId === queue[i] ? edge.targetId : edge.sourceId;
        if (next === 'gw' || previous.has(next)) continue;
        previous.set(next, { parent: queue[i], edge }); queue.push(next);
      }
      route.nodeIds = [selectedId]; route.totalPowerCostMilliJoules = 0;
      let current = selectedId;
      while (current !== 'gw') { const step = previous.get(current); route.edgeIds.push(step.edge.id); route.totalPowerCostMilliJoules += step.edge.powerCostMilliJoules; current = step.parent; route.nodeIds.push(current); }
      route.hopCount = route.edgeIds.length; route.reason = 'Available';
    }
  }
  return { generatedAtUtc: '2026-11-08T12:00:00Z', gatewayId: 'gw', subnetId: 'hydroponic-main', simulationRevision: failedNodes.length + failedLinks.length,
    simulationEnabled, nodes, links, reachability: { rootId: 'gw', reachableNodeIds: reachable,
      unreachableNodeIds: nodes.filter(n => !reachable.includes(n.id)).map(n => n.id) },
    forest: { algorithm: 'Prim', edges: selected, components, totalPowerCostMilliJoules: selected.reduce((sum, e) => sum + e.powerCostMilliJoules, 0),
      availableNodeCount: parents.size, isSingleTree: components.length === 1, heapEnqueues: 3, heapDequeues: 3, maximumFrontierSize: 2 },
    selectedRoute: route, networkState: !byId.get('gw').isAvailable ? 'GatewayUnavailable' : reachable.length === nodes.length ? 'Connected' : 'Degraded' };
}
