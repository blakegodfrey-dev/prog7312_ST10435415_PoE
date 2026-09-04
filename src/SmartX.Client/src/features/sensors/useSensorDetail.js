import { useCallback, useEffect, useState } from "react";
import { sensorsApi } from "../../api/sensorsApi";

export function useSensorDetail(sensorId) {
  const [sensor, setSensor] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);
  const [refreshKey, setRefreshKey] = useState(0);

  const refresh = useCallback(() => {
    setRefreshKey((current) => current + 1);
  }, []);

  useEffect(() => {
    const controller = new AbortController();

    async function loadSensor() {
      setIsLoading(true);
      setError(null);

      try {
        const result = await sensorsApi.getById(sensorId, {
          signal: controller.signal,
        });

        setSensor(result);
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

    loadSensor();

    return () => controller.abort();
  }, [sensorId, refreshKey]);

  return {
    sensor,
    isLoading,
    error,
    refresh,
  };
}
