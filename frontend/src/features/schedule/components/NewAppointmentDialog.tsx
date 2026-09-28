import { useEffect, useMemo, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useToast } from "../../../components/Toast";
import { ApiError } from "../../../lib/apiClient";
import { createAppointment, createSeries, previewSeries, transitionAppointment } from "../api";
import { formatTime, fromDateKey, toWallClock, wallClockToIso } from "../time";
import {
  AppointmentKind, Weekday, WeekdayShort,
  type CreateAppointmentRequest, type CreateSeriesRequest, type SchedulePatient, type ScheduleSettings,
  type SchedulingConflictBody, type SchedulingViolation, type SeriesPreview,
} from "../types";
import { PatientPicker } from "./PatientPicker";
import { QuickPatientForm } from "./QuickPatientForm";

export interface NewAppointmentDefaults {
  date: string; // YYYY-MM-DD, clinic-local
  time?: string; // HH:mm
  durationMinutes?: number;
  providerId?: string;
  locationId?: string;
}

interface Props {
  settings: ScheduleSettings;
  timezone: string;
  defaults: NewAppointmentDefaults;
  onClose: () => void;
}

type Repeat = "never" | "weekly" | "2x" | "3x" | "custom";
const durations = [15, 30, 45, 60, 90];
const allDays = [Weekday.Monday, Weekday.Tuesday, Weekday.Wednesday, Weekday.Thursday, Weekday.Friday, Weekday.Saturday, Weekday.Sunday];

/** Weekday (Mon = 0) of a "YYYY-MM-DD" date. */
function weekdayOf(dateKey: string): Weekday {
  return ((fromDateKey(dateKey).getDay() + 6) % 7) as Weekday;
}

/** Common PT patterns on weekdays, always including the first visit's own
 * day: 2x pairs it with the day two apart (Mon/Wed, Tue/Thu, Wed/Fri...),
 * 3x is Mon/Wed/Fri or Tue/Thu/Fri. Weekend starts just repeat that day. */
function presetDays(repeat: Repeat, dateKey: string): Weekday[] {
  const first = weekdayOf(dateKey);
  if (repeat === "weekly" || repeat === "never" || repeat === "custom" || first > Weekday.Friday) return [first];
  if (repeat === "2x") {
    const other = first + 2 <= Weekday.Friday ? first + 2 : first - 2;
    return [first, other as Weekday].sort();
  }
  return first === Weekday.Tuesday || first === Weekday.Thursday
    ? [Weekday.Tuesday, Weekday.Thursday, Weekday.Friday]
    : [Weekday.Monday, Weekday.Wednesday, Weekday.Friday];
}

/** "+ New appointment": pick or register the patient, then provider,
 * location, type, time, and optionally a recurring pattern. Every booking
 * is checked server-side; a single visit that breaks a rule shows the
 * conflict (with Override when allowed), and a series shows a preview --
 * "10 can be scheduled, 2 have conflicts" -- before anything is saved. */
