import { apiRequest } from "../../../lib/apiClient";

// Matches DashboardItemDto: a visit or a note.
export interface DashboardItem {
  patientId: string;
  patientName: string;
  medicalRecordNumber: string;
  date: string;
  noteId: string | null;
  noteType: number | null;
  noteStatus: number | null;
  documentationStatus: number | null;
  appointmentId: string | null;
  startsAt: string | null;
  appointmentStatus: number | null;
  authorName: string | null;
  providerName: string | null;
  daysSinceService: number;
  overdue: boolean;
  signedAt?: string | null;
  detail?: string | null;
}

// Matches DashboardDeadlineDto.
export interface DashboardDeadline {
  patientId: string;
  patientName: string;
  medicalRecordNumber: string;
  noteType: number;
  dueDate: string | null;
  overdue: boolean;
  detail: string;
  planOfCareId?: string | null;
}

export interface DocumentationDashboard {
  today: string;
  from: string | null;
  to: string;
  counts: Record<
    | "todaysVisits"
    | "notStarted"
    | "drafts"
    | "readyToSign"
    | "awaitingCosign"
    | "returned"
    | "overdue"
    | "progressNotesDue"
    | "reevaluationsDue"
    | "expiringPlans"
    | "recentlySigned",
    number
  >;
  todaysSchedule: DashboardItem[];
  notStarted: DashboardItem[];
  drafts: DashboardItem[];
  readyToSign: DashboardItem[];
  awaitingCosign: DashboardItem[];
  returned: DashboardItem[];
  overdue: DashboardItem[];
  progressNotesDue: DashboardDeadline[];
  reevaluationsDue: DashboardDeadline[];
  expiringPlans: DashboardDeadline[];
  recentlySigned: DashboardItem[];
}

export interface DashboardFilters {
  providerId: string;
  patientId: string;
  noteType: string;
  status: string;
  from: string;
  to: string;
}

export const emptyFilters: DashboardFilters = {
  providerId: "",
  patientId: "",
  noteType: "",
  status: "",
  from: "",
  to: "",
};

export function fetchDocumentationDashboard(filters: DashboardFilters) {
  const q = new URLSearchParams();
  for (const [key, value] of Object.entries(filters))
    if (value) q.set(key, value);
  return apiRequest<DocumentationDashboard>(
    `/api/v1/documentation/dashboard?${q.toString()}`,
  );
}
