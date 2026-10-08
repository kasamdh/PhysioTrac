import type { ChartNote } from "../charting/types";
import type { FieldValue, TemplateVersion } from "../templates/types";

// Matches PhysioTrac.Application.Clinical.EncounterHeaderDto.
export interface EncounterHeader {
  patientId: string;
  patientName: string;
  dateOfBirth: string;
  age: number;
  medicalRecordNumber: string;
  appointmentId: string | null;
  appointmentStartsAt: string | null;
  appointmentEndsAt: string | null;
  visitNumber: number;
  visitType: string;
  authorName: string;
  treatingProviderName: string | null;
  supervisingProviderName: string | null;
  referringProviderName: string | null;
  diagnoses: string[];
  allergies: string[];
  precautions: string | null;
  activePlanOfCareId: string | null;
  planOfCareStart: string | null;
  planOfCareEnd: string | null;
}

// Matches EncounterDto.
export interface Encounter {
  note: ChartNote & {
    templateVersionId?: string | null;
    planOfCareId?: string | null;
  };
  templateName: string | null;
  template: TemplateVersion | null;
  values: FieldValue[];
  header: EncounterHeader;
  saveVersion: number;
  lastSavedAt: string;
  lastSavedByName: string | null;
}

// Matches SaveEncounterRequest.
export interface SaveEncounterBody {
  baseSaveVersion: number;
  values?: FieldValue[];
  subjective?: string;
  objective?: string;
  interventions?: string;
  assessment?: string;
  plan?: string;
  subjectiveDetailsJson?: string;
  objectiveMeasurementsJson?: string;
}

export interface EncounterSaveResult {
  saveVersion: number;
  savedAt: string;
  savedByName: string | null;
}

/** 409 EDIT_CONFLICT body. */
export interface EditConflict {
  detail: string;
  code: "EDIT_CONFLICT";
  saveVersion: number;
  savedAt: string;
  savedByName: string | null;
}

// Matches PlanOfCareDto.
export interface PlanOfCare {
  id: string;
  status: number; // 0 draft, 1 active, 2 superseded, 3 discharged, 4 expired
  startDate: string;
  endDate: string;
  frequencyPerWeek: number | null;
  durationWeeks: number | null;
  treatmentDiagnosis: string | null;
  prognosis: string | null;
  plannedInterventions: string | null;
  homeProgram: string | null;
  sourceNoteId: string;
}

// PatientDiagnosesController / PatientAllergiesController / PatientMedicationsController DTOs.
export interface PatientDiagnosis {
  id: string;
  diagnosisCodeId: string;
  code: string;
  description: string;
  isPrimary: boolean;
  isResolved: boolean;
}
export interface DiagnosisCode {
  id: string;
  code: string;
  description: string;
}
export interface PatientAllergy {
  id: string;
  allergen: string;
  reaction: string | null;
  isActive: boolean;
}
export interface PatientMedication {
  id: string;
  name: string;
  dosage: string | null;
  frequency: string | null;
  isActive: boolean;
}
