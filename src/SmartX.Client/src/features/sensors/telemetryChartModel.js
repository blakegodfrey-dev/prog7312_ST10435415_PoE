import { getTelemetryValue } from "./telemetryDisplay.js";

export function prepareTelemetryChart(readings) {
  const ordered = readings.map(reading => {
    const value = getTelemetryValue(reading);
    return { ...reading, time: Date.parse(reading.recordedAtUtc),
      chartValue: typeof value === "boolean" ? Number(value) : value };
  }).filter(r => Number.isFinite(r.time))
    .sort((a, b) => a.time - b.time || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
  const intervals = ordered.slice(1).map((r, i) => r.time - ordered[i].time).filter(v => v > 0).sort((a, b) => a - b);
  const middle = Math.floor(intervals.length / 2);
  const median = intervals.length % 2 ? intervals[middle] : (intervals[middle - 1] + intervals[middle]) / 2;
  // Long intervals are inferred from this page's cadence, not asserted packet
  // loss. Do not join across explicit missing values or an unusually long gap.
  const gapThreshold = intervals.length >= 2 ? median * 3 : Infinity;
  const points = [], segments = [], gaps = [];
  let segment = [];
  let previous = null;
  function finish() { if (segment.length) segments.push(segment); segment = []; }
  for (const point of ordered) {
    if (point.chartValue == null || !Number.isFinite(point.chartValue)) { finish(); previous = null; continue; }
    if (previous && point.time - previous.time > gapThreshold) {
      gaps.push({ from: previous.time, to: point.time });
      finish();
    }
    points.push(point); segment.push(point); previous = point;
  }
  finish();
  return { points, segments, gaps };
}

export function telemetryTimeX(time, firstTime, lastTime, left, width) {
  return lastTime === firstTime ? left + width / 2 : left + ((time - firstTime) / (lastTime - firstTime)) * width;
}
