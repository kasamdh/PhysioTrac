import { apiRequest } from "../../lib/apiClient";
import type { AuditLogFilters, AuditLogPage } from "./types";

export function fetchAuditLogs(f: AuditLogFilters, signal?: AbortSignal): Promise<AuditLogPage> {
  const q = new URLSearchParams();
  for (const [k, v] of Object.entries(f)) if (v !== undefined && v !== "") q.set(k, String(v));
  return apiRequest<AuditLogPage>(`/api/v1/audit-logs?${q}`, { signal });
}

/** Records that the user opened a screen. Path only -- never the query string. */
export function recordPageView(path: string): Promise<void> {
  return apiRequest<void>("/api/v1/activity/page-view", { method: "POST", body: { path } });
}
