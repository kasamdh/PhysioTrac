// Mirrors PhysioTrac.Domain.Enums.TemplateFieldType (serialized as numbers).
export const FieldType = {
  ShortText: 0,
  LongText: 1,
  Number: 2,
  Date: 3,
  Time: 4,
  Checkbox: 5,
  Radio: 6,
  Select: 7,
  Multiselect: 8,
  ClinicalMeasurement: 9,
  PainScale: 10,
  StructuredTable: 11,
  Signature: 12,
} as const;
export type FieldType = (typeof FieldType)[keyof typeof FieldType];

export const FieldTypeLabels: Record<FieldType, string> = {
  0: "Short text",
  1: "Long text",
  2: "Number",
  3: "Date",
  4: "Time",
  5: "Checkbox",
  6: "Radio buttons",
  7: "Select",
  8: "Multiselect",
  9: "Clinical measurement",
  10: "Pain scale",
  11: "Structured table",
  12: "Signature",
};

export const OPTION_TYPES: ReadonlySet<FieldType> = new Set([
  FieldType.Radio,
  FieldType.Select,
  FieldType.Multiselect,
]);
export const TEXT_TYPES: ReadonlySet<FieldType> = new Set([
  FieldType.ShortText,
  FieldType.LongText,
]);

// Mirrors PhysioTrac.Domain.Enums.ClinicalSpecialty.
export const SpecialtyLabels: Record<number, string> = {
  0: "General",
  1: "Orthopedic",
  2: "Neurological",
  3: "Pelvic health",
  4: "Sports rehabilitation",
  5: "Post-surgical",
  6: "Gait and balance",
  7: "Vestibular",
  8: "Cardiopulmonary",
  9: "TMJ",
  10: "Persistent pain",
  11: "Dry needling",
};

/** Built-in clinical components a section can embed (TemplateRules.Components). */
export const ComponentLabels: Record<string, string> = {
  painAssessment: "Pain assessment",
  bodyChart: "Body chart",
  measurements: "Measurements (ROM, strength…)",
  specialTests: "Special tests",
  outcomes: "Outcome measures",
  interventions: "Interventions flowsheet",
  goals: "Goals",
  planOfCare: "Plan of care",
  diagnoses: "Diagnoses (ICD-10)",
  medicalHistory: "Medications & allergies",
  carryForward: "Carry forward",
};

export const NoteColumnLabels: Record<string, string> = {
  subjective: "Subjective",
  objective: "Objective",
  interventions: "Treatment summary",
  assessment: "Assessment",
  plan: "Plan",
};

export interface TableColumn {
  key: string;
  label: string;
}
export interface FieldValidation {
  min?: number | null;
  max?: number | null;
  maxLength?: number | null;
  pattern?: string | null;
  patternMessage?: string | null;
}
export interface FieldCondition {
  field: string;
  value?: string | null;
  anyOf?: string[] | null;
  notEmpty?: boolean | null;
}

// Matches PhysioTrac.Application.Clinical.TemplateFieldDto.
export interface TemplateField {
  key: string;
  label: string;
  fieldType: FieldType;
  isRequired: boolean;
  helpText?: string | null;
  placeholder?: string | null;
  unit?: string | null;
  noteColumn?: string | null;
  options?: string[] | null;
  columns?: TableColumn[] | null;
  scaleMin?: number | null;
  scaleMax?: number | null;
  validation?: FieldValidation | null;
  condition?: FieldCondition | null;
  id?: string | null;
  displayOrder?: number;
}

// Matches TemplateSectionDto.
export interface TemplateSection {
  key: string;
  title: string;
  fields: TemplateField[];
  helpText?: string | null;
  component?: string | null;
  displayOrder?: number;
}

// Matches TemplateVersionDto.
export interface TemplateVersion {
  id: string;
  templateId: string;
  versionNumber: number;
  changeSummary: string | null;
  createdAt: string;
  createdByName: string | null;
  sections: TemplateSection[];
}

export interface TemplateVersionSummary {
  id: string;
  versionNumber: number;
  changeSummary: string | null;
  createdAt: string;
  createdByName: string | null;
  notesUsing: number;
}

// Matches DocumentationTemplateDto.
export interface DocTemplate {
  id: string;
  name: string;
  noteType: number;
  specialty: number;
  description: string | null;
  isSystem: boolean;
  isActive: boolean;
  currentVersionId: string;
  currentVersionNumber: number;
  appointmentTypeIds: string[];
  isFavorite: boolean;
  updatedAt: string;
}

export interface DocTemplateDetail {
  template: DocTemplate;
  currentVersion: TemplateVersion;
}

export interface SaveTemplateRequest {
  name: string;
  noteType: number;
  specialty: number;
  description: string | null;
  appointmentTypeIds: string[];
  changeSummary: string | null;
  sections: TemplateSection[];
}

/** One field's value on a note (TemplateFieldValueDto). */
export interface FieldValue {
  key: string;
  text?: string | null;
  number?: number | null;
  date?: string | null;
  time?: string | null;
  bool?: boolean | null;
  json?: string | null;
}

export type FieldValues = Record<string, FieldValue>;
