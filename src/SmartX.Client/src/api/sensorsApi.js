import { apiClient } from "./apiClient";

const SENSORS_ENDPOINT = "/api/sensors";

function buildSensorQuery({ category, deploymentNodeId, search } = {}) {
  const parameters = new URLSearchParams();

  if (category) {
    parameters.set("category", category);
  }

  if (deploymentNodeId) {
    parameters.set("deploymentNodeId", deploymentNodeId);
  }

  if (search?.trim()) {
    parameters.set("search", search.trim());
  }

  const query = parameters.toString();
  return query ? `${SENSORS_ENDPOINT}?${query}` : SENSORS_ENDPOINT;
}

export const sensorsApi = {
  list(filters = {}, options = {}) {
    return apiClient.get(buildSensorQuery(filters), options);
  },

  getById(sensorId, options = {}) {
    if (!sensorId) {
      throw new TypeError("A sensor identifier is required.");
    }

    return apiClient.get(
      `${SENSORS_ENDPOINT}/${encodeURIComponent(sensorId)}`,
      options,
    );
  },

  register(sensor, options = {}) {
    if (!sensor || typeof sensor !== "object") {
      throw new TypeError("Sensor registration data is required.");
    }

    return apiClient.post(SENSORS_ENDPOINT, sensor, options);
  },
};
