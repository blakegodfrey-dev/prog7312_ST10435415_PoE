import { apiClient } from "./apiClient";

const DEPLOYMENT_NODES_ENDPOINT = "/api/deployment-nodes";

export const deploymentNodesApi = {
  list({ nodeType } = {}, options = {}) {
    const parameters = new URLSearchParams();

    if (nodeType) {
      parameters.set("nodeType", nodeType);
    }

    const query = parameters.toString();
    const endpoint = query
      ? `${DEPLOYMENT_NODES_ENDPOINT}?${query}`
      : DEPLOYMENT_NODES_ENDPOINT;

    return apiClient.get(endpoint, options);
  },

  listSensorLocations(options = {}) {
    return this.list({ nodeType: "Node" }, options);
  },
};
