export function getTelemetryValue(reading) {
  switch (reading.valueKind) {
    case "Float":
      return reading.floatValue;

    case "Integer":
      return reading.integerValue;

    case "Boolean":
      return reading.booleanValue;

    default:
      return null;
  }
}

export function formatTelemetryValue(reading, unit) {
  const value = getTelemetryValue(reading);

  if (value == null) {
    return "No value";
  }

  if (typeof value === "boolean") {
    return value ? "On" : "Off";
  }

  return unit ? `${value} ${unit}` : String(value);
}

export function formatTelemetryTimestamp(value) {
  if (!value) {
    return "Not available";
  }

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return "Invalid timestamp";
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "medium",
  }).format(date);
}

export function getConnectionStatusLabel(status) {
  switch (status) {
    case "Connected":
      return "Connected";

    case "Stale":
      return "Stale";

    case "Disconnected":
      return "Disconnected";

    case "NoData":
      return "No data";

    default:
      return "Unknown";
  }
}
