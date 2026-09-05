import {
  getSensorCategoryLabel,
  getTelemetryValueKindLabel,
} from "../../api/sensorOptions";
import { SensorAttachmentsPanel } from "./SensorAttachmentsPanel";
import { TelemetryHistoryPanel } from "./TelemetryHistoryPanel";
import { useSensorDetail } from "./useSensorDetail";

function formatExpectedRange(sensor) {
  const minimum = sensor.expectedMinimum;
  const maximum = sensor.expectedMaximum;
  const unit = sensor.unit ? ` ${sensor.unit}` : "";

  if (minimum == null && maximum == null) {
    return "No expected range configured";
  }

  if (minimum == null) {
    return `Up to ${maximum}${unit}`;
  }

  if (maximum == null) {
    return `From ${minimum}${unit}`;
  }

  return `${minimum}${unit} to ${maximum}${unit}`;
}

export function SensorDetail({
  sensorId,
  successMessage,
  onBack,
}) {
  const {
    sensor,
    isLoading,
    error,
    refresh,
  } = useSensorDetail(sensorId);

  if (isLoading) {
    return (
      <section className="state-panel" aria-live="polite">
        <h2>Loading sensor details</h2>
        <p>Retrieving the selected device configuration.</p>
      </section>
    );
  }

  if (error) {
    return (
      <section className="state-panel error-panel" role="alert">
        <h2>
          {error.status === 404
            ? "Sensor not found"
            : "Unable to load sensor"}
        </h2>
        <p>{error.message}</p>
        {error.traceId && <small>Trace ID: {error.traceId}</small>}
        <div className="state-actions">
          <button type="button" onClick={onBack}>
            Back to sensors
          </button>
          {error.status !== 404 && (
            <button type="button" onClick={refresh}>
              Try again
            </button>
          )}
        </div>
      </section>
    );
  }

  return (
    <section className="sensor-detail" aria-labelledby="sensor-detail-title">
      <div className="detail-toolbar">
        <button type="button" className="text-button" onClick={onBack}>
          ← Back to sensors
        </button>
        <span className="value-kind">
          {getTelemetryValueKindLabel(sensor.valueKind)}
        </span>
      </div>

      {successMessage && (
        <div className="success-banner" role="status">
          {successMessage}
        </div>
      )}

      <header className="detail-header">
        <p className="sensor-category">
          {getSensorCategoryLabel(sensor.category)}
        </p>
        <h2 id="sensor-detail-title">{sensor.friendlyName}</h2>
        <p className="monospace">{sensor.macAddress}</p>
      </header>

      <dl className="detail-grid">
        <div>
          <dt>Measured property</dt>
          <dd>{sensor.measuredProperty}</dd>
        </div>

        <div>
          <dt>Telemetry type</dt>
          <dd>{getTelemetryValueKindLabel(sensor.valueKind)}</dd>
        </div>

        <div>
          <dt>Unit</dt>
          <dd>{sensor.unit || "No unit configured"}</dd>
        </div>

        <div>
          <dt>Expected range</dt>
          <dd>{formatExpectedRange(sensor)}</dd>
        </div>

        <div>
          <dt>Deployment location</dt>
          <dd>{sensor.deploymentLocation.name}</dd>
          <small>{sensor.deploymentLocation.code}</small>
        </div>

        <div>
          <dt>Location level</dt>
          <dd>{sensor.deploymentLocation.nodeType}</dd>
        </div>

        <div className="detail-id">
          <dt>Sensor identifier</dt>
          <dd className="monospace">{sensor.id}</dd>
        </div>
      </dl>

      <TelemetryHistoryPanel
        sensorId={sensor.id}
        expectedMinimum={sensor.expectedMinimum}
        expectedMaximum={sensor.expectedMaximum}
      />
      <SensorAttachmentsPanel sensorId={sensor.id} />
    </section>
  );
}
