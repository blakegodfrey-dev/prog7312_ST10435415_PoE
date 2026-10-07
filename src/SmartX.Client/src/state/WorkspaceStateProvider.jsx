import { useReducer } from "react";
import { WorkspaceStateContext } from "./WorkspaceStateContext.js";
import { createWorkspaceState, workspaceReducer } from "./workspaceState.js";

export function WorkspaceStateProvider({ children }) {
  const [state, dispatch] = useReducer(
    workspaceReducer,
    undefined,
    createWorkspaceState,
  );

  return (
    <WorkspaceStateContext.Provider value={{ state, dispatch }}>
      {children}
    </WorkspaceStateContext.Provider>
  );
}
