export const MAXIMUM_ATTACHMENT_SIZE_BYTES = 5 * 1024 * 1024;

export const ATTACHMENT_CATEGORIES = Object.freeze([
  {
    value: "ConfigurationFile",
    label: "Configuration file",
    description: "Device configuration, calibration or setup information.",
    accept: ".json,.txt,.csv,.pdf",
    allowedExtensions: [".json", ".txt", ".csv", ".pdf"],
  },
  {
    value: "DeploymentImage",
    label: "Deployment photo",
    description: "A photograph showing the sensor and its installed location.",
    accept: ".png,.jpg,.jpeg",
    allowedExtensions: [".png", ".jpg", ".jpeg"],
  },
  {
    value: "HardwareLog",
    label: "Hardware log",
    description: "Diagnostic output recorded by the sensor or microcontroller.",
    accept: ".log,.txt,.csv",
    allowedExtensions: [".log", ".txt", ".csv"],
  },
]);

export function getAttachmentCategory(value) {
  return ATTACHMENT_CATEGORIES.find(
    (category) => category.value === value,
  );
}

export function getAttachmentCategoryLabel(value) {
  return getAttachmentCategory(value)?.label ?? value;
}

export function formatFileSize(sizeBytes) {
  if (!Number.isFinite(sizeBytes) || sizeBytes < 0) {
    return "Unknown size";
  }

  if (sizeBytes < 1024) {
    return `${sizeBytes} B`;
  }

  if (sizeBytes < 1024 * 1024) {
    return `${(sizeBytes / 1024).toFixed(1)} KB`;
  }

  return `${(sizeBytes / (1024 * 1024)).toFixed(1)} MB`;
}
