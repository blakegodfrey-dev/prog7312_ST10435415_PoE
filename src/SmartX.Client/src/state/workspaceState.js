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
    network: {
      search: "", nodeFilter: "all", selectedNodeId: null, selectedLinkId: null,
      routeLookupMac: null, linksView: "all", showCosts: false,
      mutationPending: false, mutationFeedback: null, lookupError: null,
      refreshVersion: 0,
    },
    commandStream: {
      search: "",
      category: "",
      alertFilter: "all",
      selectedDeviceId: null,
      historySelection: "recent",
      commandPending: false,
      commandFeedback: null,
    },
  };
}

export function getSensorHistoryState(state, sensorId) {
  return state.historyBySensor[sensorId] ?? {
    validityFilter: "all",
    page: 1,
    selectedReadingId: null,
    fromLocal: "",
    toLocal: "",
  };
}

// UI choices belong to the application lifetime. API data is refreshed by
// mounted modules; this store never becomes a second telemetry database.
export function workspaceReducer(state, action) {
  switch (action.type) {
    case "navigate":
      return ["startup", "sensors", "commands", "network"].includes(action.view)
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
    case "network/patch":
      return { ...state, network: { ...state.network, ...action.patch } };
    case "network/select":
      return { ...state, network: { ...state.network, selectedNodeId: action.nodeId ?? null,
        selectedLinkId: null, routeLookupMac: action.macAddress ?? null, lookupError: null } };
    case "network/mutation-finished":
      return { ...state, network: { ...state.network, mutationPending: false,
        refreshVersion: state.network.refreshVersion + 1 } };
    case "network/open-node":
      return { ...state, activeView: "network", network: { ...state.network,
        selectedNodeId: action.nodeId, selectedLinkId: null, routeLookupMac: null, lookupError: null } };
    default:
      return state;
  }
}
