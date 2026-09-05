import { useState } from "react";
import {
  formatTelemetryTimestamp,
  formatTelemetryValue,
  getTelemetryValue,
} from "./telemetryDisplay";


const WIDTH = 760;
const HEIGHT = 260;
const PADDING = {
  top: 24,
  right: 24,
  bottom: 48,
  left: 64,
};

function formatAxisValue(value, valueKind) {
  if (valueKind === "Boolean") {
    return value >= 0.5 ? "On" : "Off";
  }

  return Number.isInteger(value)
    ? String(value)
    : value.toFixed(1);
}

function formatShortTime(value) {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return "Unknown";
  }

  return new Intl.DateTimeFormat(undefined, {
    hour: "2-digit",
    minute: "2-digit",
  }).format(date);
}

export function TelemetryChart({
    readings,
    unit,
    expectedMinimum,
    expectedMaximum,
  }) { const [selectedReadingId, setSelectedReadingId] = useState(null);
  const points = readings
    .map((reading) => ({
      ...reading,
      chartValue: getTelemetryValue(reading),
    }))
    .filter((reading) => reading.chartValue != null)
    .map((reading) => ({
      ...reading,
      chartValue:
        typeof reading.chartValue === "boolean"
          ? Number(reading.chartValue)
          : reading.chartValue,
    }))
    .reverse();

  if (points.length === 0) {
    return null;
  }

  const valueKind = points[0].valueKind;

const hasExpectedRange =
  valueKind !== "Boolean" &&
  Number.isFinite(Number(expectedMinimum)) &&
  Number.isFinite(Number(expectedMaximum));

const numericExpectedMinimum = hasExpectedRange
  ? Number(expectedMinimum)
  : null;
const numericExpectedMaximum = hasExpectedRange
  ? Number(expectedMaximum)
  : null;

const values = points.map((point) => point.chartValue);
const scaleValues = hasExpectedRange
  ? [
      ...values,
      numericExpectedMinimum,
      numericExpectedMaximum,
    ]
  : values;

const minimumValue =
  valueKind === "Boolean" ? 0 : Math.min(...scaleValues);
const maximumValue =
  valueKind === "Boolean" ? 1 : Math.max(...scaleValues);

  const valuePadding =
    maximumValue === minimumValue
      ? Math.max(Math.abs(maximumValue) * 0.1, 1)
      : (maximumValue - minimumValue) * 0.1;

  const yMinimum =
    valueKind === "Boolean"
      ? 0
      : minimumValue - valuePadding;
  const yMaximum =
    valueKind === "Boolean"
      ? 1
      : maximumValue + valuePadding;

  const plotWidth = WIDTH - PADDING.left - PADDING.right;
  const plotHeight = HEIGHT - PADDING.top - PADDING.bottom;

  function getX(index) {
    if (points.length === 1) {
      return PADDING.left + plotWidth / 2;
    }

    return PADDING.left + (index / (points.length - 1)) * plotWidth;
  }

  function getY(value) {
    const range = yMaximum - yMinimum || 1;
    const ratio = (value - yMinimum) / range;

    return PADDING.top + plotHeight - ratio * plotHeight;
  }

  const linePoints = points
    .map((point, index) => `${getX(index)},${getY(point.chartValue)}`)
    .join(" ");

  const middleValue = (yMinimum + yMaximum) / 2;
  const firstPoint = points[0];
  const lastPoint = points[points.length - 1];
  const unitLabel = unit ? ` (${unit})` : "";

  const selectedPoint = points.find(
  (point) => point.id === selectedReadingId,
);

function selectInvalidPoint(point) {
  if (!point.isValid) {
    return;
  }

  setSelectedReadingId(point.id);
}

function handleInvalidPointKeyDown(event, point) {
  if (event.key === "Enter" || event.key === " ") {
    event.preventDefault();
    selectInvalidPoint(point);
  }
}

  return (
    <figure className="telemetry-chart">
      <figcaption>
        <strong>Recent telemetry trend</strong>
        <span>
          {points.length} plotted{" "}
          {points.length === 1 ? "reading" : "readings"}
          {unitLabel}
        </span>
      </figcaption>

      <svg
        viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
        role="img"
        aria-label={`Recent telemetry trend containing ${points.length} readings.`}
      > {hasExpectedRange && (
  <>
    <rect
      className="chart-range-band"
      x={PADDING.left}
      y={getY(numericExpectedMaximum)}
      width={plotWidth}
      height={
        getY(numericExpectedMinimum) -
        getY(numericExpectedMaximum)
      }
    />

    <line
      className="chart-range-boundary"
      x1={PADDING.left}
      x2={WIDTH - PADDING.right}
      y1={getY(numericExpectedMaximum)}
      y2={getY(numericExpectedMaximum)}
    />

    <line
      className="chart-range-boundary"
      x1={PADDING.left}
      x2={WIDTH - PADDING.right}
      y1={getY(numericExpectedMinimum)}
      y2={getY(numericExpectedMinimum)}
    />

    <text
      className="chart-range-label"
      x={WIDTH - PADDING.right - 6}
      y={getY(numericExpectedMaximum) + 15}
      textAnchor="end"
    >
      Expected {numericExpectedMinimum}-{numericExpectedMaximum}
      {unit ? ` ${unit}` : ""}
    </text>
  </>
)}
        {[yMaximum, middleValue, yMinimum].map((value) => {
          const y = getY(value);

          return (
            <g key={value}>
              <line
                className="chart-grid-line"
                x1={PADDING.left}
                x2={WIDTH - PADDING.right}
                y1={y}
                y2={y}
              />
              <text
                className="chart-axis-label"
                x={PADDING.left - 10}
                y={y + 4}
                textAnchor="end"
              >
                {formatAxisValue(value, valueKind)}
              </text>
            </g>
          );
        })}

        <line
          className="chart-axis"
          x1={PADDING.left}
          x2={PADDING.left}
          y1={PADDING.top}
          y2={HEIGHT - PADDING.bottom}
        />

        <line
          className="chart-axis"
          x1={PADDING.left}
          x2={WIDTH - PADDING.right}
          y1={HEIGHT - PADDING.bottom}
          y2={HEIGHT - PADDING.bottom}
        />

        {points.length > 1 && (
          <polyline
            className="chart-line"
            points={linePoints}
          />
        )}

        {points.map((point, index) => {
          const x = getX(index);
          const y = getY(point.chartValue);
          const tooltip = `${formatTelemetryTimestamp(
            point.recordedAtUtc,
          )}: ${formatAxisValue(point.chartValue, valueKind)}${
            unit ? ` ${unit}` : ""
          }`;

          if (point.isValid) {
            return (
              <circle
                className="chart-point"
                key={point.id}
                cx={x}
                cy={y}
                r="4"
              >
                <title>{tooltip}</title>
              </circle>
            );
          }

          return (
            <g
              className={
                selectedReadingId === point.id
                  ? "chart-anomaly selected"
                  : "chart-anomaly"
              }
              key={point.id}
              role="button"
              tabIndex="0"
              aria-label={`Invalid reading. ${tooltip}. Select for details.`}
              onClick={() => selectInvalidPoint(point)}
              onKeyDown={(event) =>
                handleInvalidPointKeyDown(event, point)
              }
            >
              <polygon
                points={
                  `${x},${y - 9} ` +
                  `${x + 9},${y} ` +
                  `${x},${y + 9} ` +
                  `${x - 9},${y}`
                }
              />
              <text x={x} y={y + 4} textAnchor="middle">
                !
              </text>
              <title>{`${tooltip}. Invalid reading.`}</title>
            </g>
          );
        })}

        <text
          className="chart-axis-label"
          x={PADDING.left}
          y={HEIGHT - 18}
          textAnchor="start"
        >
          {formatShortTime(firstPoint.recordedAtUtc)}
        </text>

        <text
          className="chart-axis-label"
          x={WIDTH - PADDING.right}
          y={HEIGHT - 18}
          textAnchor="end"
        >
          {formatShortTime(lastPoint.recordedAtUtc)}
        </text>
      </svg>
      {selectedPoint && (
  <aside
    className="anomaly-details"
    aria-live="polite"
    aria-label="Selected anomaly details"
  >
    <div className="anomaly-details-heading">
      <div>
        <p className="eyebrow">SELECTED ANOMALY</p>
        <strong>Investigation details</strong>
      </div>

      <button
        type="button"
        className="text-button"
        onClick={() => setSelectedReadingId(null)}
      >
        Close
      </button>
    </div>

    <dl>
      <div>
        <dt>Recorded</dt>
        <dd>
          {formatTelemetryTimestamp(selectedPoint.recordedAtUtc)}
        </dd>
      </div>

      <div>
        <dt>Reading</dt>
        <dd>{formatTelemetryValue(selectedPoint, unit)}</dd>
      </div>

      <div>
        <dt>Validation</dt>
        <dd>Invalid</dd>
      </div>

      <div>
        <dt>Reason</dt>
        <dd>
          {selectedPoint.validationMessage ||
            "The reading failed telemetry validation."}
        </dd>
      </div>
    </dl>
  </aside>
)}
    </figure>
  );
}