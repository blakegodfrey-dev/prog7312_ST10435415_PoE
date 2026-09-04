import { useState } from "react";
import {
  SENSOR_CATEGORIES,
  getSensorCategoryLabel,
  getTelemetryValueKindLabel,
} from "../../api/sensorOptions";
import { SensorDetail } from "./SensorDetail";
import { SensorRegistration } from "./SensorRegistration";
import { useSensorDirectory } from "./useSensorDirectory";

export function SensorDirectory({ onBack }) {
  const [search, setSearch] = useState("");
  const [category, setCategory] = useState("");
  const [deploymentNodeId, setDeploymentNodeId] = useState("");
  const [selectedSensorId, setSelectedSensorId] = useState(null);
  const [isRegistering, setIsRegistering] = useState(false);

  const {
    sensors,
    locations,
    isLoading,
    error,
    refresh,
  } = useSensorDirectory({
    search,
    category,
    deploymentNodeId,
  });

  if (isRegistering) {
    return (
      <main className="app-shell sensor-workspace">
        <SensorRegistration
          locations={locations}
          onCancel={() => setIsRegistering(false)}
          onRegistered={(sensor) => {
            setIsRegistering(false);
            refresh();
            setSelectedSensorId(sensor.id);
          }}
        />
      </main>
    );
  }

  if (selectedSensorId) {
    return (
      <main className="app-shell sensor-workspace">
        <SensorDetail
          sensorId={selectedSensorId}
          onBack={() => setSelectedSensorId(null)}
        />
      </main>
    );
  }

  return (
    <main className="app-shell sensor-workspace">
      <header className="workspace-header">
        <div>
          <p className="eyebrow">SENSOR DATA INGESTION</p>
          <h1>Sensor directory</h1>
          <p className="hero-copy">
            Find registered devices by name, MAC address, category or
            deployment location.
          </p>
        </div>

        <button type="button" className="secondary-button" onClick={onBack}>
          Back to startup
        </button>
      </header>

      <section className="filter-panel" aria-label="Sensor filters">
        <label>
          <span>Search sensors</span>
          <input
            type="search"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Name, MAC address or property"
          />
        </label>

        <label>
          <span>Category</span>
          <select
            value={category}
            onChange={(event) => setCategory(event.target.value)}
          >
            <option value="">All categories</option>
            {SENSOR_CATEGORIES.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </label>

        <label>
          <span>Location</span>
          <select
            value={deploymentNodeId}
            onChange={(event) =>
              setDeploymentNodeId(event.target.value)
            }
          >
            <option value="">All sensor locations</option>
            {locations.map((location) => (
              <option key={location.id} value={location.id}>
                {location.name} ({location.code})
              </option>
            ))}
          </select>
        </label>
      </section>

      <div className="directory-heading">
        <div>
          <h2>Registered sensors</h2>
          {!isLoading && !error && (
            <p aria-live="polite">
              {sensors.length} {sensors.length === 1 ? "sensor" : "sensors"}
            </p>
          )}
        </div>

        <button
          type="button"
          className="primary-button"
          onClick={() => setIsRegistering(true)}
        >
          Register sensor
        </button>
      </div>

      {isLoading && (
        <section className="state-panel" aria-live="polite">
          <h2>Loading sensors</h2>
          <p>Retrieving the latest registered devices and locations.</p>
        </section>
      )}

      {!isLoading && error && (
        <section className="state-panel error-panel" role="alert">
          <h2>Unable to load sensors</h2>
          <p>{error.message}</p>
          {error.traceId && <small>Trace ID: {error.traceId}</small>}
          <button type="button" onClick={refresh}>
            Try again
          </button>
        </section>
      )}

      {!isLoading && !error && sensors.length === 0 && (
        <section className="state-panel">
          <h2>No matching sensors</h2>
          <p>
            Adjust the filters or register the first sensor for this
            deployment.
          </p>
        </section>
      )}

      {!isLoading && !error && sensors.length > 0 && (
        <section className="sensor-grid" aria-label="Registered sensors">
          {sensors.map((sensor) => (
            <article className="sensor-card" key={sensor.id}>
              <div className="sensor-card-heading">
                <div>
                  <p className="sensor-category">
                    {getSensorCategoryLabel(sensor.category)}
                  </p>
                  <h2>{sensor.friendlyName}</h2>
                </div>
                <span className="value-kind">
                  {getTelemetryValueKindLabel(sensor.valueKind)}
                </span>
              </div>

              <dl className="sensor-summary">
                <div>
                  <dt>Measures</dt>
                  <dd>
                    {sensor.measuredProperty}
                    {sensor.unit ? ` (${sensor.unit})` : ""}
                  </dd>
                </div>
                <div>
                  <dt>Location</dt>
                  <dd>
                    {sensor.deploymentLocation.name}
                    <small>{sensor.deploymentLocation.code}</small>
                  </dd>
                </div>
                <div>
                  <dt>MAC address</dt>
                  <dd className="monospace">{sensor.macAddress}</dd>
                </div>
              </dl>

              <button
                type="button"
                className="text-button"
                onClick={() => setSelectedSensorId(sensor.id)}
              >
                View sensor details
              </button>
            </article>
          ))}
        </section>
      )}
    </main>
  );
}
