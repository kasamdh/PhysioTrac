import { apiRequest } from "../../../lib/apiClient";
import type { ItemResponse, OutcomeDefinition, OutcomeScore } from "./model";

export const fetchOutcomeDefinitions = () =>
  apiRequest<OutcomeDefinition[]>("/api/v1/outcomes/measures");

export const fetchPatientOutcomes = (patientId: string) =>
  apiRequest<OutcomeScore[]>(`/api/v1/outcomes/patient/${patientId}`);

export const recordOutcomeScore = (body: {
  patientId: string;
  noteId: string | null;
  measure: number;
  measuredOn: string;
  score: number | null;
  maximumScore: number | null;
  notes: string | null;
  itemResponses: ItemResponse[] | null;
}) => apiRequest<OutcomeScore>("/api/v1/outcomes", { method: "POST", body });

export const deleteOutcomeScore = (id: string) =>
  apiRequest<void>(`/api/v1/outcomes/${id}`, { method: "DELETE" });
