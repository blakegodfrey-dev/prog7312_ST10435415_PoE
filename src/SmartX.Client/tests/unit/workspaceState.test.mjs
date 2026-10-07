import assert from 'node:assert/strict';
import test from 'node:test';
import { createWorkspaceState, getSensorHistoryState, workspaceReducer } from '../../src/state/workspaceState.js';

function act(state, type, values) {
  return workspaceReducer(state, { type, ...values });
}

test('Telemetry filters, selected profile and history survive commands and Home', () => {
  let state = act(createWorkspaceState(), 'telemetry/patch', { patch: {
    search: 'Nutrient', category: 'Environmental', deploymentNodeId: 'reservoir-1', selectedSensorId: 'ph',
  } });
  state = act(state, 'history/patch', { sensorId: 'ph', patch: {
    validityFilter: 'invalid', page: 2, selectedReadingId: 'anomaly-1',
  } });
  const telemetry = state.telemetry;
  const history = state.historyBySensor;
  for (const view of ['commands', 'startup', 'sensors']) state = act(state, 'navigate', { view });
  assert.strictEqual(state.telemetry, telemetry);
  assert.strictEqual(state.historyBySensor, history);
  assert.equal(state.activeView, 'sensors');
});

test('Command Stream choices survive module changes and do not change Telemetry', () => {
  let state = createWorkspaceState();
  const telemetry = state.telemetry;
  const choices = { search: 'pump', category: 'Actuator', alertFilter: 'active', selectedDeviceId: 'pump-a', historySelection: 'selected' };
  state = act(state, 'commands/patch', { patch: choices });
  for (let i = 0; i < 20; i++) {
    state = act(state, 'navigate', { view: 'sensors' });
    state = act(state, 'navigate', { view: 'commands' });
  }
  assert.deepEqual(state.commandStream, choices);
  assert.strictEqual(state.telemetry, telemetry);
});

test('Different sensors retain independent history pages and validity choices', () => {
  let state = act(createWorkspaceState(), 'history/patch', { sensorId: 'ph', patch: { page: 3, validityFilter: 'invalid' } });
  state = act(state, 'history/patch', { sensorId: 'pump', patch: { page: 2, validityFilter: 'valid' } });
  assert.equal(getSensorHistoryState(state, 'ph').page, 3);
  assert.equal(getSensorHistoryState(state, 'pump').validityFilter, 'valid');
  assert.deepEqual(getSensorHistoryState(state, 'new'), { page: 1, validityFilter: 'all', selectedReadingId: null });
});

test('Navigation preserves a pending registration and draft without opening another module', () => {
  let state = act(createWorkspaceState(), 'registration/patch', { patch: { friendlyName: 'Pump B', valueKind: 'Boolean' } });
  state = act(state, 'telemetry/patch', { patch: { isRegistering: true, isRegistrationSubmitting: true } });
  state = act(state, 'navigate', { view: 'commands' });
  assert.equal(state.telemetry.registrationForm.friendlyName, 'Pump B');
  assert.equal(state.telemetry.isRegistrationSubmitting, true);
  state = act(state, 'telemetry/patch', { patch: { isRegistrationSubmitting: false, isRegistering: false, selectedSensorId: 'pump-b' } });
  assert.equal(state.activeView, 'commands');
  assert.equal(state.telemetry.selectedSensorId, 'pump-b');
});

test('Cancelled draft reset leaves directory and command choices intact', () => {
  let state = act(createWorkspaceState(), 'telemetry/patch', { patch: { search: 'Nutrient', registrationError: { message: 'Previous failure' } } });
  state = act(state, 'registration/patch', { patch: { friendlyName: 'Temporary' } });
  state = act(state, 'registration/reset', {});
  assert.equal(state.telemetry.registrationForm.friendlyName, '');
  assert.equal(state.telemetry.registrationError, null);
  assert.equal(state.telemetry.search, 'Nutrient');
  assert.equal(state.commandStream.search, '');
});

test('Final-PoE and unknown navigation cannot activate an unsupported module', () => {
  const state = createWorkspaceState();
  assert.strictEqual(act(state, 'navigate', { view: 'topology' }), state);
  assert.strictEqual(act(state, 'navigate', { view: 'unknown' }), state);
});

test('A fresh browser application gets its own state rather than another session draft', () => {
  const first = createWorkspaceState();
  first.telemetry.registrationForm.friendlyName = 'Private draft';
  const second = createWorkspaceState();
  assert.equal(second.telemetry.registrationForm.friendlyName, '');
  assert.equal(second.telemetry.selectedSensorId, null);
});