export function NewAppointmentDialog({ settings, timezone, defaults, onClose }: Props) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();

  const bookable = settings.providers.filter((p) => p.isActive && p.userId);
  const [patient, setPatient] = useState<SchedulePatient | null>(null);
  const [registering, setRegistering] = useState<string | null>(null);
  const [providerId, setProviderId] = useState(defaults.providerId && bookable.some((p) => p.id === defaults.providerId) ? defaults.providerId : "");
  const provider = bookable.find((p) => p.id === providerId);
  const [locationId, setLocationId] = useState(defaults.locationId || provider?.locationIds[0] || settings.locations[0]?.id || "");
  const defaultType = settings.appointmentTypes.find((t) => t.defaultKind === AppointmentKind.FollowUp) ?? settings.appointmentTypes[0];
  const [typeId, setTypeId] = useState(defaultType?.id ?? "");
  const [date, setDate] = useState(defaults.date);
  const [time, setTime] = useState(defaults.time ?? "09:00");
  const [duration, setDuration] = useState(defaults.durationMinutes ?? defaultType?.defaultDurationMinutes ?? 30);
  const [customDuration, setCustomDuration] = useState(!durations.includes(duration));
  const [confirmed, setConfirmed] = useState(false);
  const [reason, setReason] = useState("");
  const [notes, setNotes] = useState("");
  const [repeat, setRepeat] = useState<Repeat>("never");
  const [days, setDays] = useState<Weekday[]>(presetDays("weekly", defaults.date));
  const [endMode, setEndMode] = useState<"count" | "date">("count");
  const [count, setCount] = useState(12);
  const [endDate, setEndDate] = useState("");

  const [conflict, setConflict] = useState<{ violations: SchedulingViolation[]; canOverride: boolean } | null>(null);
  const [overrideReason, setOverrideReason] = useState("");
  const [preview, setPreview] = useState<SeriesPreview | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  const type = settings.appointmentTypes.find((t) => t.id === typeId);
  const kind = type?.defaultKind ?? AppointmentKind.FollowUp;
  const recurring = repeat !== "never";

  const [h, m] = time.split(":").map(Number);
  const startWall = useMemo(() => {
    const d = fromDateKey(date);
    return new Date(d.getFullYear(), d.getMonth(), d.getDate(), h || 0, m || 0);
  }, [date, h, m]);
  const endWall = new Date(startWall.getTime() + duration * 60000);

  const providersHere = [...bookable].sort(
    (x, y) => Number(y.locationIds.includes(locationId)) - Number(x.locationIds.includes(locationId)),
  );

  const missing = [
    !patient && "patient",
    !provider && "provider",
    !locationId && "location",
    !typeId && "appointment type",
    !date && "date",
    !/^\d{2}:\d{2}$/.test(time) && "start time",
    !(duration >= 5 && duration <= 480) && "duration",
    recurring && days.length === 0 && "repeat days",
    recurring && endMode === "date" && !endDate && "end date",
    recurring && endMode === "count" && !(count >= 1 && count <= 52) && "number of visits (1–52)",
  ].filter(Boolean) as string[];

  function resetChecks() {
    setConflict(null);
    setPreview(null);
    setError(null);
  }

  const single = (): CreateAppointmentRequest => ({
    patientId: patient!.id,
    therapistId: provider!.userId!,
    providerId: provider!.id,
    locationDetailId: locationId,
    roomId: null,
    appointmentTypeId: typeId,
    kind,
    startsAt: wallClockToIso(startWall, timezone),
    endsAt: wallClockToIso(endWall, timezone),
    isHomeVisit: false,
    reasonForVisit: reason.trim() || null,
    notes: notes.trim() || null,
  });

  const series = (skipConflicting: boolean): CreateSeriesRequest => ({
    patientId: patient!.id,
    therapistId: provider!.userId!,
    providerId: provider!.id,
    locationDetailId: locationId,
    roomId: null,
    appointmentTypeId: typeId,
    kind,
    firstStartsAt: wallClockToIso(startWall, timezone),
    firstEndsAt: wallClockToIso(endWall, timezone),
    intervalWeeks: 1,
    occurrenceCount: endMode === "count" ? count : 0,
    reasonForVisit: reason.trim() || null,
    notes: notes.trim() || null,
    daysOfWeek: days,
    endDate: endMode === "date" ? endDate : null,
    skipConflicting,
  });

  async function done(message: string) {
    showToast(message);
    await queryClient.invalidateQueries({ queryKey: ["schedule"] });
    onClose();
  }

  const book = useMutation({
    mutationFn: async (override: string | null) => {
      const created = await createAppointment({ ...single(), overrideReason: override });
      if (!confirmed) return true;
      // The booking already exists at this point -- a failed confirm must
      // not look like a failed booking (a retry would double-book).
      try {
        await transitionAppointment(created.id, "confirm");
        return true;
      } catch {
        return false;
      }
    },
    onSuccess: (confirmOk, override) =>
      done(
        !confirmOk
          ? "Appointment booked, but it couldn't be marked confirmed -- confirm it from the schedule."
          : override ? "Appointment booked (override recorded)." : "Appointment booked.",
      ),
    onError: (e: Error) => {
      const body = e instanceof ApiError ? (e.payload as SchedulingConflictBody | undefined) : undefined;
      if (body?.violations?.length) setConflict({ violations: body.violations, canOverride: !!body.canOverride });
      else setError(e.message);
    },
  });

  const check = useMutation({
    mutationFn: () => previewSeries(series(false)),
    onSuccess: setPreview,
    onError: (e: Error) => setError(e.message),
  });

  const bookSeries = useMutation({
    mutationFn: () => createSeries(series(preview!.conflicting > 0)),
    onSuccess: () => done(`${preview!.bookable} appointment${preview!.bookable === 1 ? "" : "s"} booked.`),
    onError: (e: Error) => setError(e.message),
  });

  function submit(e: React.FormEvent) {
    e.preventDefault();
    resetChecks();
    if (missing.length > 0) return;
    if (recurring) check.mutate();
    else book.mutate(null);
  }

  const busy = book.isPending || check.isPending || bookSeries.isPending;

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="new-appt-title"
        className="modal-panel max-h-[92vh] max-w-2xl overflow-y-auto"
        onClick={(e) => e.stopPropagation()}
      >
        <h2 id="new-appt-title" className="mb-4 text-lg font-semibold text-text">New appointment</h2>

        {registering !== null ? (
          <QuickPatientForm
            initialName={registering}
            locations={settings.locations}
            defaultLocationId={locationId}
            assignedTherapistId={provider?.userId ?? null}
            onCancel={() => setRegistering(null)}
            onCreated={(p) => {
              setPatient(p);
              setRegistering(null);
              showToast(`${p.fullName} is ready to schedule.`);
            }}
          />
        ) : (
          <form onSubmit={submit} className="space-y-4">
            <div>
              <span className="field-label">Patient *</span>
              <PatientPicker
                selected={patient}
                onSelect={(p) => {
                  setPatient(p);
                  resetChecks();
                }}
                onCreateNew={(typed) => setRegistering(typed)}
              />
            </div>

            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <label>
                <span className="field-label">Provider (PT / PTA) *</span>
                <select className="field-input" value={providerId} onChange={(e) => { setProviderId(e.target.value); resetChecks(); }}>
                  <option value="">Select provider…</option>
                  {providersHere.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name}{p.credentials ? `, ${p.credentials}` : ""}
                      {locationId && !p.locationIds.includes(locationId) ? " (not at this location)" : ""}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                <span className="field-label">Location *</span>
                <select className="field-input" value={locationId} onChange={(e) => { setLocationId(e.target.value); resetChecks(); }}>
                  {settings.locations.map((l) => (
                    <option key={l.id} value={l.id}>{l.name}</option>
                  ))}
                </select>
              </label>
              <label>
                <span className="field-label">Appointment type *</span>
                <select
                  className="field-input"
                  value={typeId}
                  onChange={(e) => {
                    setTypeId(e.target.value);
                    const t = settings.appointmentTypes.find((x) => x.id === e.target.value);
                    if (t) {
                      setDuration(t.defaultDurationMinutes);
                      setCustomDuration(!durations.includes(t.defaultDurationMinutes));
                    }
                    resetChecks();
                  }}
                >
                  {settings.appointmentTypes.map((t) => (
                    <option key={t.id} value={t.id}>{t.name} ({t.defaultDurationMinutes} min)</option>
                  ))}
                </select>
              </label>
              {!recurring && (
                <label>
                  <span className="field-label">Status</span>
                  <select className="field-input" value={confirmed ? "confirmed" : "scheduled"} onChange={(e) => setConfirmed(e.target.value === "confirmed")}>
                    <option value="scheduled">Scheduled</option>
                    <option value="confirmed">Confirmed</option>
                  </select>
                </label>
              )}
              <label>
                <span className="field-label">Date *</span>
                <input
                  type="date"
                  required
                  className="field-input"
                  value={date}
                  onChange={(e) => {
                    setDate(e.target.value);
                    if (repeat !== "custom" && e.target.value) setDays(presetDays(repeat, e.target.value));
                    resetChecks();
                  }}
                />
              </label>
              <label>
                <span className="field-label">Start time *</span>
                <input type="time" required step={300} className="field-input" value={time} onChange={(e) => { setTime(e.target.value); resetChecks(); }} />
              </label>
              <label>
                <span className="field-label">Duration *</span>
                <select
                  className="field-input"
                  value={customDuration ? "custom" : String(duration)}
                  onChange={(e) => {
                    setCustomDuration(e.target.value === "custom");
                    if (e.target.value !== "custom") setDuration(Number(e.target.value));
                    resetChecks();
                  }}
                >
                  {durations.map((d) => (
                    <option key={d} value={d}>{d} min</option>
                  ))}
                  <option value="custom">Custom…</option>
                </select>
              </label>
              {customDuration ? (
                <label>
                  <span className="field-label">Custom minutes</span>
                  <input type="number" min={5} max={480} step={5} className="field-input" value={duration} onChange={(e) => { setDuration(Number(e.target.value)); resetChecks(); }} />
                </label>
              ) : (
                <div>
                  <span className="field-label">End time</span>
                  <p className="field-input bg-surface-muted text-text-muted">{formatTime(endWall)}</p>
                </div>
              )}
            </div>

            <label className="block">
              <span className="field-label">Reason for visit</span>
              <input className="field-input" maxLength={240} value={reason} onChange={(e) => setReason(e.target.value)} />
            </label>
            <label className="block">
              <span className="field-label">Notes (staff only)</span>
              <textarea className="field-input" rows={2} maxLength={2000} value={notes} onChange={(e) => setNotes(e.target.value)} />
            </label>

            <fieldset className="rounded-lg border border-border p-3">
              <legend className="px-1 text-sm font-medium text-text-muted">Recurring appointment</legend>
              <div className="flex flex-wrap items-center gap-2">
                <select
                  aria-label="Repeat"
                  className="field-input w-auto"
                  value={repeat}
                  onChange={(e) => {
                    const r = e.target.value as Repeat;
                    setRepeat(r);
                    if (r !== "custom") setDays(presetDays(r, date));
                    resetChecks();
                  }}
                >
                  <option value="never">Never</option>
                  <option value="weekly">Weekly</option>
                  <option value="2x">2x weekly</option>
                  <option value="3x">3x weekly</option>
                  <option value="custom">Custom days</option>
                </select>
                {recurring && (
                  <div className="flex flex-wrap gap-1" role="group" aria-label="Repeat on">
                    {allDays.map((d) => (
                      <label key={d} className={`cursor-pointer rounded-md border px-2 py-1 text-xs font-medium ${days.includes(d) ? "border-primary bg-primary text-white" : "border-border text-text-muted"}`}>
                        <input
                          type="checkbox"
                          className="sr-only"
                          checked={days.includes(d)}
                          onChange={() => {
                            setRepeat("custom");
                            setDays((cur) => (cur.includes(d) ? cur.filter((x) => x !== d) : [...cur, d].sort()));
                            resetChecks();
                          }}
                        />
                        {WeekdayShort[d]}
                      </label>
                    ))}
                  </div>
                )}
              </div>
              {recurring && (
                <div className="mt-3 flex flex-wrap items-center gap-3 text-sm">
                  <span className="flex items-center gap-2">
                    <input
                      type="radio"
                      name="end"
                      aria-label="End after a number of visits"
                      checked={endMode === "count"}
                      onChange={() => { setEndMode("count"); resetChecks(); }}
                    />
                    <input
                      type="number"
                      min={1}
                      max={52}
                      aria-label="Number of visits"
                      className="field-input w-20 py-1"
                      value={count}
                      onChange={(e) => { setCount(Number(e.target.value)); setEndMode("count"); resetChecks(); }}
                    />
                    visits
                  </span>
                  <span className="flex items-center gap-2">
                    <input
                      type="radio"
                      name="end"
                      aria-label="End on a date"
                      checked={endMode === "date"}
                      onChange={() => { setEndMode("date"); resetChecks(); }}
                    />
                    or until
                    <input
                      type="date"
                      min={date}
                      aria-label="End date"
                      className="field-input w-auto py-1"
                      value={endDate}
                      onChange={(e) => { setEndDate(e.target.value); setEndMode("date"); resetChecks(); }}
                    />
                  </span>
                </div>
              )}
            </fieldset>

            {missing.length > 0 && (patient || providerId) && (
              <p className="text-sm text-text-muted">Still needed: {missing.join(", ")}.</p>
            )}

            {conflict && (
              <div className="space-y-2" role="alert">
                <p className="font-semibold text-danger">Scheduling conflict</p>
                <ul className="space-y-1.5 text-sm">
                  {conflict.violations.map((v) => (
                    <li key={v.code + v.message} className="rounded-md bg-danger-light px-3 py-2 text-danger">{v.message}</li>
                  ))}
                </ul>
                {conflict.canOverride && (
                  <label className="block">
                    <span className="field-label">Reason for override (required, recorded in the audit log)</span>
                    <textarea className="field-input" rows={2} maxLength={500} value={overrideReason} onChange={(e) => setOverrideReason(e.target.value)} />
                  </label>
                )}
              </div>
            )}

            {preview && (
              <div className="space-y-2" role="status">
                <p className="text-sm font-semibold text-text">
                  {preview.bookable} appointment{preview.bookable === 1 ? "" : "s"} can be scheduled.
                  {preview.conflicting > 0 && (
                    <span className="text-danger"> {preview.conflicting} {preview.conflicting === 1 ? "has a conflict" : "have conflicts"} and will be skipped.</span>
                  )}
                </p>
                <ul className="max-h-48 space-y-1 overflow-y-auto rounded-md border border-border p-2 text-sm">
                  {preview.occurrences.map((o) => {
                    const start = toWallClock(o.startsAt, timezone);
                    const bad = o.violations.length > 0;
                    return (
                      <li key={o.startsAt} className={bad ? "text-danger" : "text-text"}>
                        {bad ? "✕" : "✓"} {start.toLocaleDateString("en-US", { weekday: "short", month: "short", day: "numeric" })} · {formatTime(start)}
                        {bad && <span className="block pl-4 text-xs">{o.violations[0].message}</span>}
                      </li>
                    );
                  })}
                </ul>
              </div>
            )}

            {error && <p className="alert-error">{error}</p>}

            <div className="flex flex-wrap justify-end gap-2 border-t border-border pt-4">
              <button type="button" className="btn-secondary" onClick={onClose}>Cancel</button>
              {conflict ? (
                <>
                  <button type="button" className="btn-secondary" onClick={() => setConflict(null)}>Choose another time</button>
                  {conflict.canOverride && (
                    <button type="button" className="btn-primary bg-danger hover:bg-danger" disabled={!overrideReason.trim() || busy} onClick={() => book.mutate(overrideReason.trim())}>
                      Override and book
                    </button>
                  )}
                </>
              ) : preview ? (
                <>
                  <button type="button" className="btn-secondary" onClick={() => setPreview(null)}>Back to edit</button>
                  <button type="button" className="btn-primary" disabled={preview.bookable === 0 || busy} onClick={() => bookSeries.mutate()}>
                    {bookSeries.isPending ? "Booking…" : `Book ${preview.bookable} appointment${preview.bookable === 1 ? "" : "s"}`}
                  </button>
                </>
              ) : (
                <button type="submit" className="btn-primary" disabled={missing.length > 0 || busy}>
                  {busy ? "Checking…" : recurring ? "Check dates" : "Book appointment"}
                </button>
              )}
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
