import { apiRequest } from "../../../lib/apiClient";
import type { GroupItem, InterventionGroup, LibraryItem } from "./model";

const base = "/api/v1/intervention-library";

export function searchInterventions(filter: {
  search?: string;
  category?: number | null;
  favoritesOnly?: boolean;
}) {
  const q = new URLSearchParams();
  if (filter.search?.trim()) q.set("search", filter.search.trim());
  if (filter.category != null) q.set("category", String(filter.category));
  if (filter.favoritesOnly) q.set("favoritesOnly", "true");
  return apiRequest<LibraryItem[]>(`${base}?${q.toString()}`);
}

export const setInterventionFavorite = (id: string, favorite: boolean) =>
  apiRequest<void>(`${base}/${id}/favorite`, {
    method: favorite ? "PUT" : "DELETE",
  });

export const fetchInterventionGroups = () =>
  apiRequest<InterventionGroup[]>(`${base}/groups`);

export const createInterventionGroup = (body: {
  name: string;
  description: string | null;
  isShared: boolean;
  items: GroupItem[];
}) => apiRequest<InterventionGroup>(`${base}/groups`, { method: "POST", body });

export const deleteInterventionGroup = (id: string) =>
  apiRequest<void>(`${base}/groups/${id}`, { method: "DELETE" });
