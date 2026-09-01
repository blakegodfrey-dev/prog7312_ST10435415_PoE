import { useEffect, useState } from "react";
import { apiRequest } from "./api/apiClient";

function App() {
  const [apiStatus, setApiStatus] = useState("Checking...");
  const [error, setError] = useState(null);

  useEffect(() => {
    async function checkApi() {
      try {
        const result = await apiRequest("/api/health");
        setApiStatus(result.status);
      } catch (err) {
        setApiStatus("Unavailable");
        setError(err.message);
      }
    }

    checkApi();
  }, []);

  console.log("API URL:", import.meta.env.VITE_API_BASE_URL);

  return (
    <main>
      <h1>Smart-X</h1>
      <p>API Status: {apiStatus}</p>

      {error && (
        <p>
          Connection error: {error}
        </p>
      )}
    </main>
  );
}

export default App;