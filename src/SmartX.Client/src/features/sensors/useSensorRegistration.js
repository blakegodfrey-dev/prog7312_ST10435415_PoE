import { useState } from "react";
import { sensorsApi } from "../../api/sensorsApi";
import {
  createSensorRegistrationRequest,
  validateSensorForm,
} from "./sensorRegistrationModel";

function normaliseValidationErrors(errors) {
  if (!errors || typeof errors !== "object") {
    return {};
  }

  return Object.fromEntries(
    Object.entries(errors).map(([key, messages]) => {
      const fieldName =
        key.charAt(0).toLowerCase() + key.slice(1);

      return [fieldName, messages];
    }),
  );
}

export function useSensorRegistration() {
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submissionError, setSubmissionError] = useState(null);

  async function register(form) {
    const validationErrors = validateSensorForm(form);

    if (Object.keys(validationErrors).length > 0) {
      return {
        sensor: null,
        validationErrors,
      };
    }

    setIsSubmitting(true);
    setSubmissionError(null);

    try {
      const request = createSensorRegistrationRequest(form);
      const sensor = await sensorsApi.register(request);

      return {
        sensor,
        validationErrors: {},
      };
    } catch (error) {
      setSubmissionError(error);

      return {
        sensor: null,
        validationErrors: normaliseValidationErrors(error.errors),
      };
    } finally {
      setIsSubmitting(false);
    }
  }

  function clearSubmissionError() {
    setSubmissionError(null);
  }

  return {
    register,
    isSubmitting,
    submissionError,
    clearSubmissionError,
  };
}
