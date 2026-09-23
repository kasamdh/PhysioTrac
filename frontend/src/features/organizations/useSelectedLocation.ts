import { useEffect, useState } from "react";
import type { LocationSummary } from "./types";

const STORAGE_KEY = "physiotrac.selectedLocationId";

/** Local-only UI state -- no page in this SPA foundation actually scopes
 * data by location yet (there's no location-filtered view to react to),
 * so switching here just remembers the choice for whenever one exists.
 * Falls back to the first location once the list loads, and clears itself
 * if the remembered id isn't in the caller's org anymore. */
export function useSelectedLocation(locations: LocationSummary[] | undefined) {
  const [selectedId, setSelectedId] = useState<string | null>(() => {
    try {
      return localStorage.getItem(STORAGE_KEY);
    } catch {
      return null;
    }
  });

  useEffect(() => {
    if (!locations || locations.length === 0) return;
    const stillValid = locations.some((l) => l.id === selectedId);
    if (!stillValid) {
      setSelectedId(locations[0].id);
    }
  }, [locations, selectedId]);

  function selectLocation(id: string) {
    setSelectedId(id);
    try {
      localStorage.setItem(STORAGE_KEY, id);
    } catch {
      // Best-effort only -- a private window or blocked storage just means
      // the choice won't survive a reload, which is fine.
    }
  }

  return { selectedId, selectLocation };
}
