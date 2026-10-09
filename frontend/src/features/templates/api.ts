import { apiRequest } from "../../lib/apiClient";
import type {
  DocTemplate,
  DocTemplateDetail,
  SaveTemplateRequest,
  TemplateVersion,
  TemplateVersionSummary,
} from "./types";

const base = "/api/v1/documentation-templates";

export function fetchTemplates(filter: {
  noteType?: number | null;
  specialty?: number | null;
  includeInactive?: boolean;
  search?: string;
}) {
  const q = new URLSearchParams();
  if (filter.noteType != null) q.set("noteType", String(filter.noteType));
  if (filter.specialty != null) q.set("specialty", String(filter.specialty));
  if (filter.includeInactive) q.set("includeInactive", "true");
  if (filter.search?.trim()) q.set("search", filter.search.trim());
  return apiRequest<DocTemplate[]>(`${base}?${q.toString()}`);
}

export const fetchTemplate = (id: string) =>
  apiRequest<DocTemplateDetail>(`${base}/${id}`);

export const fetchTemplateVersions = (id: string) =>
  apiRequest<TemplateVersionSummary[]>(`${base}/${id}/versions`);

export const fetchTemplateVersion = (versionId: string) =>
  apiRequest<TemplateVersion>(`${base}/versions/${versionId}`);

export const createTemplate = (body: SaveTemplateRequest) =>
  apiRequest<DocTemplateDetail>(base, { method: "POST", body });

export const updateTemplate = (id: string, body: SaveTemplateRequest) =>
  apiRequest<DocTemplateDetail>(`${base}/${id}`, { method: "PUT", body });

export const setTemplateActive = (id: string, isActive: boolean) =>
  apiRequest<DocTemplate>(`${base}/${id}/active`, {
    method: "PUT",
    body: { isActive },
  });

export const copyTemplate = (id: string, name?: string) =>
  apiRequest<DocTemplateDetail>(`${base}/${id}/copy`, {
    method: "POST",
    body: { name: name ?? null },
  });

export const setTemplateFavorite = (id: string, favorite: boolean) =>
  apiRequest<void>(`${base}/${id}/favorite`, {
    method: favorite ? "PUT" : "DELETE",
  });
