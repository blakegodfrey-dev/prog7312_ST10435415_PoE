import { useEffect, useState } from "react";
import { apiRequest } from "./api/apiClient";
import { SensorDirectory } from "./features/sensors/SensorDirectory";
import { CommandStreamWorkspace } from "./features/commands/CommandStreamWorkspace.jsx";
import { ModuleNavigation } from "./components/ModuleNavigation.jsx";
import { WorkspaceStateProvider } from "./state/WorkspaceStateProvider.jsx";
import { useWorkspaceState } from "./state/useWorkspaceState.js";
import "./App.css";

function SmartXApplication() {
  const [apiStatus, setApiStatus] = useState("Checking...");
  const { state, dispatch } = useWorkspaceState();
  const { activeView } = state;
  const setActiveView = (view) => dispatch({ type: "navigate", view });

  useEffect(() => {
    const controller = new AbortController();

    async function checkApi() {
      try {
        const result = await apiRequest("/api/health", {
          signal: controller.signal,
        });
        if (!controller.signal.aborted) setApiStatus(result.status);
      } catch (error) {
        if (!controller.signal.aborted && error?.name !== "AbortError") {
          setApiStatus("Unavailable");
        }
      }
    }

    checkApi();

    return () => controller.abort();
  }, []);

  return (
    <>
      <ModuleNavigation />
      {activeView === "sensors" && (
        <SensorDirectory onBack={() => setActiveView("startup")} />
      )}
      {activeView === "commands" && <CommandStreamWorkspace />}
      {activeView === "startup" && (
        <main className="app-shell">
          <section className="hero">
            <p className="eyebrow">SMART-X</p>
            <h1>IoT Mesh Ecosystem</h1>
            <p className="hero-copy">
              Monitor and manage telemetry from the Smart Hydroponic Facility.
            </p>
          </section>

          <section className="pillar-grid" aria-label="Smart-X system areas">
            <article className="pillar-card active-card">
              <span className="status-badge">Part 1</span>
              <h2>Sensor Data Ingestion and Telemetry</h2>
              <p>
                Register simulated sensors, receive telemetry and investigate
                device health.
              </p>
              <button
                type="button"
                className="primary-button"
                onClick={() => setActiveView("sensors")}
              >
                Open Telemetry
              </button>
            </article>

            <article className="pillar-card active-card">
              <span className="status-badge">Part 2</span>
              <h2>Real-Time Command Stream and History</h2>
              <p>
                Open the operational workspace for live device activity,
                manual commands and ordered history.
              </p>
              <button
                type="button"
                className="primary-button"
                onClick={() => setActiveView("commands")}
              >
                Open Command Stream
              </button>
            </article>

            <article className="pillar-card disabled-card">
              <span className="status-badge disabled-badge">Final PoE</span>
              <h2>Network Topology and Mesh Routing</h2>
              <p>
                Mesh visualisation and routing management will be implemented in
                the final PoE.
              </p>
              <button type="button" disabled>
                Coming in Final PoE
              </button>
            </article>
          </section>

          <footer className="system-status">
            <span
              className={
                apiStatus === "Healthy"
                  ? "status-dot status-online"
                  : "status-dot status-offline"
              }
            />
            API Status: {apiStatus}
          </footer>
        </main>
      )}
    </>
  );
}

function App() {
  return (
    <WorkspaceStateProvider>
      <SmartXApplication />
    </WorkspaceStateProvider>
  );
}

export default App;
