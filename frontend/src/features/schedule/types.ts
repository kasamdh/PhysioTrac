// Mirror PhysioTrac.Domain.Enums / PhysioTrac.Application.Scheduling. The Api
// serializes enums by ordinal, so the numeric values matter. `as const`
// objects rather than `enum` -- see the note in features/auth/types.ts.

export const AppointmentStatus = {
  Scheduled: 0,
  Confirmed: 1,
  CheckedIn: 2,
  Completed: 3,
  Cancelled: 4,
  NoShow: 5,
  InProgress: 6,
} as const;
export type AppointmentStatus = (typeof AppointmentStatus)[keyof typeof AppointmentStatus];

export const AppointmentStatusLabels: Record<AppointmentStatus, string> = {
  [AppointmentStatus.Scheduled]: "Scheduled",
  [AppointmentStatus.Confirmed]: "Confirmed",
  [AppointmentStatus.CheckedIn]: "Checked in",
  [AppointmentStatus.Completed]: "Completed",
  [AppointmentStatus.Cancelled]: "Cancelled",
  [AppointmentStatus.NoShow]: "No show",
  [AppointmentStatus.InProgress]: "In progress",
};

export const AppointmentKind = {
  Evaluation: 0,
  FollowUp: 1,
  Progress: 2,
  Discharge: 3,
  Telehealth: 4,
  ReEvaluation: 5,
} as const;
export type AppointmentKind = (typeof AppointmentKind)[keyof typeof AppointmentKind];

export const ProviderDiscipline = {
  Other: 0,
  PT: 1,
  PTA: 2,
} as const;
export type ProviderDiscipline = (typeof ProviderDiscipline)[keyof typeof ProviderDiscipline];

export interface ScheduleLocation {
  id: string;
  name: string;
  timezone: string;
  state: string | null;
}

export interface ScheduleProvider {
  id: string;
  name: string;
  credentials: string | null;
  discipline: ProviderDiscipline;
  isActive: boolean;
  hasLogin: boolean;
  locationIds: string[];
  /** The clinician's login -- the therapistId a booking for them carries. */
  userId: string | null;
}

export interface ScheduleAppointmentType {
  id: string;
  name: string;
  defaultDurationMinutes: number;
  color: string | null;
  defaultKind: AppointmentKind | null;
}

export interface ScheduleSettings {
  timezone: string;
  slotMinutes: number;
  canCreate: boolean;
  canReschedule: boolean;
  canOverride: boolean;
  allowDoubleBookOverride: boolean;
  canManageAvailability: boolean;
  locations: ScheduleLocation[];
  providers: ScheduleProvider[];
  appointmentTypes: ScheduleAppointmentType[];
}

export interface ScheduleAppointment {
  id: string;
  confirmationNumber: string;
  patientId: string;
  patientName: string;
  medicalRecordNumber: string;
  patientDateOfBirth: string;
  patientPhone: string | null;
  therapistId: string;
  providerId: string | null;
  providerName: string | null;
  providerCredentials: string | null;
  providerDiscipline: ProviderDiscipline | null;
  locationId: string | null;
  locationName: string | null;
  appointmentTypeId: string | null;
  appointmentTypeName: string | null;
  appointmentTypeColor: string | null;
  kind: AppointmentKind;
  status: AppointmentStatus;
  startsAt: string;
  endsAt: string;
  durationMinutes: number;
  isHomeVisit: boolean;
  reasonForVisit: string | null;
  seriesId: string | null;
}

export interface ScheduleRange {
  timezone: string;
  appointments: ScheduleAppointment[];
}

export interface TimeWindow {
  start: string; // "HH:mm", clinic-local
  end: string;
  locationId: string | null;
  locationName: string | null;
}

export interface ScheduleBlock {
  startsAt: string;
  endsAt: string;
  kind: "TimeOff" | "Closure";
  reason: string;
}

export interface ProviderDaySummary {
  appointments: number;
  scheduled: number;
  checkedIn: number;
  inProgress: number;
  completed: number;
  cancelled: number;
  noShow: number;
  remaining: number;
  workingMinutes: number;
  bookedMinutes: number;
  blockedMinutes: number;
  availableMinutes: number;
  utilizationPercent: number | null;
  firstAppointmentAt: string | null;
  lastAppointmentEndsAt: string | null;
  currentAppointmentId: string | null;
  nextAppointmentId: string | null;
}

export interface ProviderDayColumn {
  provider: ScheduleProvider;
  workingHours: TimeWindow[];
  blocks: ScheduleBlock[];
  summary: ProviderDaySummary;
}

export interface ScheduleDay {
  date: string;
  timezone: string;
  slotMinutes: number;
  providers: ProviderDayColumn[];
  appointments: ScheduleAppointment[];
}

export type ScheduleView = "day" | "week" | "month" | "year" | "list";

/** How much the List view covers around the selected date. */
export type ListRange = "day" | "week" | "month";

export interface ScheduleDayCount {
  date: string; // YYYY-MM-DD, clinic-local
  count: number;
}

export interface PagedScheduleAppointments {
  items: ScheduleAppointment[];
  total: number;
  page: number;
  pageSize: number;
}

export interface ScheduleFilters {
  locationId: string;
  providerId: string;
  status: string; // "" or an AppointmentStatus ordinal as text
  appointmentTypeId: string;
  patient: string;
}

// ---- Moves / rescheduling (PhysioTrac.Application.Scheduling.SchedulingRules) ----

