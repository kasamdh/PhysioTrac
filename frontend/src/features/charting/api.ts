import { apiRequest } from "../../lib/apiClient";
import type {
  ChartNote,
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
