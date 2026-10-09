import type { AppointmentKind, AppointmentStatus, ProviderDiscipline } from "../schedule/types";

// Mirrors PhysioTrac.Domain.Enums.NoteType (only the values this page uses are named).
export const NoteType = { Evaluation: 0, Daily: 1, Progress: 4, ReEvaluation: 5, Discharge: 6 } as const;
export type NoteType = number;

export const NoteTypeLabels: Record<number, string> = {
  0: "Evaluation",
  1: "Daily note",
  2: "SOAP note",
  3: "Home visit",
  4: "Progress note",
  5: "Re-evaluation",
  6: "Discharge summary",
  7: "Handoff",
  8: "Plan of care",
  9: "Dry needling treatment",
  10: "Pelvic health evaluation",
  11: "Recertification",
  12: "Consultation",
  13: "Communication",
  14: "Missed visit",
  15: "Addendum",
};

/** Note types documentation templates are written for, in menu order. */
export const TEMPLATE_NOTE_TYPES = [0, 1, 4, 5, 11, 6, 12, 13, 14, 15];

/** Note types whose signing requires plan-of-care details (NoteComplianceEvaluator). */
export const needsPlanOfCare = (t: NoteType) =>
  t === NoteType.Evaluation || t === NoteType.Progress || t === NoteType.ReEvaluation;

// Mirrors PhysioTrac.Domain.Enums.NoteStatus.
export const NoteStatus = {
  Draft: 0,
  ReviewRequired: 1,
  Signed: 2,
  Amended: 3,
  Locked: 4,
  ReturnedForCorrection: 5,
  Voided: 6,
  InReview: 7,
} as const;
export type NoteStatus = (typeof NoteStatus)[keyof typeof NoteStatus];

export const NoteStatusLabels: Record<number, string> = {
  0: "Draft",
  1: "Cosign required",
  2: "Signed",
  3: "Amended",
  4: "Signed · locked",
  5: "Returned for correction",
  6: "Voided",
  7: "In review",
};

// Mirrors PhysioTrac.Application.Clinical.DocumentationStatus: stored
// statuses plus derived ones (not started, ready to sign, cosigned).
export const DocumentationStatus = {
  NotStarted: 0,
  Draft: 1,
  InReview: 2,
  ReturnedForCorrection: 3,
  ReadyToSign: 4,
  Signed: 5,
  CosignRequired: 6,
  Cosigned: 7,
  Amended: 8,
  Locked: 9,
  Voided: 10,
} as const;

export const DocumentationStatusLabels: Record<number, string> = {
  0: "Not started",
  1: "Draft",
  2: "In review",
  3: "Returned for correction",
  4: "Ready to sign",
  5: "Signed",
  6: "Cosign required",
  7: "Cosigned",
  8: "Amended",
  9: "Locked",
  10: "Voided",
};

// Matches PhysioTrac.Api.Controllers.WorkflowProviderDto.
export interface WorkflowProvider {
  id: string;
  name: string;
  credentials: string | null;
  discipline: ProviderDiscipline;
  userId: string | null;
}

// Matches PhysioTrac.Api.Controllers.WorkflowAppointmentDto.
export interface WorkflowAppointment {
  appointmentId: string;
  startsAt: string;
  endsAt: string;
  status: AppointmentStatus;
  kind: AppointmentKind;
  appointmentTypeName: string | null;
  appointmentTypeColor: string | null;
  patientId: string;
  patientName: string;
  medicalRecordNumber: string;
  providerId: string | null;
  providerName: string | null;
  locationName: string | null;
  suggestedNoteType: NoteType;
  noteId: string | null;
  noteStatus: NoteStatus | null;
  noteType: NoteType | null;
}

// Matches PhysioTrac.Api.Controllers.WorkflowDayDto.
export interface WorkflowDay {
  date: string;
  timezone: string;
  providers: WorkflowProvider[];
  myProviderId: string | null;
  selectedProviderId: string | null;
  allProviders: boolean;
  ownDayOnly: boolean;
  appointments: WorkflowAppointment[];
}

// Matches PhysioTrac.Application.Clinical.ClinicalNoteDto (fields this page uses).
export interface VisitNote {
  id: string;
  patientId: string;
  appointmentId: string | null;
  noteType: NoteType;
  status: NoteStatus;
  serviceDate: string;
  subjective: string | null;
  objective: string | null;
  interventions: string | null;
  assessment: string | null;
  plan: string | null;
  planOfCareStart: string | null;
  planOfCareEnd: string | null;
  frequencyPerWeek: number | null;
  durationWeeks: number | null;
  signatureName: string | null;
  signedAt: string | null;
  cosignRequired: boolean;
}

export interface NoteFields {
  subjective: string | null;
  objective: string | null;
  interventions: string | null;
  assessment: string | null;
  plan: string | null;
  planOfCareStart: string | null;
  planOfCareEnd: string | null;
  frequencyPerWeek: number | null;
  durationWeeks: number | null;
  reassessmentDue: null;
}
