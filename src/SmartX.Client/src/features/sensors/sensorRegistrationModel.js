const MAC_ADDRESS_PATTERN =
  /^([0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}$/;

export const INITIAL_SENSOR_FORM = Object.freeze({
  macAddress: "",
  friendlyName: "",
  category: "Environmental",
  measuredProperty: "",
  valueKind: "Float",
  unit: "",
  deploymentNodeId: "",
  expectedMinimum: "",
  expectedMaximum: "",
});

function parseOptionalNumber(value) {
  if (value === "" || value == null) {
    return null;
  }

  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

export function validateSensorForm(form) {
  const errors = {};

  if (!MAC_ADDRESS_PATTERN.test(form.macAddress.trim())) {
    errors.macAddress =
      "Enter a MAC address in the format A4:CF:12:8B:40:01.";
  }

  if (!form.friendlyName.trim()) {
    errors.friendlyName = "Enter a friendly sensor name.";
  } else if (form.friendlyName.trim().length > 150) {
    errors.friendlyName =
      "The friendly name cannot exceed 150 characters.";
  }

  if (!form.measuredProperty.trim()) {
    errors.measuredProperty =
      "Enter the property measured by this sensor.";
  } else if (form.measuredProperty.trim().length > 100) {
    errors.measuredProperty =
      "The measured property cannot exceed 100 characters.";
  }

  if (form.unit.trim().length > 30) {
    errors.unit = "The unit cannot exceed 30 characters.";
  }

  if (!form.deploymentNodeId) {
    errors.deploymentNodeId = "Select a deployment location.";
  }

  const minimum = parseOptionalNumber(form.expectedMinimum);
  const maximum = parseOptionalNumber(form.expectedMaximum);

  if (form.expectedMinimum !== "" && minimum == null) {
    errors.expectedMinimum = "Enter a valid minimum number.";
  }

  if (form.expectedMaximum !== "" && maximum == null) {
    errors.expectedMaximum = "Enter a valid maximum number.";
  }

  if (
    minimum != null &&
    maximum != null &&
    minimum > maximum
  ) {
    errors.expectedMaximum =
      "The maximum must be greater than or equal to the minimum.";
  }

  return errors;
}

export function createSensorRegistrationRequest(form) {
  const isBoolean = form.valueKind === "Boolean";

  return {
    id: crypto.randomUUID(),
    macAddress: form.macAddress.trim().toUpperCase(),
    friendlyName: form.friendlyName.trim(),
    category: form.category,
    measuredProperty: form.measuredProperty.trim(),
    valueKind: form.valueKind,
    unit: form.unit.trim(),
    deploymentNodeId: form.deploymentNodeId,
    expectedMinimum: isBoolean
      ? null
      : parseOptionalNumber(form.expectedMinimum),
    expectedMaximum: isBoolean
      ? null
      : parseOptionalNumber(form.expectedMaximum),
  };
}