export interface SchedulingViolation {
  code: string;
  message: string;
  overridable: boolean;
}

export interface AppointmentSlotSummary {
  providerId: string | null;
  providerName: string | null;
  locationId: string | null;
  locationName: string | null;
  startsAt: string;
  endsAt: string;
}

export interface MoveCheck {
  appointmentId: string;
  patientName: string;
  from: AppointmentSlotSummary;
  to: AppointmentSlotSummary;
  isValid: boolean;
  canOverride: boolean;
  violations: SchedulingViolation[];
}

/** Body for validate-move and reschedule. Null ids mean "keep as is". */
export interface RescheduleRequest {
  startsAt: string;
  endsAt: string;
  providerId: string | null;
  locationDetailId: string | null;
  roomId: string | null;
  overrideReason?: string | null;
}

/** 409 body from reschedule/create when scheduling rules are broken. */
export interface SchedulingConflictBody {
  detail: string;
  violations?: SchedulingViolation[];
  canOverride?: boolean;
}

// ---- Booking (PhysioTrac.Application.Scheduling.SchedulingContracts) ----

/** PhysioTrac.Domain.Enums.Weekday -- Monday = 0, unlike JS getDay(). */
export const Weekday = { Monday: 0, Tuesday: 1, Wednesday: 2, Thursday: 3, Friday: 4, Saturday: 5, Sunday: 6 } as const;
export type Weekday = (typeof Weekday)[keyof typeof Weekday];
export const WeekdayShort: Record<Weekday, string> = { 0: "Mon", 1: "Tue", 2: "Wed", 3: "Thu", 4: "Fri", 5: "Sat", 6: "Sun" };

export interface SchedulePatient {
  id: string;
  fullName: string;
  medicalRecordNumber: string;
  dateOfBirth: string;
  phone: string | null;
  email: string | null;
  primaryLocationId: string | null;
}

export interface CreateAppointmentRequest {
  patientId: string;
  therapistId: string;
  providerId: string | null;
  locationDetailId: string | null;
  roomId: string | null;
  appointmentTypeId: string | null;
  kind: AppointmentKind;
  startsAt: string;
  endsAt: string;
  isHomeVisit: boolean;
  reasonForVisit: string | null;
  overrideReason?: string | null;
  notes?: string | null;
}

export interface CreateSeriesRequest {
  patientId: string;
  therapistId: string;
  providerId: string | null;
  locationDetailId: string | null;
  roomId: string | null;
  appointmentTypeId: string | null;
  kind: AppointmentKind;
  firstStartsAt: string;
  firstEndsAt: string;
  intervalWeeks: number;
  occurrenceCount: number;
  reasonForVisit: string | null;
  notes?: string | null;
  daysOfWeek?: Weekday[] | null;
  endDate?: string | null;
  skipConflicting?: boolean;
}

export interface SeriesOccurrencePreview {
  startsAt: string;
  endsAt: string;
  violations: SchedulingViolation[];
}

export interface SeriesPreview {
  bookable: number;
  conflicting: number;
  occurrences: SeriesOccurrencePreview[];
}

/** POST /api/v1/patients body (PhysioTrac.Application.Patients.CreatePatientRequest). */
export interface QuickPatientRequest {
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  phone: string | null;
  email: string | null;
  address: null;
  emergencyContact: null;
  preferredLanguage: null;
  assignedTherapistId: string | null;
  primaryLocationId: string | null;
  primaryCareProviderId: null;
  referringProviderId: null;
}

// ---- Provider hours & time off (PhysioTrac.Application.Scheduling.IProviderAvailabilityService) ----

export const TimeOffReason = { Vacation: 0, Personal: 1, Conference: 2, Lunch: 3, Meeting: 4, Admin: 5, Other: 6 } as const;
export type TimeOffReason = (typeof TimeOffReason)[keyof typeof TimeOffReason];
export const TimeOffReasonLabels: Record<TimeOffReason, string> = {
  0: "Vacation", 1: "Personal", 2: "Conference", 3: "Lunch", 4: "Meeting", 5: "Admin", 6: "Other",
};

export interface WeeklyHoursWindow {
  id: string;
  dayOfWeek: Weekday;
  locationId: string;
  locationName: string;
  start: string; // HH:mm
  end: string;
  effectiveFrom: string | null;
  effectiveUntil: string | null;
}

export interface WeeklyHoursWindowRequest {
  dayOfWeek: Weekday;
  locationId: string;
  start: string;
  end: string;
  effectiveFrom?: string | null;
  effectiveUntil?: string | null;
}

export interface TimeOff {
  id: string;
  startsAt: string;
  endsAt: string;
  reason: TimeOffReason;
  notes: string | null;
  locationId: string | null;
}

export interface ProviderSchedule {
  providerId: string;
  providerName: string;
  credentials: string | null;
  timezone: string;
  canManage: boolean;
  locations: ScheduleLocation[];
  weeklyHours: WeeklyHoursWindow[];
  timeOff: TimeOff[];
}

export interface CreateTimeOffRequest {
  startsAt: string;
  endsAt: string;
  reason: TimeOffReason;
  notes: string | null;
  repeatUntil?: string | null;
  repeatDays?: Weekday[] | null;
}

export interface CreateTimeOffResult {
  created: TimeOff[];
  affectedAppointments: { id: string; patientName: string; startsAt: string; endsAt: string }[];
}
