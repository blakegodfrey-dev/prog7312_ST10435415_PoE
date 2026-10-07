import assert from 'node:assert/strict';
import test from 'node:test';
import { INITIAL_SENSOR_FORM, createSensorRegistrationRequest, validateSensorForm } from '../../src/features/sensors/sensorRegistrationModel.js';

function form(overrides = {}) {
  return { ...INITIAL_SENSOR_FORM, macAddress: ' a4:cf:12:8b:40:01 ', friendlyName: ' Pump B ', measuredProperty: ' State ', deploymentNodeId: 'node-1', ...overrides };
}

test('Boolean type switch ignores retained disabled numeric fields and sends null bounds', () => {
  const input = form({ valueKind: 'Boolean', category: 'Actuator', expectedMinimum: '10', expectedMaximum: '2' });
  assert.deepEqual(validateSensorForm(input), {});
  const request = createSensorRegistrationRequest(input);
  assert.equal(request.expectedMinimum, null);
  assert.equal(request.expectedMaximum, null);
  assert.equal(request.valueKind, 'Boolean');
});

test('Float and integer registrations retain finite numeric bounds', () => {
  for (const valueKind of ['Float', 'Integer']) {
    const input = form({ valueKind, expectedMinimum: '1', expectedMaximum: '10' });
    assert.deepEqual(validateSensorForm(input), {});
    const request = createSensorRegistrationRequest(input);
    assert.equal(request.expectedMinimum, 1);
    assert.equal(request.expectedMaximum, 10);
    assert.equal(request.valueKind, valueKind);
  }
});

test('Registration trims names, normalises MAC and creates a packet-independent ID', () => {
  const request = createSensorRegistrationRequest(form());
  assert.equal(request.macAddress, 'A4:CF:12:8B:40:01');
  assert.equal(request.friendlyName, 'Pump B');
  assert.equal(request.measuredProperty, 'State');
  assert.match(request.id, /^[0-9a-f-]{36}$/i);
});

test('Missing identity/location and invalid numeric bounds remain actionable errors', () => {
  const errors = validateSensorForm(form({ macAddress: 'bad', friendlyName: '', deploymentNodeId: '', expectedMinimum: '5', expectedMaximum: '2' }));
  for (const field of ['macAddress', 'friendlyName', 'deploymentNodeId', 'expectedMaximum']) assert.ok(errors[field]);
  assert.ok(validateSensorForm(form({ expectedMinimum: 'Infinity' })).expectedMinimum);
  assert.equal(createSensorRegistrationRequest(form()).expectedMinimum, null);
});
