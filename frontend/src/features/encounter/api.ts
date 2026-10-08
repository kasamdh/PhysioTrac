import { apiRequest } from "../../lib/apiClient";
import type {
  DiagnosisCode,
  PainHistoryPoint,
  Encounter,
  EncounterSaveResult,
  PatientAllergy,
  PatientDiagnosis,
  PatientMedication,
  PlanOfCare,
  SaveEncounterBody,
} from "./types";

export const fetchEncounter = (noteId: string) =>
  apiRequest<Encounter>(`/api/v1/notes/${noteId}/encounter`);

export const saveEncounter = (noteId: string, body: SaveEncounterBody) =>
  apiRequest<EncounterSaveResult>(`/api/v1/notes/${noteId}/encounter`, {
    method: "PUT",
    body,
  });

export const changeNoteTemplate = (noteId: string, templateId: string) =>
  apiRequest<unknown>(`/api/v1/notes/${noteId}/template`, {
    method: "PUT",
    body: { templateId },
  });

export const fetchPlansOfCare = (patientId: string) =>
  apiRequest<PlanOfCare[]>(`/api/v1/notes/patient/${patientId}/plans-of-care`);

export const fetchPatientDiagnoses = (patientId: string) =>
  apiRequest<PatientDiagnosis[]>(`/api/v1/patients/${patientId}/diagnoses`);

export const searchDiagnosisCodes = (q: string) =>
  apiRequest<DiagnosisCode[]>(
    `/api/v1/diagnosis-codes?q=${encodeURIComponent(q)}&limit=15`,
  );

export const addPatientDiagnosis = (
  patientId: string,
  diagnosisCodeId: string,
  isPrimary: boolean,
) =>
  apiRequest<PatientDiagnosis>(`/api/v1/patients/${patientId}/diagnoses`, {
    method: "POST",
    body: { diagnosisCodeId, isPrimary, diagnosedDate: null, notes: null },
  });

export const fetchAllergies = (patientId: string) =>
  apiRequest<PatientAllergy[]>(`/api/v1/patients/${patientId}/allergies`);

export const addAllergy = (
  patientId: string,
  allergen: string,
  reaction: string,
) =>
  apiRequest<PatientAllergy>(`/api/v1/patients/${patientId}/allergies`, {
    method: "POST",
    body: { allergen, reaction: reaction || null, severity: 0, notes: null },
  });

export const fetchMedications = (patientId: string) =>
  apiRequest<PatientMedication[]>(`/api/v1/patients/${patientId}/medications`);

export const addMedication = (
  patientId: string,
  name: string,
  dosage: string,
  frequency: string,
) =>
  apiRequest<PatientMedication>(`/api/v1/patients/${patientId}/medications`, {
    method: "POST",
    body: {
      name,
      dosage: dosage || null,
      frequency: frequency || null,
      prescribingProvider: null,
      startDate: null,
      notes: null,
    },
  });

export interface NewGoal {
  patientId: string;
  functionalLimitation: string;
  functionalTask: string;
  term: number;
  baselineValue: number;
  targetValue: number;
  unit: string;
  measurementMethod: string;
  targetDate: string;
  suggestedWording: null;
}
export const createGoal = (body: NewGoal) =>
  apiRequest<unknown>("/api/v1/goals", { method: "POST", body });
export const approveGoal = (goalId: string) =>
  apiRequest<unknown>(`/api/v1/goals/${goalId}/approve`, { method: "POST" });

/** The encounter for an appointment: its note, or a new draft (one per visit). */
export const openAppointmentEncounter = (appointmentId: string) =>
  apiRequest<{ noteId: string; created: boolean }>(
    `/api/v1/notes/for-appointment/${appointmentId}`,
    { method: "POST" },
  );

export interface EncounterStatus {
  saveVersion: number;
  savedAt: string;
  savedByName: string | null;
  savedById: string | null;
  status: number;
}
export const fetchEncounterStatus = (noteId: string) =>
  apiRequest<EncounterStatus>(`/api/v1/notes/${noteId}/encounter/status`);

/** A note not tied to an appointment (communication, consultation...). */
export const createPatientNote = (
  patientId: string,
  noteType: number,
  serviceDate: string,
) =>
  apiRequest<{ id: string }>("/api/v1/notes", {
    method: "POST",
    body: {
      patientId,
      noteType,
      serviceDate,
      appointmentId: null,
      subjective: null,
      objective: null,
      interventions: null,
      assessment: null,
      plan: null,
      planOfCareStart: null,
      planOfCareEnd: null,
      frequencyPerWeek: null,
      durationWeeks: null,
      reassessmentDue: null,
    },
  });

export const fetchPainHistory = (patientId: string) =>
  apiRequest<PainHistoryPoint[]>(
    `/api/v1/notes/patient/${patientId}/pain-history`,
  );

export interface PatientMeasurement {
  noteId: string;
  serviceDate: string;
  category: number;
  item: string;
  movement: string | null;
  side: number | null;
  mode: string | null;
  unit: string | null;
  numericValue: number | null;
  textValue: string | null;
}
export const fetchMeasurementHistory = (patientId: string) =>
  apiRequest<PatientMeasurement[]>(
    `/api/v1/notes/patient/${patientId}/measurement-history`,
  );
