import { useState } from "react";
import {
  ATTACHMENT_CATEGORIES,
  formatFileSize,
  getAttachmentCategory,
  getAttachmentCategoryLabel,
} from "../../api/attachmentOptions";
import { formatTelemetryTimestamp } from "./telemetryDisplay";
import { useSensorAttachments } from "./useSensorAttachments";

export function SensorAttachmentsPanel({ sensorId }) {
  const [category, setCategory] = useState("ConfigurationFile");
  const [file, setFile] = useState(null);
  const [fileInputKey, setFileInputKey] = useState(0);
  const [validationError, setValidationError] = useState(null);
  const [successMessage, setSuccessMessage] = useState(null);

  const {
    attachments,
    isLoading,
    isUploading,
    activeAttachmentId,
    error,
    upload,
    download,
    remove,
    refresh,
    clearError,
  } = useSensorAttachments(sensorId);

  const selectedCategory = getAttachmentCategory(category);

  function selectCategory(value) {
    setCategory(value);
    setFile(null);
    setFileInputKey((current) => current + 1);
    setValidationError(null);
    setSuccessMessage(null);
    clearError();
  }

  function selectFile(event) {
    setFile(event.target.files?.[0] ?? null);
    setValidationError(null);
    setSuccessMessage(null);
    clearError();
  }

  async function handleUpload(event) {
    event.preventDefault();

    const result = await upload(file, category);
    setValidationError(result.validationError);

    if (result.attachment) {
      setSuccessMessage(
        `${result.attachment.originalFileName} was uploaded successfully.`,
      );
      setFile(null);
      setFileInputKey((current) => current + 1);
    }
  }

  async function handleDelete(attachment) {
    const confirmed = window.confirm(
      `Delete "${attachment.originalFileName}"? This cannot be undone.`,
    );

    if (confirmed) {
      await remove(attachment.id);
    }
  }

  return (
    <section
      className="attachments-panel"
      aria-labelledby="attachments-title"
    >
      <div className="attachments-heading">
        <div>
          <p className="eyebrow">DEVICE FILES</p>
          <h2 id="attachments-title">Sensor attachments</h2>
          <p>
            Store configuration, deployment evidence and diagnostic logs
            against this sensor.
          </p>
        </div>

        <span className="file-limit">Maximum 5 MB</span>
      </div>

      <div
        className="attachment-category-grid"
        aria-label="Attachment category"
      >
        {ATTACHMENT_CATEGORIES.map((option) => (
          <button
            type="button"
            key={option.value}
            className={
              category === option.value
                ? "attachment-category selected"
                : "attachment-category"
            }
            aria-pressed={category === option.value}
            onClick={() => selectCategory(option.value)}
          >
            <strong>{option.label}</strong>
            <span>{option.description}</span>
            <small>{option.allowedExtensions.join(", ")}</small>
          </button>
        ))}
      </div>

      <form className="upload-form" onSubmit={handleUpload}>
        <label>
          <span>{selectedCategory.label}</span>
          <input
            key={fileInputKey}
            type="file"
            accept={selectedCategory.accept}
            onChange={selectFile}
            aria-describedby="attachment-help attachment-error"
          />
          <small id="attachment-help">
            Accepted: {selectedCategory.allowedExtensions.join(", ")}.
          </small>
        </label>

        {file && (
          <div className="selected-file" aria-live="polite">
            <span>{file.name}</span>
            <small>{formatFileSize(file.size)}</small>
          </div>
        )}

        {validationError && (
          <p id="attachment-error" className="field-error" role="alert">
            {validationError}
          </p>
        )}

        {successMessage && (
          <p className="upload-success" role="status">
            {successMessage}
          </p>
        )}

        {error && (
          <div className="form-error-summary" role="alert">
            <strong>Attachment action failed.</strong>
            <span>{error.message}</span>
            {error.traceId && <small>Trace ID: {error.traceId}</small>}
          </div>
        )}

        <button
          type="submit"
          className="primary-button upload-button"
          disabled={isUploading}
        >
          {isUploading ? "Uploading..." : `Upload ${selectedCategory.label}`}
        </button>
      </form>

      <div className="attachment-list-heading">
        <h3>Uploaded files</h3>
        <button type="button" className="text-button" onClick={refresh}>
          Refresh list
        </button>
      </div>

      {isLoading && (
        <div className="attachment-state" aria-live="polite">
          Loading attachments...
        </div>
      )}

      {!isLoading && attachments.length === 0 && (
        <div className="attachment-state">
          No files have been attached to this sensor.
        </div>
      )}

      {!isLoading && attachments.length > 0 && (
        <ul className="attachment-list">
          {attachments.map((attachment) => {
            const isActive = activeAttachmentId === attachment.id;

            return (
              <li key={attachment.id}>
                <div className="attachment-file-info">
                  <span className="attachment-type">
                    {getAttachmentCategoryLabel(attachment.category)}
                  </span>
                  <strong>{attachment.originalFileName}</strong>
                  <small>
                    {formatFileSize(attachment.sizeBytes)} · Uploaded{" "}
                    {formatTelemetryTimestamp(
                      attachment.uploadedAtUtc,
                    )}
                  </small>
                </div>

                <div className="attachment-actions">
                  <button
                    type="button"
                    className="secondary-button"
                    disabled={isActive}
                    onClick={() => download(attachment)}
                  >
                    {isActive ? "Working..." : "Download"}
                  </button>

                  <button
                    type="button"
                    className="danger-button"
                    disabled={isActive}
                    onClick={() => handleDelete(attachment)}
                  >
                    Delete
                  </button>
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
