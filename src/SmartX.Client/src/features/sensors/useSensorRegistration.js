import { useWorkspaceState } from "../../state/useWorkspaceState.js";
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
  // A submitted write can complete while another module is open. Keep its
  // status above module lifetimes so returning cannot submit it a second time.
  const { state, dispatch } = useWorkspaceState();
  const isSubmitting = state.telemetry.isRegistrationSubmitting;
  const submissionError = state.telemetry.registrationError;
  const patch = (values) => dispatch({ type: "telemetry/patch", patch: values });
  const setIsSubmitting = (isRegistrationSubmitting) => patch({ isRegistrationSubmitting });
  const setSubmissionError = (registrationError) => patch({ registrationError });

  async function register(form) {
    if (isSubmitting) return { sensor: null, validationErrors: {} };

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
