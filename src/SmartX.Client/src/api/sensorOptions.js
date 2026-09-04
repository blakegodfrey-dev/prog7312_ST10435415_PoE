export const SENSOR_CATEGORIES = Object.freeze([
  {
    value: "Environmental",
    label: "Environmental",
    description: "Monitors growing conditions such as temperature, humidity and pH.",
  },
  {
    value: "PowerConsumption",
    label: "Power consumption",
    description: "Measures electricity usage across equipment and deployment areas.",
  },
  {
    value: "Actuator",
    label: "Actuator",
    description: "Reports the operating state of pumps, valves and other controlled devices.",
  },
]);

export const TELEMETRY_VALUE_KINDS = Object.freeze([
  {
    value: "Float",
    label: "Decimal number",
    example: "For readings such as 24.6 °C or 6.4 pH.",
  },
  {
    value: "Integer",
    label: "Whole number",
    example: "For readings such as 1,250 RPM.",
  },
  {
    value: "Boolean",
    label: "On or off",
    example: "For states such as pump running or valve closed.",
  },
]);

export const DEPLOYMENT_NODE_TYPES = Object.freeze([
  "Facility",
  "Zone",
  "SubZone",
  "Node",
]);

export function getSensorCategoryLabel(value) {
  return (
    SENSOR_CATEGORIES.find((category) => category.value === value)?.label ??
    value
  );
}

export function getTelemetryValueKindLabel(value) {
  return (
    TELEMETRY_VALUE_KINDS.find((kind) => kind.value === value)?.label ??
    value
  );
}
