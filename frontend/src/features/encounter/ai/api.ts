import { apiRequest } from "../../../lib/apiClient";

export type AiSection = "assessment" | "plan";

// Matches AiStatusDto / AiDraftDto (section is a number: 0 assessment, 1 plan).
export interface AiStatus {
  enabled: boolean;
  provider: string;
}
export interface AiDraft {
  section: number;
  text: string;
  provider: string;
  generatedAt: string;
  notice: string;
}

export const fetchAiStatus = () =>
  apiRequest<AiStatus>("/api/v1/documentation/ai/status");

/** A suggestion only: the note is unchanged until the clinician inserts it. */
export const draftWithAi = (noteId: string, section: AiSection) =>
  apiRequest<AiDraft>(`/api/v1/notes/${noteId}/ai/${section}/draft`, {
    method: "POST",
  });

/** Records the insert (the note is marked AI-assisted when signed). */
export const recordAiInserted = (noteId: string, section: AiSection) =>
  apiRequest<void>(`/api/v1/notes/${noteId}/ai/${section}/inserted`, {
    method: "POST",
  });
