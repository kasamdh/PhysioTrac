// Matches PhysioTrac.Api.Controllers.AuditCategories.
export const LOG_CATEGORIES: { value: string; label: string }[] = [
  { value: "sign-in", label: "Sign-in / sign-out" },
  { value: "pages", label: "Screens opened" },
  { value: "patients", label: "Patients" },
  { value: "schedule", label: "Schedule" },
  { value: "notes", label: "Clinical notes" },
  { value: "messages", label: "Messages" },
  { value: "billing", label: "Billing" },
  { value: "admin", label: "Administration" },
  { value: "other", label: "Other" },
];
export const categoryLabel = (c: string) => LOG_CATEGORIES.find((x) => x.value === c)?.label ?? c;

// Matches PhysioTrac.Api.Controllers.AuditLogRowDto.
export interface AuditLogRow {
  id: string;
  at: string;
  userId: string | null;
  userName: string;
  userLogin: string | null;
  action: string;
  category: string;
  description: string;
  objectType: string;
  objectId: string | null;
  patientId: string | null;
  patientName: string | null;
  patientMrn: string | null;
  ipAddress: string | null;
  metadataJson: string;
}

export interface AuditLogPage {
  items: AuditLogRow[];
  total: number;
  page: number;
  pageSize: number;
  from: string;
  to: string;
  timezone: string;
  users: { id: string; name: string; userName: string | null }[];
}

export interface AuditLogFilters {
  from?: string;
  to?: string;
  userId?: string;
  category?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}
