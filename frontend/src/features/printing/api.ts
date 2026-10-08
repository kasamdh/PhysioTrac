import { apiRequest } from "../../lib/apiClient";
import type { BodyFinding } from "../encounter/types";

export type OutputKind = "print" | "export";

/** The patient-level reports that can be printed (NotesController / PatientReports). */
export const PatientReports = {
  "plan-of-care": "Plan of care",
  "body-chart": "Body chart history",
  measurements: "Measurement comparison",
  goals: "Goal progress",
  outcomes: "Outcome measure history",
} as const;
export type PatientReport = keyof typeof PatientReports;

/** Records a print or PDF export of a note; refused (403) when the caller may not view it. */
export const recordNoteOutput = (noteId: string, kind: OutputKind) =>
  apiRequest<void>(`/api/v1/notes/${noteId}/output`, {
    method: "POST",
    body: { kind },
  });

export const recordReportOutput = (
  patientId: string,
  report: PatientReport,
  kind: OutputKind,
) =>
  apiRequest<void>(`/api/v1/notes/patient/${patientId}/report-output`, {
    method: "POST",
    body: { report, kind },
  });

// Matches BodyChartHistoryDto.
export interface BodyChartHistory {
  noteId: string;
  serviceDate: string;
  noteType: number;
  findings: BodyFinding[];
}
export const fetchBodyChartHistory = (patientId: string) =>
  apiRequest<BodyChartHistory[]>(
    `/api/v1/notes/patient/${patientId}/body-charts`,
  );
