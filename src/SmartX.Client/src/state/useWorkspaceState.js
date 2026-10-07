import { useContext } from "react";
import { WorkspaceStateContext } from "./WorkspaceStateContext.js";

export function useWorkspaceState() {
  const context = useContext(WorkspaceStateContext);
  if (!context) {
    throw new Error("Workspace components require WorkspaceStateProvider.");
  }
  return context;
}
