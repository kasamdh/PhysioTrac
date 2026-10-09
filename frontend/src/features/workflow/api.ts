import { apiRequest } from "../../lib/apiClient";
import type { NoteFields, NoteType, VisitNote, WorkflowDay } from "./types";

export function fetchWorkflowToday(opts: { providerId?: string; all?: boolean }): Promise<WorkflowDay> {
  const q = new URLSearchParams();
  if (opts.providerId) q.set("providerId", opts.providerId);
  if (opts.all) q.set("allProviders", "true");
  const text = q.toString();
  return apiRequest<WorkflowDay>(`/api/v1/workflow/today${text ? `?${text}` : ""}`);
}

export const createVisitNote = (
  req: { patientId: string; appointmentId: string; noteType: NoteType; serviceDate: string } & NoteFields,
) => apiRequest<VisitNote>("/api/v1/notes", { method: "POST", body: req });
