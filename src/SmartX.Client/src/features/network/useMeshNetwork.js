import { useCallback, useEffect, useRef, useState } from "react";
import { meshApi } from "../../api/meshApi.js";

export function useMeshNetwork(selectedNodeId, routeLookupMac, refreshVersion = 0) {
  const [result, setResult] = useState({ snapshot: null, error: null, key: null });
  const [refreshKey, setRefreshKey] = useState(0);
  const currentController = useRef(null);
  const key = `${selectedNodeId ?? ""}|${routeLookupMac ?? ""}|${refreshKey}|${refreshVersion}`;
  const refresh = useCallback(() => {
    if (currentController.current && !currentController.current.signal.aborted) setRefreshKey(n => n + 1);
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    currentController.current = controller;
    let timer;
    async function poll() {
      try {
        const snapshot = await meshApi.snapshot({ nodeId: selectedNodeId, macAddress: routeLookupMac }, { signal: controller.signal });
        if (!controller.signal.aborted) setResult({ snapshot, error: null, key });
      } catch (error) {
        if (!controller.signal.aborted) setResult(previous => ({ ...previous, error, key }));
      } finally {
        if (!controller.signal.aborted) timer = setTimeout(poll, 2000);
      }
    }
    poll();
    return () => { controller.abort(); clearTimeout(timer); };
  }, [selectedNodeId, routeLookupMac, key, refreshKey]);

  return { snapshot: result.snapshot, error: result.key === key ? result.error : null,
    isLoading: result.key !== key, refresh };
}
