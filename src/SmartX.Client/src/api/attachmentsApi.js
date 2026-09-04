import { apiClient } from "./apiClient";

function buildAttachmentsEndpoint(sensorId) {
  if (!sensorId) {
    throw new TypeError("A sensor identifier is required.");
  }

  return `/api/sensors/${
    encodeURIComponent(sensorId)
  }/attachments`;
}

function requireAttachmentId(attachmentId) {
  if (!attachmentId) {
    throw new TypeError("An attachment identifier is required.");
  }

  return encodeURIComponent(attachmentId);
}

export const attachmentsApi = {
  list(sensorId, options = {}) {
    return apiClient.get(
      buildAttachmentsEndpoint(sensorId),
      options,
    );
  },

  upload(sensorId, { file, category }, options = {}) {
    if (!(file instanceof File)) {
      throw new TypeError("An attachment file is required.");
    }

    if (!category) {
      throw new TypeError("An attachment category is required.");
    }

    const formData = new FormData();
    formData.append("File", file, file.name);
    formData.append("Category", category);

    return apiClient.post(
      buildAttachmentsEndpoint(sensorId),
      formData,
      options,
    );
  },

  download(sensorId, attachmentId, options = {}) {
    const endpoint = `${
      buildAttachmentsEndpoint(sensorId)
    }/${requireAttachmentId(attachmentId)}/content`;

    return apiClient.get(endpoint, {
      ...options,
      responseType: "blob",
    });
  },

  delete(sensorId, attachmentId, options = {}) {
    const endpoint = `${
      buildAttachmentsEndpoint(sensorId)
    }/${requireAttachmentId(attachmentId)}`;

    return apiClient.delete(endpoint, options);
  },
};
