import { apiClient } from "./apiClient.js";

export const meshApi = {
  snapshot({ nodeId, macAddress } = {}, options = {}) {
    const parameters = new URLSearchParams();
    if (macAddress) parameters.set("macAddress", macAddress);
    else if (nodeId) parameters.set("nodeId", nodeId);
    const query = parameters.toString();
    return apiClient.get(`/api/mesh${query ? `?${query}` : ""}`, options);
  },
  setNodeAvailable(id, isAvailable) {
    return apiClient.put(`/api/mesh/nodes/${encodeURIComponent(id)}/availability`, { isAvailable });
  },
  setLinkAvailable(id, isAvailable) {
    return apiClient.put(`/api/mesh/links/${encodeURIComponent(id)}/availability`, { isAvailable });
  },
  reset() { return apiClient.delete("/api/mesh/simulation"); },
};
