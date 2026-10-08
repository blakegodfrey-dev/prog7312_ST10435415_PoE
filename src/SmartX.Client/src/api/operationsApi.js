import { apiClient } from './apiClient.js';
export const operationsApi = {
  dashboard: signal => apiClient.get('/api/operations/dashboard', { signal }),
  command: (sensorId, desiredState) => apiClient.post('/api/operations/commands', { id: crypto.randomUUID(), sensorId, desiredState }),
  undo: () => apiClient.post('/api/operations/undo', { id: crypto.randomUUID() }),
  interaction: (targetId, kind, query = '') => apiClient.post('/api/operations/interactions', { id: crypto.randomUUID(), targetId, kind, query }),
  annotate: (id, label, note) => apiClient.put(`/api/operations/incidents/${id}`, { label, note }),
};
