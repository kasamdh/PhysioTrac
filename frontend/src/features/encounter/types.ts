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
  pain?: PainAssessment | null;
  bodyChart?: BodyFinding[] | null;
  previous?: PreviousCharting | null;
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
  pain?: PainAssessment;
  bodyChart?: BodyFinding[];
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

// Matches PhysioTrac.Application.Clinical.PainAssessmentDto.
export interface PainAssessment {
  scale: number; // 0 numeric 0-10, 1 visual analog 0-100, 2 faces, 3 verbal 0-3
  current: number | null;
  best: number | null;
  worst: number | null;
  beforeTreatment: number | null;
  afterTreatment: number | null;
  location: string | null;
  qualities: string[];
  frequency: number | null; // 0 constant, 1 intermittent, 2 occasional
  duration: string | null;
  irritability: number | null; // 0 low, 1 moderate, 2 high
  aggravatingFactors: string | null;
  easingFactors: string | null;
  dailyPattern: string | null;
  sleepImpact: string | null;
  functionalImpact: string | null;
}

// Matches BodyChartFindingDto.
export interface BodyFinding {
  id?: string | null;
  view: number;
  region: string;
  side: number;
  x: number;
  y: number;
  findingType: number;
  severity: number | null;
  radiatesTo: string | null;
  annotation: string | null;
  comment: string | null;
}

// Matches PreviousChartingDto.
export interface PreviousCharting {
  noteId: string;
  serviceDate: string;
  pain: PainAssessment | null;
  bodyChart: BodyFinding[];
}

export interface PainHistoryPoint {
  noteId: string;
  serviceDate: string;
  scale: number;
  current: number | null;
  worst: number | null;
  beforeTreatment: number | null;
  afterTreatment: number | null;
}
