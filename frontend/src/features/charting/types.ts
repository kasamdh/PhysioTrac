import type { NoteStatus, NoteType } from "../workflow/types";

/** Structured subjective findings, stored in ClinicalNote.SubjectiveDetailsJson. */
export interface SubjectiveDetails {
  painNow: number | null;
  painBest: number | null;
  painWorst: number | null;
  painLocation: string;
  hepCompliance: "" | "yes" | "partial" | "no";
}

export type Side = "" | "L" | "R" | "B";

export interface RomRow {
  id: string;
  joint: string;
  motion: string;
  side: Side;
  arom: string;
  prom: string;
}

export interface MmtRow {
  id: string;
  muscle: string;
  side: Side;
  grade: string;
}

export interface SpecialTestRow {
  id: string;
  test: string;
  side: Side;
  result: "" | "positive" | "negative";
}

/** Structured examination findings, stored in ClinicalNote.ObjectiveMeasurementsJson. */
export interface ObjectiveDetails {
  rom: RomRow[];
  mmt: MmtRow[];
  specialTests: SpecialTestRow[];
  vitals: { bloodPressure: string; heartRate: string; spo2: string };
}

// Matches PhysioTrac.Application.Clinical.ClinicalNoteDto (fields charting uses).
export interface ChartNote {
  id: string;
  patientId: string;
  therapistId: string;
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
  signatureCredentials?: string | null;
  signedAt: string | null;
  cosignRequired: boolean;
  cosignedAt?: string | null;
  documentationStatus?: number;
  returnReason?: string | null;
  voidReason?: string | null;
  voidedAt?: string | null;
  subjectiveDetailsJson: string;
  objectiveMeasurementsJson: string;
  /** Set on a formal amendment: the signed note it corrects. */
  amendsNoteId?: string | null;
  amendmentReason?: string | null;
  templateVersionId?: string | null;
}

// Matches PhysioTrac.Application.Clinical.NoteActionsDto.
export interface NoteActions {
  canEdit: boolean;
  canSign: boolean;
  canCosign: boolean;
  canAddAddendum: boolean;
  canAmend: boolean;
  canLock: boolean;
  canStartReview?: boolean;
  canReturn?: boolean;
  canVoid?: boolean;
  /** Signing sends the note to a PT for cosignature. */
  signSubmitsForCosign?: boolean;
  /** Voiding re-confirms the voider's password (a signed note). */
  voidNeedsPassword?: boolean;
}

// Matches PhysioTrac.Application.Clinical.NoteAddendumDto.
export interface NoteAddendum {
  id: string;
  noteId: string;
  authorId: string;
  reason: string;
  body: string;
  createdAt: string;
  authorName: string | null;
}

// Matches PhysioTrac.Application.Clinical.NoteRecordDto.
export interface NoteRecord {
  actions: NoteActions;
  authorName: string;
  cosignedByName: string | null;
  addenda: NoteAddendum[];
  amendmentNoteId: string | null;
  amendmentStatus: NoteStatus | null;
}

// Matches PhysioTrac.Application.Clinical.ClinicalNoteVersionDto.
export interface NoteVersion {
  id: string;
  versionNumber: number;
  contentJson: string;
  savedById: string;
  savedByName: string | null;
  isSignedVersion: boolean;
  createdAt: string;
}

// Matches PhysioTrac.Application.Clinical.NoteQueueItemDto.
export interface NoteQueueItem {
  noteId: string;
  patientId: string;
  patientName: string;
  medicalRecordNumber: string;
  noteType: NoteType;
  status: NoteStatus;
  serviceDate: string;
  authorName: string;
  isAmendment: boolean;
}

// Matches PhysioTrac.Application.Clinical.NoteQueuesDto.
export interface NoteQueues {
  myUnsignedNotes: NoteQueueItem[];
  awaitingMyCosign: NoteQueueItem[];
}

// Mirrors PhysioTrac.Domain.Enums.InterventionCategory.
export const InterventionCategoryLabels: Record<number, string> = {
  0: "Therapeutic exercise",
  1: "Manual therapy",
  2: "Therapeutic activity",
  3: "Neuromuscular re-education",
  4: "Gait training",
  5: "Self-care / home management",
  6: "Patient education",
  7: "Other",
};

// Matches PhysioTrac.Application.Clinical.InterventionDto.
export interface Intervention {
  id: string;
  noteId: string;
  description: string;
  bodyRegion: string | null;
  category: number | null;
  minutes: number;
  units: number | null;
  isTimed: boolean;
  order: number;
  patientResponse: string | null;
}

export interface InterventionInput {
  description: string;
  bodyRegion: string | null;
  category: number | null;
  minutes: number;
  units: number | null;
  isTimed: boolean;
  order: number;
  patientResponse: string | null;
}

export interface InterventionSummary {
  timedMinutes: number;
  untimedCount: number;
  estimatedTimedUnits: number;
  ruleVariant: string;
}

// Matches PhysioTrac.Application.Clinical.ComplianceFinding.
export interface ComplianceFinding {
  code: string;
  severity: string;
  title: string;
  detail: string;
  finalizationBlocker: boolean;
}

// Matches PhysioTrac.Application.Clinical.FunctionalGoalDto.
export interface Goal {
  id: string;
  functionalLimitation: string;
  functionalTask: string;
  term: number; // 0 short-term, 1 long-term
  baselineValue: number;
  targetValue: number;
  currentValue: number | null;
  unit: string;
  measurementMethod: string;
  targetDate: string;
  status: number; // 0 draft, 1 in progress, 2 met, 3 discontinued, 4 not started, 5 partially met
  progressPercent: number | null;
}

// Matches PhysioTrac.Application.Clinical.OutcomeScoreDto.
export interface OutcomeScore {
  id: string;
  noteId: string | null;
  measure: number;
  measuredOn: string;
  score: number;
  maximumScore: number | null;
}

// Matches PhysioTrac.Application.Clinical.PullForwardDataDto.
export interface PullForward {
  activeGoals: Goal[];
  lastObjectiveMeasurementsJson: string | null;
  activeDiagnoses: {
    diagnosisCodeId: string;
    code: string;
    description: string;
    isPrimary: boolean;
  }[];
}
