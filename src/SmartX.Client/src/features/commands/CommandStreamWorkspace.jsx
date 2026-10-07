import { SENSOR_CATEGORIES } from "../../api/sensorOptions.js";
import { useWorkspaceState } from "../../state/useWorkspaceState.js";

export function CommandStreamWorkspace() {
  const { state, dispatch } = useWorkspaceState();
  const { search, category, alertFilter, historySelection } = state.commandStream;
  const patch = (values) => dispatch({ type: "commands/patch", patch: values });

  return (
    <main className="app-shell sensor-workspace command-workspace">
      <header className="workspace-header">
        <div>
          <p className="eyebrow">SMART HYDROPONIC FACILITY</p>
          <h1>Real-Time Command Stream and History</h1>
          <p className="hero-copy">
            Review device activity and prepare your operational workspace.
          </p>
        </div>
      </header>

      <div className="command-phase-notice" role="status">
        <strong>Command workspace is being prepared.</strong>
        <p>
          Live device data and operational controls will arrive in the next
          development phases. Your workspace filters are kept when you switch
          modules.
        </p>
      </div>

      <section className="filter-panel" aria-label="Command Stream filters">
        <label>
          <span>Search devices</span>
          <input
            type="search"
            value={search}
            onChange={(event) => patch({ search: event.target.value })}
            placeholder="Name, MAC address or property"
          />
        </label>
        <label>
          <span>Device category</span>
          <select value={category} onChange={(event) => patch({ category: event.target.value })}>
            <option value="">All categories</option>
            {SENSOR_CATEGORIES.map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </select>
        </label>
        <label>
          <span>Alert state</span>
          <select value={alertFilter} onChange={(event) => patch({ alertFilter: event.target.value })}>
            <option value="all">All devices</option>
            <option value="active">Active alerts</option>
            <option value="critical">Critical</option>
            <option value="disconnected">Disconnected</option>
            <option value="healthy">Healthy</option>
          </select>
        </label>
      </section>

      <div className="command-panel-grid">
        <section className="command-shell-panel" aria-labelledby="live-devices-title">
          <h2 id="live-devices-title">Live devices and telemetry</h2>
          <p>Live readings will appear here when the device registry is connected.</p>
        </section>
        <section className="command-shell-panel" aria-labelledby="selected-device-title">
          <h2 id="selected-device-title">Selected device</h2>
          <p>Select a device from the live list once it becomes available.</p>
          <button className="secondary-button" type="button" disabled>Manual command unavailable</button>
        </section>
        <section className="command-shell-panel" aria-labelledby="active-alerts-title">
          <h2 id="active-alerts-title">Active alerts</h2>
          <p>Device incidents and connection warnings will appear here.</p>
        </section>
        <section className="command-shell-panel" aria-labelledby="processing-status-title">
          <h2 id="processing-status-title">Processing status</h2>
          <p>Queue, device, alert and Undo counts will appear when processing is connected.</p>
        </section>
        <section className="command-shell-panel" aria-labelledby="command-history-title">
          <h2 id="command-history-title">Command history</h2>
          <p>Successful and failed device commands will be shown here.</p>
          <button className="secondary-button" type="button" disabled>Undo unavailable</button>
        </section>
        <section className="command-shell-panel" aria-labelledby="command-telemetry-history-title">
          <h2 id="command-telemetry-history-title">Telemetry history</h2>
          <label className="command-history-control">
            <span>History view</span>
            <select value={historySelection} onChange={(event) => patch({ historySelection: event.target.value })}>
              <option value="recent">Recent facility readings</option>
              <option value="selected">Selected device readings</option>
            </select>
          </label>
          <p>Timestamp-ordered history will appear when the history store is connected.</p>
        </section>
      </div>
    </main>
  );
}
