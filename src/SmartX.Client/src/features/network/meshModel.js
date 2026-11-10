const compareId = (a, b) => a.id < b.id ? -1 : a.id > b.id ? 1 : 0;

export function costText(value) {
  return value == null || !Number.isFinite(Number(value)) ? "Unavailable"
    : `${Number(value).toLocaleString(undefined, { maximumFractionDigits: 6 })} mJ`;
}

export function nodeStatus(node) {
  if (node.availabilityReason === "SimulatedNodeFailure") return "Simulated failure";
  if (node.isGateway) return node.isAvailable ? "Gateway ready" : "Gateway unavailable";
  if (!node.isRegistered) return "Unregistered";
  if (!node.lastSeenAtUtc) return "No gateway contact";
  return node.connectionState === "Connected" ? "Connected"
    : node.connectionState === "Stale" ? "Stale" : "Disconnected";
}

export function availabilityText(reason) {
  return ({ Available: "Available", GatewayReady: "Gateway ready", SimulatedNodeFailure: "Simulated node failure",
    SimulatedLinkFailure: "Simulated link failure", EndpointUnavailable: "An endpoint is unavailable",
    ConfiguredDisabled: "Disabled in configuration", NotRegistered: "Device is not registered",
    NoRecentGatewayContact: "No recent gateway contact", GatewayUnavailable: "Gateway unavailable",
    NodeUnavailable: "Selected node is unavailable", NoAvailablePathToGateway: "No available path to the gateway" })[reason] ?? reason;
}

export function filterMeshNodes(snapshot, { search = "", nodeFilter = "all" } = {}) {
  const term = search.trim().toLowerCase();
  const reachable = new Set(snapshot?.reachability.reachableNodeIds ?? []);
  return (snapshot?.nodes ?? []).filter(n => {
    if (term && ![n.name, n.id, n.macAddress ?? ""].some(v => v.toLowerCase().includes(term))) return false;
    return nodeFilter === "reachable" ? reachable.has(n.id) : nodeFilter === "unreachable" ? !reachable.has(n.id) : true;
  });
}

export function exactMeshNode(snapshot, value) {
  const term = value.trim().toLowerCase();
  return snapshot?.nodes.find(n => n.id.toLowerCase() === term || n.macAddress?.toLowerCase() === term) ?? null;
}

// Stable schematic placement, not RF geometry. Health, filters and selected
// routes never change the positions of existing configured vertices.
export function meshLayout(snapshot) {
  const linked = new Set(snapshot.links.flatMap(e => [e.sourceId, e.targetId]));
  const ring = snapshot.nodes.filter(n => !n.isGateway && linked.has(n.id)).sort(compareId);
  const isolated = snapshot.nodes.filter(n => !n.isGateway && !linked.has(n.id)).sort(compareId);
  const points = new Map();
  const gateway = snapshot.nodes.find(n => n.isGateway);
  if (gateway) points.set(gateway.id, { x: 480, y: 270 });
  ring.forEach((n, i) => {
    const angle = -Math.PI / 2 + i * Math.PI * 2 / ring.length;
    points.set(n.id, { x: 480 + 335 * Math.cos(angle), y: 270 + 195 * Math.sin(angle) });
  });
  isolated.forEach((n, i) => points.set(n.id, { x: 125 + (i % 4) * 235, y: 610 + Math.floor(i / 4) * 100 }));
  return { points, height: isolated.length ? 700 + Math.floor((isolated.length - 1) / 4) * 100 : 550,
    isolatedCount: isolated.length };
}

export function usableSelectedRoute(snapshot, selectedId) {
  const route = snapshot?.selectedRoute;
  if (!selectedId || !route || route.nodeId !== selectedId || route.gatewayId !== snapshot.gatewayId) return null;
  const reachable = snapshot.reachability.reachableNodeIds.includes(selectedId);
  if (route.isReachable !== reachable) return null;
  if (!route.isReachable) return route.nodeIds.length === 0 && route.edgeIds.length === 0 &&
    route.hopCount == null && route.totalPowerCostMilliJoules == null ? route : null;
  if (route.totalPowerCostMilliJoules == null || !Number.isFinite(route.totalPowerCostMilliJoules)) return null;
  if (route.nodeIds[0] !== selectedId || route.nodeIds.at(-1) !== snapshot.gatewayId ||
    route.nodeIds.length !== route.edgeIds.length + 1 || route.hopCount !== route.edgeIds.length ||
    new Set(route.nodeIds).size !== route.nodeIds.length) return null;
  const nodes = new Map(snapshot.nodes.map(n => [n.id, n]));
  const physical = new Map(snapshot.links.map(e => [e.id, e]));
  const selected = new Set(snapshot.forest.edges.map(e => e.id));
  let total = 0;
  for (let i = 0; i < route.edgeIds.length; i++) {
    const edge = physical.get(route.edgeIds[i]);
    const a = route.nodeIds[i], b = route.nodeIds[i + 1];
    if (!edge?.isAvailable || !selected.has(edge.id) || !nodes.get(a)?.isAvailable || !nodes.get(b)?.isAvailable ||
      !((edge.sourceId === a && edge.targetId === b) || (edge.sourceId === b && edge.targetId === a))) return null;
    total += edge.powerCostMilliJoules;
  }
  if (Math.abs(total - route.totalPowerCostMilliJoules) > 0.00001) return null;
  return route;
}
