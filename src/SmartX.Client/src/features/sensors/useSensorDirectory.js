import { useCallback, useEffect, useState } from "react";
import { deploymentNodesApi } from "../../api/deploymentNodesApi";
import { sensorsApi } from "../../api/sensorsApi";

export function useSensorDirectory({
  category,
  deploymentNodeId,
  search,
}) {
  const [sensors, setSensors] = useState([]);
  const [locations, setLocations] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);
  const [refreshKey, setRefreshKey] = useState(0);

  const refresh = useCallback(() => {
    setRefreshKey((current) => current + 1);
  }, []);

  useEffect(() => {
    const controller = new AbortController();

    async function loadDirectory() {
      setIsLoading(true);
      setError(null);

      try {
        const [sensorResults, locationResults] = await Promise.all([
          sensorsApi.list(
            {
              category,
              deploymentNodeId,
              search,
            },
            {
              signal: controller.signal,
            },
          ),
          deploymentNodesApi.listSensorLocations({
            signal: controller.signal,
          }),
        ]);

        setSensors(sensorResults);
        setLocations(locationResults);
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

    loadDirectory();

    return () => controller.abort();
  }, [
    category,
    deploymentNodeId,
    search,
    refreshKey,
  ]);

  return {
    sensors,
    locations,
    isLoading,
    error,
    refresh,
  };
}
