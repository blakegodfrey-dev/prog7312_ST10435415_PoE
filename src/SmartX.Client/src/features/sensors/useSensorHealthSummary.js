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

    async function loadHealthSummary() {
      setIsLoading(true);
      setError(null);

      try {
        const result = await telemetryApi.getHealthSummary({
          signal: controller.signal,
        });

        setSummary(result);
      } catch (requestError) {
        if (requestError?.name !== "AbortError") {
          setError(requestError);
        }
      } finally {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      }
    }

    loadHealthSummary();

    return () => controller.abort();
  }, [refreshKey]);

  return {
    summary,
    isLoading,
    error,
    refresh,
  };
}