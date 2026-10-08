import test from 'node:test';
import assert from 'node:assert/strict';
import { typedValue, valueText, filterDevices, timelineRows, rangeText } from '../../src/features/commands/commandModel.js';

test('Native zero and false survive display and missing stays missing', () => {
  assert.equal(typedValue({ integerValue: 0 }), 0); assert.equal(typedValue({ booleanValue: false }), false);
  assert.equal(valueText({ booleanValue: false }), 'Off'); assert.equal(valueText(null), 'No reading');
});
const rows = [{ device: { id: 'a', friendlyName: 'Pump A', macAddress: 'AA:BB', measuredProperty: 'Relay state', category: 'Actuator' }, connection: { state: 'Connected' } },
  { device: { id: 'b', friendlyName: 'Moisture', macAddress: 'CC:DD', measuredProperty: 'Soil moisture', category: 'Environmental' }, connection: { state: 'Disconnected' } }];
const incidents = [{ deviceId: 'b', severity: 'critical' }];
test('Filters combine identity, property, category and alert lifecycle', () => {
  assert.equal(filterDevices(rows, incidents, { search: 'relay', category: 'Actuator', alertFilter: 'healthy' })[0].device.id, 'a');
  assert.equal(filterDevices(rows, incidents, { search: 'CC', alertFilter: 'critical' })[0].device.id, 'b');
  assert.equal(filterDevices(rows, incidents, { category: 'Actuator', alertFilter: 'active' }).length, 0);
  assert.equal(filterDevices(rows, incidents, { alertFilter: 'disconnected' }).length, 1);
});
test('Chronological history keeps equal timestamps and inserts gaps per device', () => {
  const rs = [{ id: '3', sensorId: 'a', recordedAtUtc: '2026-10-07T10:02:00Z' }, { id: '2', sensorId: 'b', recordedAtUtc: '2026-10-07T10:00:00Z' }, { id: '1', sensorId: 'a', recordedAtUtc: '2026-10-07T10:00:00Z' }];
  const result = timelineRows(rs, 30);
  assert.deepEqual(result.map(r => r.id), ['1', '2', 'gap-3', '3']);
  assert.equal(result[2].gap, true); assert.equal(result[2].floatValue, undefined);
});
test('Range context never invents numeric bounds for Boolean or missing configuration', () => {
  assert.match(rangeText({ valueKind: 'Boolean' }), /On \/ Off/);
  assert.match(rangeText({ valueKind: 'Float', expectedMinimum: null }), /not configured/);
  assert.equal(rangeText({ valueKind: 'Integer', expectedMinimum: 0, expectedMaximum: 300, unit: 'W' }), 'Expected 0–300 W');
});
