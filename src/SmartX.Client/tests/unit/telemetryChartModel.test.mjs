import assert from 'node:assert/strict';
import test from 'node:test';
import { prepareTelemetryChart, telemetryTimeX } from '../../src/features/sensors/telemetryChartModel.js';
const reading = (id, minute, value = 1) => ({ id, valueKind: 'Float', floatValue: value,
  recordedAtUtc: new Date(Date.UTC(2026, 10, 8, 12, minute)).toISOString() });
test('Equal recorded timestamps share x and irregular intervals preserve actual elapsed time', () => {
  const { points } = prepareTelemetryChart([reading('d', 120), reading('c', 60), reading('b', 60), reading('a', 0)]);
  assert.deepEqual(points.map(p => p.id), ['a', 'b', 'c', 'd']);
  const x = p => telemetryTimeX(p.time, points[0].time, points.at(-1).time, 0, 100);
  assert.deepEqual(points.map(x), [0, 50, 50, 100]);
  assert.equal(telemetryTimeX(10, 10, 10, 20, 100), 70);
});
test('Native zero/false survive and explicit missing values break a plotted line', () => {
  const data = prepareTelemetryChart([reading('a', 0, 0), reading('b', 1, null),
    { ...reading('c', 2), valueKind: 'Boolean', booleanValue: false }]);
  assert.deepEqual(data.points.map(p => p.chartValue), [0, 0]);
  assert.equal(data.segments.length, 2);
});
test('Long page intervals split the line; invalid timestamps are not plotted', () => {
  const data = prepareTelemetryChart([reading('a', 0), reading('b', 1), reading('c', 2), reading('d', 20),
    { ...reading('bad', 0), recordedAtUtc: 'not-a-time' }]);
  assert.equal(data.points.length, 4);
  assert.equal(data.gaps.length, 1);
  assert.equal(data.segments.length, 2);
});
