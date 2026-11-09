import assert from 'node:assert/strict';
import test from 'node:test';
import { buildHistoryRange, sastInputToUtc } from '../../src/features/sensors/historyRange.js';
import { createWorkspaceState, getSensorHistoryState, workspaceReducer } from '../../src/state/workspaceState.js';

test('Node A 14:00-16:00 SAST converts explicitly to 12:00-14:00 UTC', () => {
  assert.deepEqual(buildHistoryRange('2026-11-08T14:00', '2026-11-08T16:00'), {
    fromUtc: '2026-11-08T12:00:00.000Z', toUtc: '2026-11-08T14:00:00.000Z', error: null,
  });
  assert.equal(sastInputToUtc('2026-11-08T00:00:01'), '2026-11-07T22:00:01.000Z');
});

test('Open ranges and equal endpoints are allowed', () => {
  assert.deepEqual(buildHistoryRange(), { fromUtc: undefined, toUtc: undefined, error: null });
  assert.equal(buildHistoryRange('2026-11-08T14:00', '').error, null);
  assert.equal(buildHistoryRange('', '2026-11-08T16:00').error, null);
  assert.equal(buildHistoryRange('2026-11-08T14:00', '2026-11-08T14:00').error, null);
});

test('Invalid calendar values and reversed ranges produce clear errors', () => {
  for (const value of ['bad', '2026-02-30T12:00', '2026-13-01T12:00', '2026-11-08T25:00'])
    assert.ok(buildHistoryRange(value, '').error, value);
  assert.match(buildHistoryRange('2026-11-08T16:00', '2026-11-08T14:00').error, /cannot be after/);
});

test('Per-sensor date choices survive full module navigation', () => {
  let state = workspaceReducer(createWorkspaceState(), { type: 'history/patch', sensorId: 'a',
    patch: { fromLocal: '2026-11-08T14:00', toLocal: '2026-11-08T16:00', page: 2 } });
  for (const view of ['commands', 'startup', 'sensors']) state = workspaceReducer(state, { type: 'navigate', view });
  assert.equal(getSensorHistoryState(state, 'a').fromLocal, '2026-11-08T14:00');
  assert.equal(getSensorHistoryState(state, 'a').page, 2);
  assert.equal(getSensorHistoryState(state, 'b').fromLocal, '');
});
