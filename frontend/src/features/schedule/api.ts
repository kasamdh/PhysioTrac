import { apiRequest } from "../../lib/apiClient";
import type {
  CreateAppointmentRequest, CreateSeriesRequest, CreateTimeOffRequest, CreateTimeOffResult, ProviderSchedule, WeeklyHoursWindowRequest, MoveCheck, PagedScheduleAppointments, ScheduleDayCount, QuickPatientRequest, RescheduleRequest, ScheduleDay, ScheduleFilters,
  SchedulePatient, ScheduleRange, ScheduleSettings, SeriesPreview,
} from "./types";

function query(params: Record<string, string | undefined>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value) search.set(key, value);
  }
  const text = search.toString();
  return text ? `?${text}` : "";
}

export function fetchScheduleSettings(): Promise<ScheduleSettings> {
  return apiRequest<ScheduleSettings>("/api/v1/schedule/settings");
}

export function fetchScheduleDay(date: string, locationId: string, providerId: string): Promise<ScheduleDay> {
  return apiRequest<ScheduleDay>(`/api/v1/schedule/day${query({ date, locationId, providerId })}`);
}

/** from/to are ISO instants (already converted from clinic wall-clock time). */
export function fetchScheduleRange(from: string, to: string, filters: ScheduleFilters): Promise<ScheduleRange> {
  return apiRequest<ScheduleRange>(
    `/api/v1/schedule/range${query({
      from,
      to,
      locationId: filters.locationId,
      providerId: filters.providerId,
      status: filters.status,
      appointmentTypeId: filters.appointmentTypeId,
      patient: filters.patient,
    })}`,
  );
}

export type AppointmentAction = "confirm" | "check-in" | "start-visit" | "complete" | "no-show" | "cancel";

export function transitionAppointment(appointmentId: string, action: AppointmentAction): Promise<unknown> {
  return apiRequest(`/api/v1/appointments/${appointmentId}/${action}`, { method: "PATCH" });
}

/** Dry run: every rule the move would break, nothing saved. */
export function validateMove(appointmentId: string, request: RescheduleRequest): Promise<MoveCheck> {
  return apiRequest<MoveCheck>(`/api/v1/appointments/${appointmentId}/validate-move`, { method: "POST", body: request });
}

/** The real move -- re-validated server-side regardless of any dry run. */
export function rescheduleAppointment(appointmentId: string, request: RescheduleRequest): Promise<unknown> {
  return apiRequest(`/api/v1/appointments/${appointmentId}/reschedule`, { method: "PATCH", body: request });
}

/** Name, MRN, phone, or date of birth -- the caller's own organization only. */
export function searchPatients(term: string, signal?: AbortSignal): Promise<SchedulePatient[]> {
  return apiRequest<SchedulePatient[]>(`/api/v1/schedule/patients${query({ q: term })}`, { signal });
}

export function createPatient(request: QuickPatientRequest): Promise<SchedulePatient> {
  return apiRequest<SchedulePatient>("/api/v1/patients", { method: "POST", body: request });
}

export function createAppointment(request: CreateAppointmentRequest): Promise<{ id: string }> {
  return apiRequest<{ id: string }>("/api/v1/appointments", { method: "POST", body: request });
}

export function previewSeries(request: CreateSeriesRequest): Promise<SeriesPreview> {
  return apiRequest<SeriesPreview>("/api/v1/appointments/series/preview", { method: "POST", body: request });
}

export function createSeries(request: CreateSeriesRequest): Promise<unknown> {
  return apiRequest("/api/v1/appointments/series", { method: "POST", body: request });
}

/** Year view: non-cancelled appointments per clinic-local day. */
export function fetchScheduleCounts(from: string, to: string, locationId: string, providerId: string): Promise<ScheduleDayCount[]> {
  return apiRequest<ScheduleDayCount[]>(`/api/v1/schedule/counts${query({ from, to, locationId, providerId })}`);
}

/** List view, one page at a time. from/to are ISO instants. */
export function fetchScheduleList(
  from: string, to: string, filters: ScheduleFilters, page: number, pageSize: number,
): Promise<PagedScheduleAppointments> {
  return apiRequest<PagedScheduleAppointments>(
    `/api/v1/schedule/list${query({
      from,
      to,
      locationId: filters.locationId,
      providerId: filters.providerId,
      status: filters.status,
      appointmentTypeId: filters.appointmentTypeId,
      patient: filters.patient,
      page: String(page),
      pageSize: String(pageSize),
    })}`,
  );
}

export function fetchProviderSchedule(providerId: string): Promise<ProviderSchedule> {
  return apiRequest<ProviderSchedule>(`/api/v1/providers/${providerId}/schedule`);
}

/** Replaces the provider's whole weekly pattern. */
export function replaceWeeklyHours(providerId: string, windows: WeeklyHoursWindowRequest[]): Promise<ProviderSchedule> {
  return apiRequest<ProviderSchedule>(`/api/v1/providers/${providerId}/weekly-hours`, { method: "PUT", body: { windows } });
}

export function createTimeOff(providerId: string, request: CreateTimeOffRequest): Promise<CreateTimeOffResult> {
  return apiRequest<CreateTimeOffResult>(`/api/v1/providers/${providerId}/time-off`, { method: "POST", body: request });
}

export function cancelTimeOff(providerId: string, timeOffId: string): Promise<void> {
  return apiRequest<void>(`/api/v1/providers/${providerId}/time-off/${timeOffId}`, { method: "DELETE" });
}
