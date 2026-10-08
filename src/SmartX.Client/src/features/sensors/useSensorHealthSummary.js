import { useCallback, useEffect, useState } from "react";
import { telemetryApi } from "../../api/telemetryApi";

export function useSensorHealthSummary() {
  const [summary, setSummary] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);
  const [refreshKey, setRefreshKey] = useState(0);

  const refresh = useCallback(() => {
    setRefreshKey((current) => current + 1);
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    let timer;

    async function loadHealthSummary(initial = false) {
      if (initial) { setIsLoading(true); setError(null); }
      try {
        const result = await telemetryApi.getHealthSummary({
          signal: controller.signal,
        });

        if (controller.signal.aborted) return;
        setError(null);

        setSummary(result);
      } catch (requestError) {
        if (!controller.signal.aborted && requestError?.name !== "AbortError") {
          setError(requestError);
        }
      } finally {
        if (!controller.signal.aborted) {
          setIsLoading(false);
          timer = setTimeout(loadHealthSummary, 2000);
        }
      }
    }

    loadHealthSummary(true);

    return () => { controller.abort(); clearTimeout(timer); };
  }, [refreshKey]);

  return {
    summary,
    isLoading,
    error,
    refresh,
  };
}
