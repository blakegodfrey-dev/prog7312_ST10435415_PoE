import { useState } from "react";
import {
  formatTelemetryTimestamp,
  formatTelemetryValue,
  getConnectionStatusLabel,
} from "./telemetryDisplay";
import { useSensorTelemetry } from "./useSensorTelemetry";
import { TelemetryChart } from "./TelemetryChart";

const PAGE_SIZE = 25;

function toValidityFilter(value) {
  if (value === "valid") {
    return true;
  }

  if (value === "invalid") {
    return false;
  }

  return undefined;
}

export function TelemetryHistoryPanel({
    sensorId,
    expectedMinimum,
    expectedMaximum,
  }) {
  const [validityFilter, setValidityFilter] = useState("all");
  const [page, setPage] = useState(1);

  const {
    history,
    connectionStatus,
    isLoading,
    error,
    refresh,
  } = useSensorTelemetry({
    sensorId,
    isValid: toValidityFilter(validityFilter),
    page,
    pageSize: PAGE_SIZE,
  });

  const pageCount = history
    ? Math.max(1, Math.ceil(history.totalCount / history.pageSize))
    : 1;

  function changeValidityFilter(event) {
    setValidityFilter(event.target.value);
    setPage(1);
  }

  return (
    <section
      className="telemetry-history"
      aria-labelledby="telemetry-history-title"
    >
      <div className="telemetry-heading">
        <div>
          <p className="eyebrow">RECENT READINGS</p>
          <h2 id="telemetry-history-title">Telemetry history</h2>
        </div>

        {connectionStatus && (
          <div
            className={
              `connection-badge connection-${connectionStatus.status}`
            }
            aria-label={`Connection status: ${
              getConnectionStatusLabel(connectionStatus.status)
            }`}
          >
            <span className="connection-indicator" />
            <span>
              {getConnectionStatusLabel(connectionStatus.status)}
            </span>
          </div>
        )}
      </div>

      {connectionStatus && (
        <div className="connection-summary">
          <span>
            Last reading:{" "}
            <strong>
              {formatTelemetryTimestamp(
                connectionStatus.lastRecordedAtUtc,
              )}
            </strong>
          </span>
          <small>
            Connected ≤{connectionStatus.connectedThresholdMinutes} min ·
            stale ≤{connectionStatus.disconnectedThresholdMinutes} min
          </small>
        </div>
      )}

      <div className="telemetry-controls">
        <label>
          <span>Reading status</span>
          <select
            value={validityFilter}
            onChange={changeValidityFilter}
          >
            <option value="all">All readings</option>
            <option value="valid">Valid only</option>
            <option value="invalid">Invalid only</option>
          </select>
        </label>

        <button type="button" className="secondary-button" onClick={refresh}>
          Refresh
        </button>
      </div>

      {isLoading && (
        <div className="telemetry-state" aria-live="polite">
          Loading telemetry history...
        </div>
      )}

      {!isLoading && error && (
        <div className="telemetry-state telemetry-error" role="alert">
          <strong>Unable to load telemetry history.</strong>
          <span>{error.message}</span>
          {error.traceId && <small>Trace ID: {error.traceId}</small>}
          <button type="button" onClick={refresh}>
            Try again
          </button>
        </div>
      )}

      {!isLoading &&
        !error &&
        history &&
        history.readings.length === 0 && (
          <div className="telemetry-state">
            <strong>No telemetry readings found.</strong>
            <span>
              This sensor has not submitted readings matching the selected
              filter.
            </span>
          </div>
        )}

      {!isLoading &&
        !error &&
        history &&
        history.readings.length > 0 && (
        <>
          <TelemetryChart
            readings={history.readings}
            unit={history.unit}
            expectedMinimum={expectedMinimum}
            expectedMaximum={expectedMaximum}
          />

            <div className="telemetry-table-wrapper">
              <table className="telemetry-table">
                <thead>
                  <tr>
                    <th scope="col">Recorded</th>
                    <th scope="col">Value</th>
                    <th scope="col">Received</th>
                    <th scope="col">Validation</th>
                  </tr>
                </thead>
                <tbody>
                  {history.readings.map((reading) => (
                    <tr key={reading.id}>
                      <td>
                        {formatTelemetryTimestamp(
                          reading.recordedAtUtc,
                        )}
                      </td>
                      <td className="reading-value">
                        {formatTelemetryValue(reading, history.unit)}
                      </td>
                      <td>
                        {formatTelemetryTimestamp(
                          reading.receivedAtUtc,
                        )}
                      </td>
                      <td>
                        <span
                          className={
                            reading.isValid
                              ? "validation-valid"
                              : "validation-invalid"
                          }
                        >
                          {reading.isValid ? "Valid" : "Invalid"}
                        </span>
                        {!reading.isValid &&
                          reading.validationMessage && (
                            <small className="validation-message">
                              {reading.validationMessage}
                            </small>
                          )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <footer className="pagination">
              <span>
                Page {history.page} of {pageCount} ·{" "}
                {history.totalCount} readings
              </span>

              <div>
                <button
                  type="button"
                  className="secondary-button"
                  disabled={page <= 1}
                  onClick={() =>
                    setPage((current) => Math.max(1, current - 1))
                  }
                >
                  Previous
                </button>

                <button
                  type="button"
                  className="secondary-button"
                  disabled={page >= pageCount}
                  onClick={() =>
                    setPage((current) =>
                      Math.min(pageCount, current + 1)
                    )
                  }
                >
                  Next
                </button>
              </div>
            </footer>
          </>
        )}
    </section>
  );
}
