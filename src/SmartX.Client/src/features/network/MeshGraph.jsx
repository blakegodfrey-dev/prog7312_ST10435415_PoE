import { availabilityText, costText, meshLayout, nodeStatus } from "./meshModel.js";

function activate(event, action) {
  if (event.key === "Enter" || event.key === " ") { event.preventDefault(); action(); }
}

export function MeshGraph({ snapshot, selectedNodeId, selectedLinkId, route,
  linksView, showCosts, onSelectNode, onSelectLink }) {
  const { points, height, isolatedCount } = meshLayout(snapshot);
  const nodes = new Map(snapshot.nodes.map(n => [n.id, n]));
  const tree = new Set(snapshot.forest.edges.map(e => e.id));
  const path = new Set(route?.isReachable ? route.edgeIds : []);
  const pathNodes = new Set(route?.isReachable ? route.nodeIds : []);
  const links = snapshot.links.filter(e => linksView !== "tree" || tree.has(e.id));
  return (
    <div className="mesh-graph-scroll">
      <svg className="mesh-graph" viewBox={`0 0 960 ${height}`} role="group" aria-label="Wireless mesh topology">
        <title>Declared wireless links and actual minimum spanning forest</title>
        <desc>Nodes and links can be selected using Enter or Space. Purple lines show the selected gateway route. Positions are schematic.</desc>
        {links.map(edge => {
          const a = points.get(edge.sourceId), b = points.get(edge.targetId);
          if (!a || !b) return null;
          const label = `${nodes.get(edge.sourceId)?.name} to ${nodes.get(edge.targetId)?.name}, ${costText(edge.powerCostMilliJoules)}, ${availabilityText(edge.availabilityReason)}`;
          const inPath = path.has(edge.id);
          return (
            <g key={edge.id} className={`mesh-edge ${tree.has(edge.id) ? "in-tree" : "alternative"} ${inPath ? "in-route" : ""} ${!edge.isAvailable ? "unavailable" : ""} ${selectedLinkId === edge.id ? "selected" : ""}`}
              role="button" tabIndex="0" aria-label={`Inspect link ${label}`} aria-pressed={selectedLinkId === edge.id}
              data-link-id={edge.id} onClick={() => onSelectLink(edge.id)}
              onKeyDown={event => activate(event, () => onSelectLink(edge.id))}>
              <title>{label}</title>
              <line className="mesh-edge-hit" x1={a.x} y1={a.y} x2={b.x} y2={b.y} />
              <circle className="mesh-edge-hit-dot" cx={(a.x + b.x) / 2} cy={(a.y + b.y) / 2} r="7" fill="transparent" />
              <line className="mesh-edge-line" x1={a.x} y1={a.y} x2={b.x} y2={b.y} />
              {(showCosts || selectedLinkId === edge.id) && (
                <text className="mesh-link-cost" x={(a.x + b.x) / 2} y={(a.y + b.y) / 2 - 6} textAnchor="middle">
                  {Number(edge.powerCostMilliJoules).toLocaleString(undefined, { maximumFractionDigits: 6 })}
                </text>
              )}
            </g>
          );
        })}
        {isolatedCount > 0 && <g className="mesh-isolate-heading"><line x1="35" y1="550" x2="925" y2="550" /><text x="35" y="575">No configured wireless links</text></g>}
        {snapshot.nodes.map(node => {
          const point = points.get(node.id);
          if (!point) return null;
          const status = nodeStatus(node);
          const name = node.name.length > 22 ? `${node.name.slice(0, 20)}…` : node.name;
          return (
            <g key={node.id} className={`mesh-node ${node.isGateway ? "gateway" : "sensor"} ${node.isAvailable ? "available" : "unavailable"} ${node.connectionState === "Stale" ? "stale" : ""} ${pathNodes.has(node.id) ? "in-route" : ""} ${selectedNodeId === node.id ? "selected" : ""}`}
              role="button" tabIndex="0" aria-label={`Select ${node.name}, ${status}`}
              aria-pressed={selectedNodeId === node.id} data-node-id={node.id}
              onClick={() => onSelectNode(node.id)} onKeyDown={event => activate(event, () => onSelectNode(node.id))}>
              <title>{node.name}: {status}. {node.macAddress ?? node.id}</title>
              {node.isGateway ? <rect x={point.x - 75} y={point.y - 25} width="150" height="50" rx="15" />
                : <circle cx={point.x} cy={point.y} r="21" />}
              <text className="mesh-node-symbol" x={point.x} y={point.y + 5} textAnchor="middle">{node.isGateway ? "GATEWAY" : node.isAvailable ? "ESP32" : "OFF"}</text>
              <text className="mesh-node-name" x={point.x} y={point.y + 42} textAnchor="middle">{name}</text>
              <text className="mesh-node-state" x={point.x} y={point.y + 58} textAnchor="middle">{status}</text>
            </g>
          );
        })}
      </svg>
    </div>
  );
}
