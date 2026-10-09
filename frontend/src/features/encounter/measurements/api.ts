import { apiRequest } from "../../../lib/apiClient";
import type { SpecialTestDefinition } from "./types";

const base = "/api/v1/special-tests";

export function fetchSpecialTests(filter: {
  search?: string;
  specialty?: number | null;
  favoritesOnly?: boolean;
  includeInactive?: boolean;
}) {
  const q = new URLSearchParams();
  if (filter.search?.trim()) q.set("search", filter.search.trim());
  if (filter.specialty != null) q.set("specialty", String(filter.specialty));
  if (filter.favoritesOnly) q.set("favoritesOnly", "true");
  if (filter.includeInactive) q.set("includeInactive", "true");
  return apiRequest<SpecialTestDefinition[]>(`${base}?${q.toString()}`);
}

export const setSpecialTestFavorite = (id: string, favorite: boolean) =>
  apiRequest<void>(`${base}/${id}/favorite`, {
    method: favorite ? "PUT" : "DELETE",
  });

export interface SaveSpecialTestBody {
  name: string;
  specialty: number;
  resultKind: number;
  bodyRegion: string | null;
  description: string | null;
  unit: string | null;
  interpretationGuide: string | null;
  contraindicationWarning: string | null;
}

export const createSpecialTest = (body: SaveSpecialTestBody) =>
  apiRequest<SpecialTestDefinition>(base, { method: "POST", body });

export const updateSpecialTest = (id: string, body: SaveSpecialTestBody) =>
  apiRequest<SpecialTestDefinition>(`${base}/${id}`, { method: "PUT", body });

export const setSpecialTestActive = (id: string, isActive: boolean) =>
  apiRequest<SpecialTestDefinition>(`${base}/${id}/active`, {
    method: "PUT",
    body: { isActive },
  });
