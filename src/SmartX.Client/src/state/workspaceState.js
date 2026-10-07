import { INITIAL_SENSOR_FORM } from "../features/sensors/sensorRegistrationModel.js";

export function createWorkspaceState() {
  return {
    activeView: "startup",
    telemetry: {
      search: "",
      category: "",
      deploymentNodeId: "",
      selectedSensorId: null,
      isRegistering: false,
      successMessage: null,
      isRegistrationSubmitting: false,
      registrationError: null,
      registrationForm: { ...INITIAL_SENSOR_FORM },
    },
    historyBySensor: {},
    commandStream: {
      search: "",
      category: "",
      alertFilter: "all",
      selectedDeviceId: null,
      historySelection: "recent",
    },
  };
}

export function getSensorHistoryState(state, sensorId) {
  return state.historyBySensor[sensorId] ?? {
    validityFilter: "all",
    page: 1,
    selectedReadingId: null,
  };
}

// UI choices belong to the application lifetime. API data is refreshed by
// mounted modules; this store never becomes a second telemetry database.
export function workspaceReducer(state, action) {
  switch (action.type) {
    case "navigate":
      return ["startup", "sensors", "commands"].includes(action.view)
        ? { ...state, activeView: action.view }
        : state;
    case "telemetry/patch":
      return {
        ...state,
        telemetry: { ...state.telemetry, ...action.patch },
      };
    case "registration/patch":
      return {
        ...state,
        telemetry: {
          ...state.telemetry,
          registrationForm: {
            ...state.telemetry.registrationForm,
            ...action.patch,
          },
        },
      };
    case "registration/reset":
      return {
        ...state,
        telemetry: {
          ...state.telemetry,
          registrationForm: { ...INITIAL_SENSOR_FORM },
          registrationError: null,
        },
      };
    case "history/patch":
      if (!action.sensorId) return state;
      return {
        ...state,
        historyBySensor: {
          ...state.historyBySensor,
          [action.sensorId]: {
            ...getSensorHistoryState(state, action.sensorId),
            ...action.patch,
          },
        },
      };
    case "commands/patch":
      return {
        ...state,
        commandStream: { ...state.commandStream, ...action.patch },
      };
    default:
      return state;
  }
}
