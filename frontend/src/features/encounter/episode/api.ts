import { apiRequest } from "../../../lib/apiClient";

// Matches EpisodeSummaryDto.
export interface EpisodeSummary {
  noteType: number;
  episodeStart: string;
  periodStart: string;
  periodEnd: string;
  periodBasis: string;
  planOfCareId: string | null;
  planStart: string | null;
  planEnd: string | null;
  frequencyPerWeek: number | null;
  durationWeeks: number | null;
  visitsInPeriod: number;
  visitsInEpisode: number;
  attendance: {
    attended: number;
    cancelled: number;
    noShows: number;
    text: string;
  };
  painSummary: string | null;
  measurementChanges: string[];
  outcomeChanges: string[];
  goalLines: string[];
  evaluationNoteId: string | null;
  evaluationDate: string | null;
  sinceEvaluation: string[];
  previousProgressNoteId: string | null;
  previousProgressDate: string | null;
  sinceProgress: string[];
  homeProgram: string | null;
  signedNotesUsed: number;
}

export interface PrefillResult {
  saveVersion: number;
  filledFields: string[];
  goalsAdded: number;
  summary: EpisodeSummary;
}

/** Note types written from the episode's charting (NoteType values). */
export const SUMMARY_NOTE_TYPES = new Set([4, 5, 6, 11]);

export const fetchEpisodeSummary = (noteId: string) =>
  apiRequest<EpisodeSummary>(`/api/v1/notes/${noteId}/episode-summary`);

export const prefillNote = (noteId: string, baseSaveVersion: number) =>
  apiRequest<PrefillResult>(`/api/v1/notes/${noteId}/prefill`, {
    method: "POST",
    body: { baseSaveVersion },
  });

export const reviewPrefill = (noteId: string) =>
  apiRequest<{ prefillReviewedAt: string | null }>(
    `/api/v1/notes/${noteId}/prefill/review`,
    { method: "POST" },
  );
