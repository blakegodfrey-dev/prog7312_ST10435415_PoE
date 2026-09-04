import { useEffect, useState } from "react";
import { apiRequest } from "./api/apiClient";
import { SensorDirectory } from "./features/sensors/SensorDirectory";
import "./App.css";

function App() {
  const [apiStatus, setApiStatus] = useState("Checking...");
  const [activeView, setActiveView] = useState("startup");

  useEffect(() => {
    const controller = new AbortController();

    async function checkApi() {
      try {
        const result = await apiRequest("/api/health", {
          signal: controller.signal,
        });
        setApiStatus(result.status);
      } catch (error) {
        if (error?.name !== "AbortError") {
          setApiStatus("Unavailable");
        }
      }
    }

    checkApi();

    return () => controller.abort();
  }, []);

  if (activeView === "sensors") {
    return (
      <SensorDirectory
        onBack={() => setActiveView("startup")}
      />
    );
  }

  return (
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

        <article className="pillar-card disabled-card">
          <span className="status-badge disabled-badge">Part 2</span>
          <h2>Real-Time Command Stream and History</h2>
          <p>
            Command delivery and historical device activity will be available
            in Part 2.
          </p>
          <button type="button" disabled>
            Coming in Part 2
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
            Available in Final PoE
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
  );
}

export default App;
