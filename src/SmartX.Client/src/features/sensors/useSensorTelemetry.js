import { useCallback, useEffect, useState } from "react";
import { telemetryApi } from "../../api/telemetryApi";

export function useSensorTelemetry({
  sensorId,
  isValid,
  page,
  pageSize,
}) {
  const [history, setHistory] = useState(null);
  const [connectionStatus, setConnectionStatus] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);
  const [refreshKey, setRefreshKey] = useState(0);

  const refresh = useCallback(() => {
    setRefreshKey((current) => current + 1);
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    let timer;

    async function loadTelemetry(initial = false) {
      if (initial) { setIsLoading(true); setError(null); }
      try {
        const [historyResult, statusResult] = await Promise.all([
          telemetryApi.getHistory(
            sensorId,
            {
              isValid,
              page,
              pageSize,
            },
            {
              signal: controller.signal,
            },
          ),
          telemetryApi.getConnectionStatus(sensorId, {
            signal: controller.signal,
          }),
        ]);

        if (controller.signal.aborted) return;
        setError(null);

        setHistory(historyResult);
        setConnectionStatus(statusResult);
      } catch (requestError) {
        if (!controller.signal.aborted && requestError?.name !== "AbortError") {
          setError(requestError);
        }
      } finally {
        if (!controller.signal.aborted) {
          setIsLoading(false);
          timer = setTimeout(loadTelemetry, 2000);
        }
      }
    }

    loadTelemetry(true);

    return () => { controller.abort(); clearTimeout(timer); };
  }, [
    sensorId,
    isValid,
    page,
    pageSize,
    refreshKey,
  ]);

  return {
    history,
    connectionStatus,
    isLoading,
    error,
    refresh,
  };
}
