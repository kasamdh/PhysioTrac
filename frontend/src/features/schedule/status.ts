import { AppointmentStatus, ProviderDiscipline } from "./types";

/** Badge classes per status, from the shared brand tokens in index.css. */
export const statusBadgeClass: Record<AppointmentStatus, string> = {
  [AppointmentStatus.Scheduled]: "bg-info-light text-info",
  [AppointmentStatus.Confirmed]: "bg-primary-light text-primary-deep",
  [AppointmentStatus.CheckedIn]: "bg-warning-light text-warning",
  [AppointmentStatus.InProgress]: "bg-primary text-white",
  [AppointmentStatus.Completed]: "bg-success-light text-success",
  [AppointmentStatus.Cancelled]: "bg-danger-light text-danger",
  [AppointmentStatus.NoShow]: "bg-danger-light text-danger",
};

/** Calendar card class per status -- see features/schedule/schedule.css. */
export const statusEventClass: Record<AppointmentStatus, string> = {
  [AppointmentStatus.Scheduled]: "appt-scheduled",
  [AppointmentStatus.Confirmed]: "appt-confirmed",
  [AppointmentStatus.CheckedIn]: "appt-checked-in",
  [AppointmentStatus.InProgress]: "appt-in-progress",
  [AppointmentStatus.Completed]: "appt-completed",
  [AppointmentStatus.Cancelled]: "appt-cancelled",
  [AppointmentStatus.NoShow]: "appt-no-show",
};

export const disciplineLabel: Record<ProviderDiscipline, string | null> = {
  [ProviderDiscipline.PT]: "PT",
  [ProviderDiscipline.PTA]: "PTA",
  [ProviderDiscipline.Other]: null,
};

/** Mirrors AppointmentService's reschedule rule: anything not yet started
 * or finished can still be moved. */
export function isReschedulable(status: AppointmentStatus): boolean {
  return status === AppointmentStatus.Scheduled || status === AppointmentStatus.Confirmed || status === AppointmentStatus.CheckedIn;
}
