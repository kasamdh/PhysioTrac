import { apiRequest } from "../../lib/apiClient";
import type {
  ChartNote,
  NoteAddendum,
  NoteQueues,
  NoteRecord,
  NoteVersion,
  ComplianceFinding,
  Goal,
  Intervention,
  InterventionInput,
  InterventionSummary,
  OutcomeScore,
  PullForward,
} from "./types";

export const fetchChartNote = (id: string) =>
  apiRequest<ChartNote>(`/api/v1/notes/${id}`);

export interface ChartNoteUpdate {
  subjective: string;
  objective: string;
  interventions: string;
  assessment: string;
  plan: string;
  planOfCareStart: string | null;
  planOfCareEnd: string | null;
  frequencyPerWeek: number | null;
  durationWeeks: number | null;
  reassessmentDue: null;
  subjectiveDetailsJson: string;
  objectiveMeasurementsJson: string;
}

export const saveChartNote = (id: string, body: ChartNoteUpdate) =>
  apiRequest<ChartNote>(`/api/v1/notes/${id}`, { method: "PUT", body });

export const fetchCompliance = (id: string) =>
  apiRequest<ComplianceFinding[]>(`/api/v1/notes/${id}/compliance`);

export const fetchPullForward = (patientId: string) =>
  apiRequest<PullForward>(`/api/v1/notes/patient/${patientId}/pull-forward`);

export const fetchInterventions = (noteId: string) =>
  apiRequest<Intervention[]>(`/api/v1/notes/${noteId}/interventions`);
export const fetchInterventionSummary = (noteId: string) =>
  apiRequest<InterventionSummary>(
    `/api/v1/notes/${noteId}/interventions/summary`,
  );
export const addIntervention = (noteId: string, body: InterventionInput) =>
  apiRequest<Intervention>(`/api/v1/notes/${noteId}/interventions`, {
    method: "POST",
    body,
  });
export const updateIntervention = (
  noteId: string,
  id: string,
  body: InterventionInput,
) =>
  apiRequest<Intervention>(`/api/v1/notes/${noteId}/interventions/${id}`, {
    method: "PUT",
    body,
  });
export const deleteIntervention = (noteId: string, id: string) =>
  apiRequest<void>(`/api/v1/notes/${noteId}/interventions/${id}`, {
    method: "DELETE",
  });

export const fetchGoals = (patientId: string) =>
  apiRequest<Goal[]>(`/api/v1/goals/patient/${patientId}`);
export const updateGoalProgress = (goalId: string, currentValue: number) =>
  apiRequest<Goal>(`/api/v1/goals/${goalId}/progress`, {
    method: "PATCH",
    body: { currentValue },
  });

export const fetchOutcomes = (patientId: string) =>
  apiRequest<OutcomeScore[]>(`/api/v1/outcomes/patient/${patientId}`);
export const recordOutcome = (body: {
  patientId: string;
  noteId: string;
  measure: number;
  measuredOn: string;
  score: number;
  maximumScore: number | null;
  notes: null;
}) => apiRequest<OutcomeScore>("/api/v1/outcomes", { method: "POST", body });

/** Signing re-confirms identity (Documentation Phase 0): the signer's password. */
export const signChartNote = (id: string, password: string) =>
  apiRequest<ChartNote>(`/api/v1/notes/${id}/sign`, {
    method: "POST",
    body: { attestationConfirmed: true, password },
  });

/** Who wrote/cosigned the note, its addenda and amendment link, and what
 * the current user may do with it (the service's own rules). */
export const fetchNoteRecord = (id: string) =>
  apiRequest<NoteRecord>(`/api/v1/notes/${id}/record`);

export const cosignChartNote = (id: string, password: string) =>
  apiRequest<ChartNote>(`/api/v1/notes/${id}/cosign`, {
    method: "POST",
    body: { password },
  });

export const addAddendum = (id: string, reason: string, body: string) =>
  apiRequest<NoteAddendum>(`/api/v1/notes/${id}/addenda`, {
    method: "POST",
    body: { reason, body },
  });

/** Starts (or continues) a formal amendment; returns the amendment draft. */
export const amendNote = (id: string, reason: string) =>
  apiRequest<ChartNote>(`/api/v1/notes/${id}/amend`, {
    method: "POST",
    body: { reason },
  });

export const lockNote = (id: string) =>
  apiRequest<ChartNote>(`/api/v1/notes/${id}/lock`, { method: "POST" });

export const fetchNoteVersions = (id: string) =>
  apiRequest<NoteVersion[]>(`/api/v1/notes/${id}/versions`);

export const fetchNoteQueues = () =>
  apiRequest<NoteQueues>("/api/v1/notes/queues");
