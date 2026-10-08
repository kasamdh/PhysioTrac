import { apiRequest } from "../../../lib/apiClient";
import type { Goal, GoalHistoryEntry } from "./model";

export interface GoalDefinition {
  functionalLimitation: string;
  functionalTask: string;
  term: number;
  baselineValue: number;
  targetValue: number;
  unit: string;
  measurementMethod: string;
  targetDate: string;
  comments: string | null;
}

export const goalsKey = (patientId: string) => ["chart", "goals", patientId];

export const fetchPatientGoals = (patientId: string) =>
  apiRequest<Goal[]>(`/api/v1/goals/patient/${patientId}`);

export const createPatientGoal = (patientId: string, body: GoalDefinition) =>
  apiRequest<Goal>("/api/v1/goals", {
    method: "POST",
    body: { patientId, suggestedWording: null, ...body },
  });

export const editGoal = (id: string, body: GoalDefinition) =>
  apiRequest<Goal>(`/api/v1/goals/${id}`, { method: "PUT", body });

export const approvePatientGoal = (id: string) =>
  apiRequest<Goal>(`/api/v1/goals/${id}/approve`, { method: "POST" });

export const fetchGoalHistory = (id: string) =>
  apiRequest<GoalHistoryEntry[]>(`/api/v1/goals/${id}/history`);
