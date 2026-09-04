import { useState } from "react";
import {
  SENSOR_CATEGORIES,
  TELEMETRY_VALUE_KINDS,
} from "../../api/sensorOptions";
import { INITIAL_SENSOR_FORM } from "./sensorRegistrationModel";
import { useSensorRegistration } from "./useSensorRegistration";

function FieldError({ id, message }) {
  if (!message) {
    return null;
  }

  const text = Array.isArray(message) ? message[0] : message;

  return (
    <span id={id} className="field-error">
      {text}
    </span>
  );
}

export function SensorRegistration({
  locations,
  onCancel,
  onRegistered,
}) {
  const [form, setForm] = useState({ ...INITIAL_SENSOR_FORM });
  const [validationErrors, setValidationErrors] = useState({});

  const {
    register,
    isSubmitting,
    submissionError,
    clearSubmissionError,
  } = useSensorRegistration();

  function updateField(event) {
    const { name, value } = event.target;

    setForm((current) => ({
      ...current,
      [name]: value,
    }));

    setValidationErrors((current) => ({
      ...current,
      [name]: undefined,
    }));

    clearSubmissionError();
  }

  async function handleSubmit(event) {
    event.preventDefault();

    const result = await register(form);
    setValidationErrors(result.validationErrors);

    if (result.sensor) {
      onRegistered(result.sensor);
    }
  }

  const isBoolean = form.valueKind === "Boolean";

  return (
    <section
      className="registration-panel"
      aria-labelledby="registration-title"
    >
      <header className="registration-header">
        <div>
          <p className="eyebrow">NEW DEVICE</p>
          <h2 id="registration-title">Register a sensor</h2>
          <p>
            Link a typed IoT sensor to a Node-level deployment location.
          </p>
        </div>

        <button
          type="button"
          className="secondary-button"
          onClick={onCancel}
          disabled={isSubmitting}
        >
          Cancel
        </button>
      </header>

      {submissionError && (
        <div className="form-error-summary" role="alert">
          <strong>Registration was not completed.</strong>
          <span>{submissionError.message}</span>
          {submissionError.traceId && (
            <small>Trace ID: {submissionError.traceId}</small>
          )}
        </div>
      )}

      <form className="sensor-form" onSubmit={handleSubmit} noValidate>
        <div className="form-section">
          <div className="form-section-heading">
            <h3>Device identity</h3>
            <p>Use a clear name and the device’s unique MAC address.</p>
          </div>

          <div className="form-fields">
            <label>
              <span>Friendly name</span>
              <input
                name="friendlyName"
                value={form.friendlyName}
                onChange={updateField}
                maxLength="150"
                aria-describedby="friendlyName-error"
                aria-invalid={Boolean(validationErrors.friendlyName)}
              />
              <FieldError
                id="friendlyName-error"
                message={validationErrors.friendlyName}
              />
            </label>

            <label>
              <span>MAC address</span>
              <input
                name="macAddress"
                value={form.macAddress}
                onChange={updateField}
                maxLength="17"
                placeholder="A4:CF:12:8B:40:01"
                className="monospace"
                aria-describedby="macAddress-error"
                aria-invalid={Boolean(validationErrors.macAddress)}
              />
              <FieldError
                id="macAddress-error"
                message={validationErrors.macAddress}
              />
            </label>
          </div>
        </div>

        <div className="form-section">
          <div className="form-section-heading">
            <h3>Telemetry configuration</h3>
            <p>Keep the reading in its native float, integer or boolean type.</p>
          </div>

          <div className="form-fields">
            <label>
              <span>Category</span>
              <select
                name="category"
                value={form.category}
                onChange={updateField}
              >
                {SENSOR_CATEGORIES.map((category) => (
                  <option key={category.value} value={category.value}>
                    {category.label}
                  </option>
                ))}
              </select>
            </label>

            <label>
              <span>Telemetry value type</span>
              <select
                name="valueKind"
                value={form.valueKind}
                onChange={updateField}
              >
                {TELEMETRY_VALUE_KINDS.map((kind) => (
                  <option key={kind.value} value={kind.value}>
                    {kind.label}
                  </option>
                ))}
              </select>
            </label>

            <label>
              <span>Measured property</span>
              <input
                name="measuredProperty"
                value={form.measuredProperty}
                onChange={updateField}
                maxLength="100"
                placeholder="Air temperature"
                aria-describedby="measuredProperty-error"
                aria-invalid={Boolean(validationErrors.measuredProperty)}
              />
              <FieldError
                id="measuredProperty-error"
                message={validationErrors.measuredProperty}
              />
            </label>

            <label>
              <span>Unit</span>
              <input
                name="unit"
                value={form.unit}
                onChange={updateField}
                maxLength="30"
                placeholder="°C"
                aria-describedby="unit-error"
                aria-invalid={Boolean(validationErrors.unit)}
              />
              <FieldError
                id="unit-error"
                message={validationErrors.unit}
              />
            </label>
          </div>
        </div>

        <div className="form-section">
          <div className="form-section-heading">
            <h3>Deployment and expected range</h3>
            <p>
              Sensors may only be registered against the final Node level.
            </p>
          </div>

          <div className="form-fields">
            <label className="full-field">
              <span>Deployment location</span>
              <select
                name="deploymentNodeId"
                value={form.deploymentNodeId}
                onChange={updateField}
                aria-describedby="deploymentNodeId-error"
                aria-invalid={Boolean(
                  validationErrors.deploymentNodeId
                )}
              >
                <option value="">Select a sensor location</option>
                {locations.map((location) => (
                  <option key={location.id} value={location.id}>
                    {location.name} ({location.code})
                  </option>
                ))}
              </select>
              <FieldError
                id="deploymentNodeId-error"
                message={validationErrors.deploymentNodeId}
              />
            </label>

            <label>
              <span>Expected minimum</span>
              <input
                type="number"
                step="any"
                name="expectedMinimum"
                value={form.expectedMinimum}
                onChange={updateField}
                disabled={isBoolean}
                aria-describedby="expectedMinimum-error"
                aria-invalid={Boolean(
                  validationErrors.expectedMinimum
                )}
              />
              <FieldError
                id="expectedMinimum-error"
                message={validationErrors.expectedMinimum}
              />
            </label>

            <label>
              <span>Expected maximum</span>
              <input
                type="number"
                step="any"
                name="expectedMaximum"
                value={form.expectedMaximum}
                onChange={updateField}
                disabled={isBoolean}
                aria-describedby="expectedMaximum-error"
                aria-invalid={Boolean(
                  validationErrors.expectedMaximum
                )}
              />
              <FieldError
                id="expectedMaximum-error"
                message={validationErrors.expectedMaximum}
              />
            </label>

            {isBoolean && (
              <p className="full-field field-help">
                Boolean sensors use true/false states and therefore do not
                require a numeric expected range.
              </p>
            )}
          </div>
        </div>

        <div className="form-actions">
          <button
            type="button"
            className="secondary-button"
            onClick={onCancel}
            disabled={isSubmitting}
          >
            Cancel
          </button>

          <button
            type="submit"
            className="primary-button submit-button"
            disabled={isSubmitting || locations.length === 0}
          >
            {isSubmitting ? "Registering..." : "Register sensor"}
          </button>
        </div>
      </form>
    </section>
  );
}

