import { useWorkspaceState } from "../state/useWorkspaceState.js";

export function ModuleNavigation() {
  const { state, dispatch } = useWorkspaceState();
  const modules = [
    ["startup", "Home"],
    ["sensors", "Telemetry"],
    ["commands", "Command Stream"],
  ];

  return (
    <nav className="module-navigation" aria-label="Smart-X modules">
      <span className="module-brand">SMART-X</span>
      <div className="module-links">
        {modules.map(([view, label]) => (
          <button
            key={view}
            type="button"
            aria-current={state.activeView === view ? "page" : undefined}
            onClick={() => dispatch({ type: "navigate", view })}
          >
            {label}
          </button>
        ))}
      </div>
    </nav>
  );
}
