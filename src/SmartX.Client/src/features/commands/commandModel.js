export function typedValue(reading) {
  if (!reading) return null;
  return reading.floatValue ?? reading.integerValue ?? reading.booleanValue ?? null;
}
export function valueText(reading) {
  const value = typedValue(reading);
  return value === null ? 'No reading' : typeof value === 'boolean' ? (value ? 'On' : 'Off') : String(value);
}
export function filterDevices(rows, incidents, { search = '', category = '', alertFilter = 'all' }) {
  const query = search.trim().toLowerCase();
  return rows.filter(({ device: d, connection }) => {
    const alerts = incidents.filter(i => i.deviceId === d.id);
    return (!query || [d.friendlyName, d.macAddress, d.measuredProperty].some(v => v.toLowerCase().includes(query))) &&
      (!category || d.category === category) &&
      (alertFilter === 'all' || (alertFilter === 'active' && alerts.length > 0) ||
        (alertFilter === 'critical' && alerts.some(i => i.severity === 'critical')) ||
        (alertFilter === 'disconnected' && connection.state === 'Disconnected') ||
        (alertFilter === 'healthy' && alerts.length === 0 && connection.state === 'Connected'));
  });
}
export function rangeText(device) {
  return device.valueKind === 'Boolean' ? 'Boolean actuator: On / Off' :
    device.expectedMinimum == null || device.expectedMaximum == null ? 'Expected range not configured' :
      `Expected ${device.expectedMinimum}–${device.expectedMaximum} ${device.unit}`;
}
export function timelineRows(readings, gapSeconds) {
  const previous = new Map();
  const rows = [];
  for (const r of [...readings].sort((a, b) => Date.parse(a.recordedAtUtc) - Date.parse(b.recordedAtUtc) || Date.parse(a.receivedAtUtc) - Date.parse(b.receivedAtUtc) || a.id.localeCompare(b.id))) {
    const last = previous.get(r.sensorId);
    if (last && Date.parse(r.recordedAtUtc) - Date.parse(last.recordedAtUtc) > gapSeconds * 1000)
      rows.push({ id: `gap-${r.id}`, sensorId: r.sensorId, gap: true, fromUtc: last.recordedAtUtc, toUtc: r.recordedAtUtc });
    rows.push(r); previous.set(r.sensorId, r);
  }
  return rows;
}
