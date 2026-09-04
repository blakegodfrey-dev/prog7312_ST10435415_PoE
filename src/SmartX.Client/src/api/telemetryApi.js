import { apiClient } from "./apiClient";

const TELEMETRY_ENDPOINT = "/api/telemetry";

function requireSensorId(sensorId) {
  if (!sensorId) {
    throw new TypeError("A sensor identifier is required.");
  }

  return encodeURIComponent(sensorId);
}

function toUtcQueryValue(value) {
  if (!value) {
    return null;
  }

  const date = value instanceof Date ? value : new Date(value);

  if (Number.isNaN(date.getTime())) {
    throw new TypeError("A valid telemetry timestamp is required.");
  }

  return date.toISOString();
}

function buildHistoryQuery({
  fromUtc,
  toUtc,
  isValid,
  page = 1,
  pageSize = 100,
} = {}) {
  const parameters = new URLSearchParams();

  const from = toUtcQueryValue(fromUtc);
  const to = toUtcQueryValue(toUtc);

  if (from) {
    parameters.set("fromUtc", from);
  }

  if (to) {
    parameters.set("toUtc", to);
  }

  if (typeof isValid === "boolean") {
    parameters.set("isValid", String(isValid));
  }

  parameters.set("page", String(page));
  parameters.set("pageSize", String(pageSize));

  return parameters.toString();
}

export const telemetryApi = {
  getHealthSummary(options = {}) {
    return apiClient.get(
      `${TELEMETRY_ENDPOINT}/diagnostics/health-summary`,
      options,
    );
  },
  getHistory(sensorId, filters = {}, options = {}) {
    const id = requireSensorId(sensorId);
    const query = buildHistoryQuery(filters);

    return apiClient.get(
      `${TELEMETRY_ENDPOINT}/sensors/${id}?${query}`,
      options,
    );
  },

  getConnectionStatus(sensorId, options = {}) {
    const id = requireSensorId(sensorId);

    return apiClient.get(
      `/api/sensors/${id}/connection-status`,
      options,
    );
  },
};
