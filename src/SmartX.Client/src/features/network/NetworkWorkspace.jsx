import { useWorkspaceState } from "../../state/useWorkspaceState.js";
import { meshApi } from "../../api/meshApi.js";
import { SensorDetail } from "../sensors/SensorDetail.jsx";
import { formatTelemetryTimestamp } from "../sensors/telemetryDisplay.js";
import { HISTORY_TIME_ZONE } from "../sensors/historyRange.js";
import { MeshGraph } from "./MeshGraph.jsx";
import { useMeshNetwork } from "./useMeshNetwork.js";
import { availabilityText, costText, exactMeshNode, filterMeshNodes, nodeStatus, usableSelectedRoute } from "./meshModel.js";
import "./network.css";

export function NetworkWorkspace() {
  const { state, dispatch } = useWorkspaceState();
  const choices = state.network;
  const patch = values => dispatch({ type: "network/patch", patch: values });
  const { snapshot, error, isLoading, refresh } = useMeshNetwork(choices.selectedNodeId, choices.routeLookupMac, choices.refreshVersion);
  const selected = snapshot?.nodes.find(n => n.id === choices.selectedNodeId);
  const selectedLink = snapshot?.links.find(e => e.id === choices.selectedLinkId);
  const byId = new Map((snapshot?.nodes ?? []).map(n => [n.id, n]));
  const route = !error && !isLoading && !choices.mutationPending ? usableSelectedRoute(snapshot, choices.selectedNodeId) : null;
  const rows = filterMeshNodes(snapshot, choices);
  const reachable = new Set(snapshot?.reachability.reachableNodeIds ?? []);
  const treeIds = new Set(snapshot?.forest.edges.map(e => e.id) ?? []);
  const canMutate = snapshot?.simulationEnabled && !error && !isLoading && !choices.mutationPending;
  const selectNode = id => dispatch({ type: "network/select", nodeId: id });

  function lookup(event) {
    event.preventDefault();
    const node = exactMeshNode(snapshot, choices.search);
    if (!node) { patch({ lookupError: "No exact MAC address or node ID matches. Select a name from the node list." }); return; }
    dispatch({ type: "network/select", nodeId: node.id,
      macAddress: choices.search.trim().toLowerCase() === node.macAddress?.toLowerCase() ? choices.search.trim() : null });
  }

  async function simulate(action, message) {
    if (!canMutate) return;
    patch({ mutationPending: true, mutationFeedback: null });
    try {
      await action();
      patch({ mutationFeedback: { error: false, message } });
    } catch (requestError) {
      patch({ mutationFeedback: { error: true, message: requestError.message } });
    } finally { dispatch({ type: "network/mutation-finished" }); }
  }

  function openTelemetry(id) {
    dispatch({ type: "telemetry/patch", patch: { selectedSensorId: id, isRegistering: false, successMessage: null } });
    dispatch({ type: "navigate", view: "sensors" });
  }

  function openCommands(id) {
    dispatch({ type: "commands/patch", patch: { selectedDeviceId: id, historySelection: "selected" } });
    dispatch({ type: "navigate", view: "commands" });
  }

  return (
    <main className="app-shell network-workspace">
      <header className="workspace-header">
        <div><p className="eyebrow">SMART-X NETWORK</p><h1>Network Topology and Mesh Routing</h1>
          <p className="hero-copy">Inspect wireless links, trace gateway routes and investigate sensor history.</p></div>
        <button className="secondary-button" type="button" onClick={refresh}>Refresh network</button>
      </header>

      {!snapshot && !error && <section className="state-panel" aria-live="polite"><h2>Loading mesh network</h2><p>Retrieving current gateway contact and available links.</p></section>}
      {error && <section className="state-panel error-panel" role="alert"><h2>Unable to refresh mesh network</h2><p>{error.message}</p>
        {error.traceId && <small>Trace ID: {error.traceId}</small>}
        {snapshot && <p>The previous snapshot remains visible. Route highlighting and simulation controls are paused.</p>}
        <button type="button" onClick={refresh}>Retry network</button>
        {choices.selectedNodeId && <button type="button" className="secondary-button" onClick={() => selectNode(null)}>Clear selection</button>}
      </section>}

      {snapshot && <>
        <div className="network-status-line">
          <span className={`network-status ${snapshot.networkState === "Connected" ? "good" : "attention"}`}>{snapshot.networkState === "GatewayUnavailable" ? "Gateway unavailable" : snapshot.networkState}</span>
          <span>Subnet <strong>{snapshot.subnetId}</strong></span>
          <span>Updated <time>{formatTelemetryTimestamp(snapshot.generatedAtUtc, HISTORY_TIME_ZONE)} SAST</time></span>
          <span>{choices.mutationPending ? "Applying simulation…" : isLoading ? "Updating selection…" : "Refreshes every 2 seconds"}</span>
        </div>
        <section className="network-metrics" aria-label="Mesh summary">
          <article><span>Gateway reachability</span><strong>{snapshot.reachability.reachableNodeIds.length} / {snapshot.nodes.length}</strong><small>Includes the gateway when available</small></article>
          <article><span>Available links</span><strong>{snapshot.links.filter(e => e.isAvailable).length} / {snapshot.links.length}</strong><small>Declared wireless connections</small></article>
          <article><span>Minimum forest cost</span><strong>{costText(snapshot.forest.totalPowerCostMilliJoules)}</strong><small>Simulated energy across selected edges</small></article>
          <article><span>Available components</span><strong>{snapshot.forest.components.length}</strong><small>{snapshot.forest.isSingleTree ? "One connected tree" : "Disconnected components form a forest"}</small></article>
        </section>

        <form className="network-filters" onSubmit={lookup} aria-label="Network filters">
          <label><span>Search nodes</span><input type="search" value={choices.search}
            placeholder="Name, MAC address or node ID" onChange={e => patch({ search: e.target.value, lookupError: null })} /></label>
          <label><span>Node reachability</span><select aria-label="Node reachability" value={choices.nodeFilter} onChange={e => patch({ nodeFilter: e.target.value })}>
            <option value="all">All nodes</option><option value="reachable">Reachable</option><option value="unreachable">Unreachable</option>
          </select></label>
          <button className="secondary-button" type="submit" disabled={!choices.search.trim()}>Find exact MAC / ID</button>
        </form>
        {choices.lookupError && <p className="network-feedback error" role="alert">{choices.lookupError}</p>}
        {choices.mutationFeedback && <p className={`network-feedback ${choices.mutationFeedback.error ? "error" : "success"}`} role={choices.mutationFeedback.error ? "alert" : "status"}>{choices.mutationFeedback.message}</p>}

        <div className="network-grid">
          <section className="network-panel network-map" aria-labelledby="mesh-map-title">
            <div className="network-panel-heading"><div><p className="eyebrow">WIRELESS MESH</p><h2 id="mesh-map-title">Links and gateway</h2></div>
              <label><span>Graph links</span><select aria-label="Graph links" value={choices.linksView} onChange={e => patch({ linksView: e.target.value })}>
                <option value="all">All declared links</option><option value="tree">Minimum spanning forest</option>
              </select></label>
            </div>
            <div className="mesh-legend" aria-label="Graph legend"><span><i className="legend-tree" />Selected tree edge</span><span><i className="legend-route" />Selected route</span><span><i className="legend-down" />Unavailable link</span></div>
            <MeshGraph snapshot={snapshot} selectedNodeId={choices.selectedNodeId} selectedLinkId={choices.selectedLinkId}
              route={route} linksView={choices.linksView} showCosts={choices.showCosts} onSelectNode={selectNode}
              onSelectLink={id => patch({ selectedLinkId: id })} />
            <div className="network-map-footer"><label className="mesh-check"><input type="checkbox" checked={choices.showCosts} onChange={e => patch({ showCosts: e.target.checked })} /> Show link costs (mJ)</label>
              <span>Schematic positions · select a node or link</span></div>
          </section>

          <aside className="network-panel network-selection" aria-label="Selected network node">
            <p className="eyebrow">SELECTED NODE</p>
            {!selected && <><h2>Trace a gateway route</h2><p>Select a node on the graph or use the searchable list. The selected sensor’s diagnostics and historical readings appear below.</p></>}
            {selected && <>
              <h2>{selected.name}</h2><span className={`network-status ${selected.isAvailable ? "good" : "attention"}`}>{nodeStatus(selected)}</span>
              <dl className="network-facts"><div><dt>MAC address</dt><dd className="monospace">{selected.macAddress ?? "Gateway / no sensor MAC"}</dd></div>
                <div><dt>Node ID</dt><dd className="monospace">{selected.id}</dd></div>
                <div><dt>Gateway contact</dt><dd>{selected.lastSeenAtUtc ? `${formatTelemetryTimestamp(selected.lastSeenAtUtc, HISTORY_TIME_ZONE)} SAST` : "No contact recorded"}</dd></div>
                <div><dt>Availability</dt><dd>{availabilityText(selected.availabilityReason)}</dd></div></dl>
              {(isLoading || choices.mutationPending) && <p aria-live="polite">Updating gateway route…</p>}
              {!isLoading && !error && !choices.mutationPending && !route && <p role="status">Waiting for a route matching the current network snapshot.</p>}
              {route && <div className="network-route" aria-label="Selected gateway route">
                <h3>{route.isReachable ? "Route to gateway" : "No available route"}</h3>
                {route.isReachable ? <><p><strong>{route.hopCount} {route.hopCount === 1 ? "hop" : "hops"}</strong> · {costText(route.totalPowerCostMilliJoules)}</p>
                  <ol>{route.nodeIds.map(id => <li key={id}>{byId.get(id)?.name ?? id}{id === snapshot.gatewayId ? " (gateway)" : ""}</li>)}</ol></>
                  : <p>{availabilityText(route.reason)}. No path is available through the current links.</p>}
              </div>}
              {selected.isRegistered && !selected.isGateway && <div className="network-actions">
                <button className="secondary-button" type="button" onClick={() => openTelemetry(selected.id)}>Open in Telemetry</button>
                <button className="secondary-button" type="button" onClick={() => openCommands(selected.id)}>Open command history</button>
              </div>}
              <button className="text-button" type="button" onClick={() => selectNode(null)}>Clear selection</button>
            </>}
            <p className="network-qualification">Prim minimises total tree weight. A sensor’s tree route is not guaranteed to be its shortest individual route.</p>
            <details className="network-algorithm"><summary>Algorithm evidence</summary><dl className="network-facts">
              <div><dt>Traversal</dt><dd>BFS over available adjacency</dd></div><div><dt>Spanning forest</dt><dd>Prim with a min-heap frontier</dd></div>
              <div><dt>Heap activity</dt><dd>{snapshot.forest.heapEnqueues} enqueues · {snapshot.forest.heapDequeues} dequeues</dd></div>
              <div><dt>Maximum frontier</dt><dd>{snapshot.forest.maximumFrontierSize} links</dd></div></dl></details>
          </aside>
        </div>

        <section className="network-panel network-node-browser" aria-labelledby="network-nodes-title"><div className="network-panel-heading"><h2 id="network-nodes-title">Network nodes</h2><span>{rows.length} matching nodes</span></div>
          <p className="network-help">The graph retains the full network. Search and reachability filters apply to this list.</p>
          <div className="network-node-list">{rows.map(n => <button key={n.id} type="button" className={`network-node-row ${choices.selectedNodeId === n.id ? "selected" : ""}`}
            aria-pressed={choices.selectedNodeId === n.id} onClick={() => selectNode(n.id)}>
            <span><strong>{n.name}</strong><small className="monospace">{n.macAddress ?? n.id}</small></span><span>{nodeStatus(n)}<small>{reachable.has(n.id) ? "Gateway reachable" : "No gateway route"}</small></span>
          </button>)}</div>
          {rows.length === 0 && <p>No nodes match these filters.</p>}
        </section>

        <section className="network-panel" aria-labelledby="network-links-title"><div className="network-panel-heading"><h2 id="network-links-title">Wireless links</h2><span>{snapshot.forest.edges.length} edges selected by Prim</span></div>
          <div className="network-table-scroll"><table className="network-link-table"><thead><tr><th scope="col">Link endpoints</th><th scope="col">Estimated cost</th><th scope="col">Availability</th><th scope="col">Tree</th><th scope="col">Inspect</th></tr></thead>
            <tbody>{snapshot.links.map(edge => <tr key={edge.id} className={choices.selectedLinkId === edge.id ? "selected" : ""}>
              <td>{byId.get(edge.sourceId)?.name ?? edge.sourceId}<span className="network-link-separator">↔</span>{byId.get(edge.targetId)?.name ?? edge.targetId}</td>
              <td>{costText(edge.powerCostMilliJoules)}</td><td>{availabilityText(edge.availabilityReason)}</td><td>{treeIds.has(edge.id) ? "Selected" : "Not selected"}</td>
              <td><button className="text-button" type="button" aria-label={`Inspect wireless link ${byId.get(edge.sourceId)?.name} to ${byId.get(edge.targetId)?.name}`} onClick={() => patch({ selectedLinkId: edge.id })}>Inspect</button></td>
            </tr>)}</tbody></table></div>
          {selectedLink && <div className="network-link-detail" aria-label="Selected link details"><h3>Selected link</h3>
            <p>{byId.get(selectedLink.sourceId)?.name} ↔ {byId.get(selectedLink.targetId)?.name}</p>
            <p>{costText(selectedLink.powerCostMilliJoules)} · {selectedLink.distanceMetres} m assumed distance · {(selectedLink.deliveryProbability * 100).toLocaleString()}% assumed delivery</p>
            <p>{availabilityText(selectedLink.availabilityReason)}</p><small className="monospace">{selectedLink.id}</small>
          </div>}
          {snapshot.links.length === 0 && <p>No wireless links are configured.</p>}
        </section>

        <section className="network-panel network-simulation" aria-labelledby="network-simulation-title"><h2 id="network-simulation-title">Failure simulation</h2>
          <p>These controls change routing availability only. Restoring a node clears its simulated failure; a real gateway heartbeat is still required.</p>
          {!snapshot.simulationEnabled && <p>Simulation controls are disabled in this environment.</p>}
          <div className="network-actions">
            <button className="secondary-button" type="button" disabled={!canMutate || !selected?.isAvailable}
              onClick={() => simulate(() => meshApi.setNodeAvailable(selected.id, false), "Selected node failed in the routing simulation.")}>Fail selected node</button>
            <button className="secondary-button" type="button" disabled={!canMutate || selected?.availabilityReason !== "SimulatedNodeFailure"}
              onClick={() => simulate(() => meshApi.setNodeAvailable(selected.id, true), "Node failure cleared; current gateway health determines availability.")}>Restore selected node</button>
            <button className="secondary-button" type="button" disabled={!canMutate || !selectedLink?.isAvailable}
              onClick={() => simulate(() => meshApi.setLinkAvailable(selectedLink.id, false), "Selected wireless link failed in the routing simulation.")}>Fail selected link</button>
            <button className="secondary-button" type="button" disabled={!canMutate || selectedLink?.availabilityReason !== "SimulatedLinkFailure"}
              onClick={() => simulate(() => meshApi.setLinkAvailable(selectedLink.id, true), "Selected link failure cleared.")}>Restore selected link</button>
            <button className="secondary-button" type="button" disabled={!canMutate}
              onClick={() => simulate(meshApi.reset, "All mesh simulation failures cleared; actual gateway health retained.")}>Clear simulated failures</button>
          </div><p className="network-help">Link distances and energy values are declared simulation estimates. No physical RF measurement is claimed.</p>
        </section>

        {selected?.isRegistered && !selected.isGateway && <section className="network-panel network-telemetry" aria-label="Linked sensor diagnostics and history">
          <h2>Sensor telemetry and diagnostics</h2><p>History filters and anomaly selections are shared with the Telemetry module.</p>
          <SensorDetail key={selected.id} sensorId={selected.id} backLabel="Clear network selection"
            showNetworkLink={false} onBack={() => selectNode(null)} />
        </section>}
      </>}
    </main>
  );
}
