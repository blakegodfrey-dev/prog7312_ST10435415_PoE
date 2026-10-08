import { useEffect, useState } from 'react';
import { SENSOR_CATEGORIES } from '../../api/sensorOptions.js';
import { operationsApi } from '../../api/operationsApi.js';
import { useWorkspaceState } from '../../state/useWorkspaceState.js';
import { filterDevices, rangeText, timelineRows, valueText } from './commandModel.js';
import { CommandTelemetryChart } from './CommandTelemetryChart.jsx';
import { SensorAttachmentsPanel } from '../sensors/SensorAttachmentsPanel.jsx';

const stamp = value => value ? new Date(value).toLocaleString() : 'No gateway evidence';

export function CommandStreamWorkspace() {
  const { state, dispatch } = useWorkspaceState();
  const choices = state.commandStream;
  const { search, category, alertFilter, historySelection, selectedDeviceId, commandPending, commandFeedback } = choices;
  const patch = values => dispatch({ type: 'commands/patch', patch: values });
  const [snapshot, setSnapshot] = useState(null);
  const [error, setError] = useState(null);
  const [refresh, setRefresh] = useState(0);
  const [updatedAt, setUpdatedAt] = useState(null);
  useEffect(() => {
    const controller = new AbortController();
    let timer;
    async function poll() {
      try {
        const data = await operationsApi.dashboard(controller.signal);
        if (controller.signal.aborted) return;
        setSnapshot(data); setError(null); setUpdatedAt(new Date());
      } catch (e) { if (!controller.signal.aborted) setError(e.message); }
      finally { if (!controller.signal.aborted) timer = setTimeout(poll, 2000); }
    }
    poll();
    return () => { controller.abort(); clearTimeout(timer); };
  }, [refresh]);

  const incidents = snapshot?.activeIncidents ?? [];
  const devices = snapshot?.devices ?? [];
  const rows = filterDevices(devices, incidents, choices);
  const selected = devices.find(r => r.device.id === selectedDeviceId);
  const d = selected?.device;
  const canCommand = d?.category === 'Actuator' && d?.valueKind === 'Boolean' && selected?.connection.state === 'Connected' && d?.latestReading?.isValid && !error;
  const byId = new Map(devices.map(r => [r.device.id, r.device]));
  const history = (snapshot?.history ?? []).filter(r => historySelection !== 'selected' || r.sensorId === selectedDeviceId);
  const timeline = timelineRows(history, snapshot?.staleSeconds ?? 30);

  async function act(action) {
    if (commandPending) return;
    patch({ commandPending: true, commandFeedback: null });
    try {
      const result = await action();
      patch({ commandFeedback: `${result.successful ? 'Success' : 'Failed'}: ${result.message}` });
      setRefresh(n => n + 1);
    } catch (e) { patch({ commandFeedback: e.message }); }
    finally { patch({ commandPending: false }); }
  }
  async function inspect(id, kind = 'view', query = '') {
    patch({ selectedDeviceId: id });
    try { await operationsApi.interaction(id, kind, query); }
    catch (e) { patch({ commandFeedback: `Device selected; history recording failed: ${e.message}` }); }
  }
  async function submitSearch(e) {
    e.preventDefault();
    if (!search.trim() || !rows.length) return;
    await inspect(rows[0].device.id, 'search', search.trim());
  }

  return <main className="app-shell sensor-workspace command-workspace">
    <header className="workspace-header"><div><p className="eyebrow">SMART HYDROPONIC FACILITY</p>
      <h1>Real-Time Command Stream and History</h1><p className="hero-copy">Monitor gateway activity, investigate incidents and control simulated ESP32 actuators.</p></div></header>
    <p role="status">{snapshot ? `Last refreshed ${updatedAt?.toLocaleTimeString()}` : 'Loading operational data…'}</p>
    {error && <div role="alert" className="command-phase-notice">{error} {snapshot && 'Displayed data may be stale.'} <button type="button" onClick={() => setRefresh(n => n + 1)}>Retry</button></div>}
    {commandFeedback && <p role="status" className="command-phase-notice">{commandFeedback}</p>}
    <form className="filter-panel" aria-label="Command Stream filters" onSubmit={submitSearch}>
      <label><span>Search devices</span><input type="search" value={search} onChange={e => patch({ search: e.target.value })} placeholder="Name, MAC address or property" /></label>
      <label><span>Device category</span><select value={category} onChange={e => patch({ category: e.target.value })}><option value="">All categories</option>{SENSOR_CATEGORIES.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}</select></label>
      <label><span>Alert state</span><select value={alertFilter} onChange={e => patch({ alertFilter: e.target.value })}>{[['all', 'All devices'], ['active', 'Active alerts'], ['critical', 'Critical'], ['disconnected', 'Disconnected'], ['healthy', 'Healthy']].map(([v, label]) => <option key={v} value={v}>{label}</option>)}</select></label>
      <button type="submit" className="secondary-button">Find and record search</button>
    </form>
    <section className="command-shell-panel" aria-labelledby="suggestions-title"><h2 id="suggestions-title">Suggested Actions / Automated Insights</h2>
      {!snapshot?.suggestions?.length && <p>No supported pattern for the current context. Suggestions require at least three matching historical actions with 60% confidence.</p>}
      {snapshot?.suggestions?.map(s => <article key={`${s.targetId}-${s.action}`} className="operation-insight"><h3>{byId.get(s.targetId)?.friendlyName}</h3><p>{s.reason}</p><p>Support: {s.support} / {s.contextObservations} · Confidence: {Math.round(s.confidence * 100)}%</p>
        {s.action === 'command' ? <button type="button" disabled={commandPending || !!error} onClick={() => act(() => operationsApi.command(s.targetId, s.desiredState))}>Approve: set {s.desiredState ? 'On' : 'Off'}</button> : <button type="button" onClick={() => inspect(s.targetId)}>Inspect device</button>}</article>)}
    </section>
    <div className="command-panel-grid">
      <section className="command-shell-panel" aria-labelledby="live-devices-title"><h2 id="live-devices-title">Live devices and telemetry</h2>
        {!rows.length && <p>{snapshot ? 'No devices match these filters.' : 'Waiting for the API.'}</p>}
        <div className="operation-device-list">{rows.map(({ device, connection }) => <button type="button" className="operation-device" aria-pressed={device.id === selectedDeviceId} key={device.id} onClick={() => inspect(device.id)}>
          <strong>{device.friendlyName}</strong><span>{device.macAddress} · {device.category}</span><span>{device.measuredProperty}: {valueText(device.latestReading)} {device.unit}</span>
          <span>{connection.state} · {incidents.filter(i => i.deviceId === device.id).length} active incidents</span><span>Recorded: {stamp(device.latestReading?.recordedAtUtc)}</span><span>Gateway last seen: {stamp(connection.lastSeenAtUtc)}</span></button>)}</div>
      </section>
      <section className="command-shell-panel" aria-labelledby="selected-device-title"><h2 id="selected-device-title">Selected device</h2>
        {!d ? <p>Select a device from the live list.</p> : <><h3>{d.friendlyName}</h3><p>{d.macAddress} · {d.measuredProperty} · {d.valueKind}</p><p>{rangeText(d)}</p><p>Current: {valueText(d.latestReading)} {d.unit} · {d.latestReading?.isValid === false ? 'Invalid reading' : d.latestReading ? 'Valid' : 'Missing'}</p>
          <p>Recorded: {stamp(d.latestReading?.recordedAtUtc)}<br />Gateway last seen: {stamp(selected.connection.lastSeenAtUtc)} · {selected.connection.state}</p>
          {selected.connection.state !== 'Connected' && <p>Live data unavailable. Last recorded value is historical; no zero reading is substituted.</p>}
          {d.category === 'Actuator' && <div className="operation-actions"><button type="button" disabled={!canCommand || commandPending || d.latestReading?.booleanValue === true} onClick={() => act(() => operationsApi.command(d.id, true))}>Set On</button><button type="button" disabled={!canCommand || commandPending || d.latestReading?.booleanValue === false} onClick={() => act(() => operationsApi.command(d.id, false))}>Set Off</button></div>}
          <SensorAttachmentsPanel key={d.id} sensorId={d.id} />
        </>}
      </section>
      <section className="command-shell-panel" aria-labelledby="active-alerts-title"><h2 id="active-alerts-title">Active alerts ({incidents.length})</h2>
        {!incidents.length && <p>No active incidents.</p>}{incidents.map(i => <IncidentCard key={i.id} incident={i} name={byId.get(i.deviceId)?.friendlyName} onSaved={() => setRefresh(n => n + 1)} />)}
        <details><summary>Recent incident recovery events</summary>{snapshot?.events.filter(i => i.resolvedAtUtc).map(i => <p key={i.id}>{byId.get(i.deviceId)?.friendlyName}: {i.type} recovered at {stamp(i.resolvedAtUtc)}</p>)}</details>
      </section>
      <section className="command-shell-panel" aria-labelledby="processing-status-title"><h2 id="processing-status-title">Processing status</h2>
        {snapshot ? <dl className="operation-counts">{Object.entries({ 'Normal FIFO work items': snapshot.processing.normalCount, 'Priority work items': snapshot.processing.priorityCount, 'Queued readings / heartbeats': snapshot.processing.pendingReadings, Worker: snapshot.processing.workerState, 'Committed work items': snapshot.processing.completed, 'Failed work items': snapshot.processing.failed, 'Rejected work items': snapshot.processing.rejected, Devices: devices.length, 'Active incidents': incidents.length, 'Available Undo entries': snapshot.undoCount }).map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}</dl> : <p>Waiting for processing status.</p>}
        <p>Critical packets run before waiting ordinary packets. An in-flight write completes first.</p>
      </section>
      <section className="command-shell-panel" aria-labelledby="command-history-title"><h2 id="command-history-title">Command history</h2>
        <button className="secondary-button" type="button" disabled={!snapshot?.undoCount || commandPending || !!error} onClick={() => act(operationsApi.undo)}>{commandPending ? 'Awaiting acknowledgement…' : 'Undo latest successful command'}</button>
        {!snapshot?.commands.length && <p>No command attempts yet.</p>}{snapshot?.commands.map(c => <article key={c.id}><p><strong>{byId.get(c.sensorId)?.friendlyName}</strong> {c.isUndo ? 'Undo' : 'Set'} {c.desiredState ? 'On' : 'Off'} · {c.successful ? 'Success' : 'Failed'} {c.undone ? '· Undone' : ''}</p><p>{stamp(c.atUtc)} · {c.message}</p></article>)}
      </section>
      <section className="command-shell-panel operation-history" aria-labelledby="command-telemetry-history-title"><h2 id="command-telemetry-history-title">Telemetry history</h2>
        <label className="command-history-control"><span>History view</span><select value={historySelection} onChange={e => patch({ historySelection: e.target.value })}><option value="recent">Recent facility readings</option><option value="selected">Selected device readings</option></select></label>
        <p>Chronological recent-memory window, up to 500 rows from {snapshot?.recentCapacity ?? '…'} retained readings. Full SQL history remains in Telemetry.</p>
        {d && <CommandTelemetryChart key={d.id} device={d} readings={snapshot?.history ?? []} gapSeconds={snapshot?.staleSeconds ?? 30} />}
        {!timeline.length && <p>No recorded telemetry in this view.</p>}
        <div className="operation-table-wrap"><table><thead><tr><th>Recorded time</th><th>Device</th><th>Reading</th><th>Context</th></tr></thead><tbody>{timeline.map(r => <tr key={r.id} className={r.gap || !r.isValid ? 'operation-invalid' : ''}><td>{r.gap ? `${stamp(r.fromUtc)} → ${stamp(r.toUtc)}` : stamp(r.recordedAtUtc)}</td><td>{byId.get(r.sensorId)?.friendlyName}</td><td>{r.gap ? 'Missing interval — gap' : `${valueText(r)} ${byId.get(r.sensorId)?.unit ?? ''}`}</td><td>{r.gap ? 'No readings recorded; no interpolation' : `${r.isValid ? 'Valid' : `Invalid: ${r.validationMessage}`} · ${byId.has(r.sensorId) ? rangeText(byId.get(r.sensorId)) : ''}`}</td></tr>)}</tbody></table></div>
        {devices.filter(row => row.connection.state === 'Disconnected' && (historySelection !== 'selected' || row.device.id === selectedDeviceId)).map(row => <p key={row.device.id}>Gap: {row.device.friendlyName} has no live readings after its last gateway contact ({stamp(row.connection.lastSeenAtUtc)}).</p>)}
      </section>
    </div>
  </main>;
}

function IncidentCard({ incident, name, onSaved }) {
  const [label, setLabel] = useState(incident.label);
  const [note, setNote] = useState(incident.note ?? '');
  const [feedback, setFeedback] = useState('');
  const [pending, setPending] = useState(false);
  async function save(e) {
    e.preventDefault(); setPending(true);
    try { await operationsApi.annotate(incident.id, label, note); setFeedback('Saved'); onSaved(); }
    catch (error) { setFeedback(error.message); }
    finally { setPending(false); }
  }
  return <article className="operation-incident"><h3>{name} · {incident.type} · {incident.severity}</h3><p>{incident.reason}</p><p>Opened {stamp(incident.openedAtUtc)} · Observations: {incident.observations}</p>
    <form onSubmit={save}><label>Incident label<select value={label} onChange={e => setLabel(e.target.value)}>{['unresolved', 'confirmed', 'expected', 'false positive'].map(l => <option key={l}>{l}</option>)}</select></label><label>Diagnostic note<textarea maxLength={500} value={note} onChange={e => setNote(e.target.value)} /></label><button type="submit" disabled={pending}>Save note</button><p role="status">{feedback}</p></form></article>;
}
