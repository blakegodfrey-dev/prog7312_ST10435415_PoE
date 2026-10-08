import { useSensorHealthSummary } from "./useSensorHealthSummary";

function formatEvaluationTime(value) {
  if (!value) {
    return "Unknown";
  }

  return new Date(value).toLocaleString(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  });
}

export function SensorHealthOverview() {
  const {
    summary,
    isLoading,
    error,
    refresh,
  } = useSensorHealthSummary();

  const healthCards = summary
    ? [
        {
          key: "connected",
          label: "Connected",
          code: "Live",
          count: summary.connectedSensorCount,
          description: `Gateway contact less than ${summary.staleSeconds} seconds ago.`,
        },
        {
          key: "stale",
          label: "Stale",
          code: "Aging",
          count: summary.staleSensorCount,
          description: `Gateway silent for ${summary.staleSeconds}–${summary.disconnectedSeconds} seconds.`,
        },
        {
          key: "disconnected",
          label: "Disconnected",
          code: "Offline",
          count: summary.disconnectedSensorCount,
          description: `No gateway contact for at least ${summary.disconnectedSeconds} seconds.`,
        },
        {
          key: "invalid",
          label: "Invalid",
          code: "Check",
          count: summary.invalidLatestReadingCount,
          description: "Latest reading failed telemetry validation.",
        },
      ]
    : [];

  return (
    <section
      className="health-overview"
      aria-labelledby="health-overview-title"
      aria-busy={isLoading}
    >
      <div className="health-overview-heading">
        <div>
          <p className="eyebrow">LIVE FACILITY HEALTH</p>
          <h2 id="health-overview-title">Attention overview</h2>
          <p>
            Start with sensors that need investigation instead of searching
            through every reading.
          </p>
        </div>

        <button
          type="button"
          className="secondary-button"
          onClick={refresh}
          disabled={isLoading}
        >
          {isLoading ? "Refreshing..." : "Refresh health"}
        </button>
      </div>

      {isLoading && (
        <div className="health-state" aria-live="polite">
          <strong>Checking sensor health</strong>
          <span>Checking gateway contact against the shared connection thresholds.</span>
        </div>
      )}

      {!isLoading && error && (
        <div className="health-state health-state-error" role="alert">
          <strong>Health overview unavailable</strong>
          <span>{error.message}</span>
          {error.traceId && <small>Trace ID: {error.traceId}</small>}
          <button type="button" className="text-button" onClick={refresh}>
            Try again
          </button>
        </div>
      )}

      {!isLoading && !error && summary?.totalSensorCount === 0 && (
        <div className="health-state">
          <strong>No sensors to evaluate</strong>
          <span>Register a sensor to begin monitoring facility health.</span>
        </div>
      )}

      {!isLoading && !error && summary?.totalSensorCount > 0 && (
        <>
          <div className="health-card-grid" aria-live="polite">
            {healthCards.map((card) => (
              <article
                className={`health-card health-card-${card.key}`}
                key={card.key}
              >
                <div className="health-card-label">
                  <span
                    className="health-card-indicator"
                    aria-hidden="true"
                  />
                  <span>{card.code}</span>
                </div>
                <strong className="health-count">{card.count}</strong>
                <h3>{card.label}</h3>
                <p>{card.description}</p>
              </article>
            ))}
          </div>

          <div className="health-overview-meta">
            <p>
              <strong>{summary.totalSensorCount}</strong> registered sensors
              evaluated
            </p>
            <p>
              <strong>{summary.noDataSensorCount}</strong> awaiting their first
              reading
            </p>
            <p>
              <strong>{summary.unknownSensorCount}</strong> with unknown gateway contact
            </p>
            <p>
              Updated {formatEvaluationTime(summary.evaluatedAtUtc)}
            </p>
          </div>

          <p className="health-overlap-note">
            Invalid readings are counted separately and may belong to a
            connected, stale or disconnected sensor.
          </p>
        </>
      )}
    </section>
  );
}