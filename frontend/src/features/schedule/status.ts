import { AppointmentKind, AppointmentStatus, ProviderDiscipline } from "./types";

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

/** Visit-type colors: an appointment's card is tinted by what kind of visit
 * it is (so an initial evaluation never looks like a follow-up); its status
 * is printed on the card instead. An appointment type with its own `color`
 * overrides these. Cancelled / no-show stay grey (statusEventClass). */
export const kindColors: Record<AppointmentKind, { label: string; accent: string; bg: string }> = {
  [AppointmentKind.Evaluation]: { label: "Initial evaluation", accent: "#7c3aed", bg: "#ede9fe" },
  [AppointmentKind.FollowUp]: { label: "Follow-up visit", accent: "#2563eb", bg: "#dbeafe" },
  [AppointmentKind.ReEvaluation]: { label: "Re-evaluation", accent: "#d97706", bg: "#fef3c7" },
  [AppointmentKind.Progress]: { label: "Progress note visit", accent: "#0891b2", bg: "#cffafe" },
  [AppointmentKind.Discharge]: { label: "Discharge", accent: "#059669", bg: "#d1fae5" },
  [AppointmentKind.Telehealth]: { label: "Telehealth", accent: "#db2777", bg: "#fce7f3" },
};

/** Cancelled and no-show appointments keep their grey, struck-through look. */
export const isVoided = (status: AppointmentStatus) =>
  status === AppointmentStatus.Cancelled || status === AppointmentStatus.NoShow;

/** Accent + tint for one appointment: its type's own color when set,
 * otherwise its visit kind's. */
export function appointmentColors(a: { kind: AppointmentKind; appointmentTypeColor: string | null }) {
  if (a.appointmentTypeColor) {
    return { accent: a.appointmentTypeColor, bg: `color-mix(in srgb, ${a.appointmentTypeColor} 18%, white)` };
  }
  return kindColors[a.kind];
}

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
