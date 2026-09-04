import { useCallback, useEffect, useState } from "react";
import { attachmentsApi } from "../../api/attachmentsApi";
import { validateAttachment } from "./attachmentValidation";

export function useSensorAttachments(sensorId) {
  const [attachments, setAttachments] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isUploading, setIsUploading] = useState(false);
  const [activeAttachmentId, setActiveAttachmentId] = useState(null);
  const [error, setError] = useState(null);
  const [refreshKey, setRefreshKey] = useState(0);

  const refresh = useCallback(() => {
    setRefreshKey((current) => current + 1);
  }, []);

  useEffect(() => {
    const controller = new AbortController();

    async function loadAttachments() {
      setIsLoading(true);
      setError(null);

      try {
        const result = await attachmentsApi.list(sensorId, {
          signal: controller.signal,
        });

        setAttachments(result);
      } catch (requestError) {
        if (requestError?.name !== "AbortError") {
          setError(requestError);
        }
      } finally {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      }
    }

    loadAttachments();

    return () => controller.abort();
  }, [sensorId, refreshKey]);

  async function upload(file, category) {
    const validationError = validateAttachment(file, category);

    if (validationError) {
      return {
        attachment: null,
        validationError,
      };
    }

    setIsUploading(true);
    setError(null);

    try {
      const attachment = await attachmentsApi.upload(sensorId, {
        file,
        category,
      });

      setAttachments((current) => [attachment, ...current]);

      return {
        attachment,
        validationError: null,
      };
    } catch (requestError) {
      setError(requestError);

      return {
        attachment: null,
        validationError: null,
      };
    } finally {
      setIsUploading(false);
    }
  }

  async function download(attachment) {
    setActiveAttachmentId(attachment.id);
    setError(null);

    try {
      const blob = await attachmentsApi.download(
        sensorId,
        attachment.id,
      );

      const objectUrl = URL.createObjectURL(blob);
      const link = document.createElement("a");

      link.href = objectUrl;
      link.download = attachment.originalFileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(objectUrl);
    } catch (requestError) {
      setError(requestError);
    } finally {
      setActiveAttachmentId(null);
    }
  }

  async function remove(attachmentId) {
    setActiveAttachmentId(attachmentId);
    setError(null);

    try {
      await attachmentsApi.delete(sensorId, attachmentId);

      setAttachments((current) =>
        current.filter(
          (attachment) => attachment.id !== attachmentId,
        ),
      );

      return true;
    } catch (requestError) {
      setError(requestError);
      return false;
    } finally {
      setActiveAttachmentId(null);
    }
  }

  function clearError() {
    setError(null);
  }

  return {
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
  };
}
