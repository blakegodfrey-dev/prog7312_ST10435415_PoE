import { useState } from 'react';
import { typedValue, rangeText, valueText } from './commandModel.js';

// Each segment ends at a missing interval; time gaps never become zero values.
export function CommandTelemetryChart({ device, readings, gapSeconds }) {
  const [selectedId, setSelectedId] = useState(null);
  const points = readings.filter(r => r.sensorId === device.id && typedValue(r) !== null)
    .sort((a, b) => Date.parse(a.recordedAtUtc) - Date.parse(b.recordedAtUtc) || Date.parse(a.receivedAtUtc) - Date.parse(b.receivedAtUtc) || a.id.localeCompare(b.id));
  if (!points.length) return <p>No readings available to plot for this device.</p>;
  const numeric = r => Number(typedValue(r));
  const range = device.valueKind !== 'Boolean' && device.expectedMinimum != null && device.expectedMaximum != null;
  const values = points.map(numeric).concat(range ? [device.expectedMinimum, device.expectedMaximum] : []);
  const low = Math.min(...values), high = Math.max(...values);
  const min = low - (high - low || 1) * .1, max = high + (high - low || 1) * .1;
  const start = Date.parse(points[0].recordedAtUtc), span = Date.parse(points.at(-1).recordedAtUtc) - start || 1;
  const x = r => 55 + ((Date.parse(r.recordedAtUtc) - start) / span) * 610;
  const y = value => 170 - ((value - min) / (max - min)) * 130;
  const segments = [];
  let current = [];
  points.forEach((r, index) => {
    if (index > 0 && Date.parse(r.recordedAtUtc) - Date.parse(points[index - 1].recordedAtUtc) > gapSeconds * 1000) {
      segments.push(current); current = [];
    }
    current.push(`${x(r)},${y(numeric(r))}`);
  });
  segments.push(current);
  const selected = points.find(r => r.id === selectedId);
  return <div className="operation-chart"><p>{device.friendlyName} · {rangeText(device)}</p>
    <svg viewBox="0 0 720 225" role="img" aria-label={`${device.friendlyName} chronological telemetry; gaps break the line`}>
      <title>{device.friendlyName}: native readings with anomaly markers and missing intervals</title>
      {range && <><rect x="55" y={y(device.expectedMaximum)} width="610" height={Math.max(1, y(device.expectedMinimum) - y(device.expectedMaximum))} fill="#d9f3e7" /><text x="55" y="25" fontSize="12">Expected {device.expectedMinimum}–{device.expectedMaximum} {device.unit}</text></>}
      <path d="M55 40 V170 H665" fill="none" stroke="#869999" />
      <text x="5" y="45" fontSize="12">{device.valueKind === 'Boolean' ? 'On' : high.toFixed(1)}</text><text x="5" y="170" fontSize="12">{device.valueKind === 'Boolean' ? 'Off' : low.toFixed(1)}</text>
      {segments.map((segment, i) => <polyline key={i} points={segment.join(' ')} fill="none" stroke="#00746a" strokeWidth="2" />)}
      {points.map(r => <circle key={r.id} cx={x(r)} cy={y(numeric(r))} r={r.isValid ? 4 : 7} fill={r.isValid ? '#00746a' : '#b65d13'} stroke={r.id === selectedId ? '#000' : '#fff'} strokeWidth="2" role="button" tabIndex="0" aria-label={`${r.isValid ? 'Reading' : 'Anomaly'} ${valueText(r)} at ${new Date(r.recordedAtUtc).toLocaleString()}`} onClick={() => setSelectedId(r.id)} onKeyDown={e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); setSelectedId(r.id); } }}><title>{valueText(r)} {device.unit} · {r.isValid ? 'Valid' : r.validationMessage}</title></circle>)}
      <text x="55" y="200" fontSize="12">{new Date(points[0].recordedAtUtc).toLocaleTimeString()}</text><text x="665" y="200" textAnchor="end" fontSize="12">{new Date(points.at(-1).recordedAtUtc).toLocaleTimeString()}</text>
    </svg>
    {selected && <div role="status"><strong>{selected.isValid ? 'Selected reading' : 'Selected anomaly'}</strong><p>{device.macAddress} · {device.measuredProperty} · {valueText(selected)} {device.unit}<br />Recorded {new Date(selected.recordedAtUtc).toLocaleString()} · Received {new Date(selected.receivedAtUtc).toLocaleString()}<br />{selected.validationMessage ?? 'Valid reading'} · {rangeText(device)}</p></div>}
  </div>;
}
