import { useWorkspaceState } from "../../state/useWorkspaceState.js";
import { getSensorHistoryState } from "../../state/workspaceState.js";
import {
  formatTelemetryTimestamp,
  formatTelemetryValue,
  getConnectionStatusLabel,
} from "./telemetryDisplay";
import { useSensorTelemetry } from "./useSensorTelemetry";
import { TelemetryChart } from "./TelemetryChart";
import { buildHistoryRange, HISTORY_TIME_ZONE } from "./historyRange.js";

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
  const { state, dispatch } = useWorkspaceState();
  const { validityFilter, page, selectedReadingId, fromLocal, toLocal } = getSensorHistoryState(state, sensorId);
  const range = buildHistoryRange(fromLocal, toLocal);
  const patchHistory = (patch) => dispatch({ type: "history/patch", sensorId, patch });

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
    fromUtc: range.fromUtc,
    toUtc: range.toUtc,
    enabled: !range.error,
  });

  const pageCount = history
    ? Math.max(1, Math.ceil(history.totalCount / history.pageSize))
    : 1;

  function changeValidityFilter(event) {
    patchHistory({ validityFilter: event.target.value, page: 1, selectedReadingId: null });
  }

  return (
    <section
      className="telemetry-history"
      aria-labelledby="telemetry-history-title"
    >
      <div className="telemetry-heading">
        <div>
          <p className="eyebrow">HISTORICAL READINGS</p>
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
                HISTORY_TIME_ZONE,
              )}
            </strong>
          </span>
          <span>
            Gateway last seen:{" "}
            <strong>{formatTelemetryTimestamp(connectionStatus.lastSeenAtUtc, HISTORY_TIME_ZONE)}</strong>
          </span>
          <small>
            Connected &lt;{connectionStatus.staleSeconds} s ·
            stale &lt;{connectionStatus.disconnectedSeconds} s ·
            disconnected ≥{connectionStatus.disconnectedSeconds} s
          </small>
        </div>
      )}

      <div className="telemetry-controls">
        <label>
          <span>From (SAST)</span>
          <input type="datetime-local" step="1" value={fromLocal}
            aria-describedby="history-timezone"
            onChange={(event) => patchHistory({ fromLocal: event.target.value, page: 1, selectedReadingId: null })} />
        </label>
        <label>
          <span>To (SAST)</span>
          <input type="datetime-local" step="1" value={toLocal}
            aria-describedby="history-timezone"
            onChange={(event) => patchHistory({ toLocal: event.target.value, page: 1, selectedReadingId: null })} />
        </label>
        <label>
          <span>Reading status</span>
          <select
            aria-label="Reading status"
            value={validityFilter}
            onChange={changeValidityFilter}
          >
            <option value="all">All readings</option>
            <option value="valid">Valid only</option>
            <option value="invalid">Invalid only</option>
          </select>
        </label>

        <button type="button" className="secondary-button"
          onClick={() => patchHistory({ fromLocal: "", toLocal: "", page: 1, selectedReadingId: null })}>
          Clear dates
        </button>
        <button type="button" className="secondary-button" onClick={refresh} disabled={Boolean(range.error)}>
          Refresh
        </button>
      </div>
      <p id="history-timezone" className="history-range-help">
        All history times use SAST (UTC+02:00). Both endpoints are included; leave either date blank for an open range.
      </p>
      {range.error && <p className="telemetry-state telemetry-error" role="alert">{range.error}</p>}

      {!range.error && isLoading && (
        <div className="telemetry-state" aria-live="polite">
          Loading telemetry history...
        </div>
      )}

      {!range.error && !isLoading && error && (
        <div className="telemetry-state telemetry-error" role="alert">
          <strong>Unable to load telemetry history.</strong>
          <span>{error.message}</span>
          {error.traceId && <small>Trace ID: {error.traceId}</small>}
          <button type="button" onClick={refresh}>
            Try again
          </button>
        </div>
      )}

      {!range.error && !isLoading &&
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

      {!range.error && !isLoading &&
        !error &&
        history &&
        history.readings.length > 0 && (
        <>
          <TelemetryChart
            readings={history.readings}
            unit={history.unit}
            expectedMinimum={expectedMinimum}
            expectedMaximum={expectedMaximum}
            selectedReadingId={selectedReadingId}
            onSelectReading={(selectedReadingId) => patchHistory({ selectedReadingId })}
            timeZone={HISTORY_TIME_ZONE}
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
                          HISTORY_TIME_ZONE,
                        )}
                      </td>
                      <td className="reading-value">
                        {formatTelemetryValue(reading, history.unit)}
                      </td>
                      <td>
                        {formatTelemetryTimestamp(
                          reading.receivedAtUtc,
                          HISTORY_TIME_ZONE,
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
                    patchHistory({ page: Math.max(1, page - 1), selectedReadingId: null })
                  }
                >
                  Previous
                </button>

                <button
                  type="button"
                  className="secondary-button"
                  disabled={page >= pageCount}
                  onClick={() =>
                    patchHistory({ page: Math.min(pageCount, page + 1), selectedReadingId: null })
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
