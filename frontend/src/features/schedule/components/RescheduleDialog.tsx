import { useEffect, useState } from "react";
import { formatTime, fromDateKey, toDateKey, toWallClock, wallClockToIso } from "../time";
import type { RescheduleRequest, ScheduleAppointment, ScheduleSettings } from "../types";
import { MoveAppointmentDialog } from "./MoveAppointmentDialog";

interface Props {
  appointment: ScheduleAppointment;
  settings: ScheduleSettings;
  timezone: string;
  onClose: () => void;
}

const durations = [15, 30, 45, 60, 90];

const pad = (n: number) => String(n).padStart(2, "0");

/** "Reschedule": CURRENT slot on top, NEW provider/location/date/time/
 * duration below. Continue hands off to MoveAppointmentDialog, the same
 * confirm/conflict/override flow a drag-and-drop move uses. */
export function RescheduleDialog({ appointment: a, settings, timezone, onClose }: Props) {
  const start = toWallClock(a.startsAt, timezone);
  const end = toWallClock(a.endsAt, timezone);

  const [providerId, setProviderId] = useState(a.providerId ?? "");
  const [locationId, setLocationId] = useState(a.locationId ?? "");
  const [date, setDate] = useState(toDateKey(start));
  const [time, setTime] = useState(`${pad(start.getHours())}:${pad(start.getMinutes())}`);
  const [duration, setDuration] = useState(a.durationMinutes);
  const [customDuration, setCustomDuration] = useState(!durations.includes(a.durationMinutes));
  const [request, setRequest] = useState<RescheduleRequest | null>(null);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && !request && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose, request]);

  // Only providers who can take appointments; those at the chosen location first.
  const providers = settings.providers
    .filter((p) => p.isActive && p.hasLogin)
    .sort((x, y) => Number(y.locationIds.includes(locationId)) - Number(x.locationIds.includes(locationId)));
  const valid = !!date && /^\d{2}:\d{2}$/.test(time) && duration >= 5 && duration <= 480;

  function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!valid) return;
    const [h, m] = time.split(":").map(Number);
    const day = fromDateKey(date);
    const newStart = new Date(day.getFullYear(), day.getMonth(), day.getDate(), h, m);
    const newEnd = new Date(newStart.getTime() + duration * 60000);
    setRequest({
      startsAt: wallClockToIso(newStart, timezone),
      endsAt: wallClockToIso(newEnd, timezone),
      providerId: providerId && providerId !== a.providerId ? providerId : null,
      locationDetailId: locationId && locationId !== a.locationId ? locationId : null,
      roomId: null,
    });
  }

  if (request) {
    // Closing the confirmation returns to the form, so "Choose another time"
    // keeps what was entered instead of starting over.
    return (
      <MoveAppointmentDialog appointment={a} request={request} timezone={timezone} onClose={() => setRequest(null)} onMoved={onClose} />
    );
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <form
        role="dialog"
        aria-modal="true"
        aria-labelledby="reschedule-title"
        className="modal-panel max-w-lg"
        onClick={(e) => e.stopPropagation()}
        onSubmit={submit}
      >
        <h2 id="reschedule-title" className="mb-1 text-lg font-semibold text-text">Reschedule appointment</h2>
        <p className="mb-3 text-sm text-text-muted">
          Patient: <span className="font-medium text-text">{a.patientName}</span>
        </p>

        <div className="mb-4 rounded-lg border border-border bg-surface-muted p-3 text-sm">
          <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-text-subtle">Current</p>
          <p className="text-text">
            {a.providerName ?? "Unassigned"} · {start.toLocaleDateString("en-US", { weekday: "short", month: "short", day: "numeric" })} ·{" "}
            {formatTime(start)} – {formatTime(end)}
            {a.locationName ? ` · ${a.locationName}` : ""}
          </p>
        </div>

        <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-text-subtle">New</p>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <label>
            <span className="field-label">Provider</span>
            <select className="field-input" value={providerId} onChange={(e) => setProviderId(e.target.value)}>
              {!a.providerId && <option value="">Unassigned</option>}
              {providers.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                  {p.credentials ? `, ${p.credentials}` : ""}
                  {locationId && !p.locationIds.includes(locationId) ? " (not at this location)" : ""}
                </option>
              ))}
            </select>
          </label>
          <label>
            <span className="field-label">Location</span>
            <select className="field-input" value={locationId} onChange={(e) => setLocationId(e.target.value)}>
              {!a.locationId && <option value="">None</option>}
              {settings.locations.map((l) => (
                <option key={l.id} value={l.id}>{l.name}</option>
              ))}
            </select>
          </label>
          <label>
            <span className="field-label">Date</span>
            <input type="date" required className="field-input" value={date} onChange={(e) => setDate(e.target.value)} />
          </label>
          <label>
            <span className="field-label">Start time</span>
            <input type="time" required step={300} className="field-input" value={time} onChange={(e) => setTime(e.target.value)} />
          </label>
          <label>
            <span className="field-label">Duration</span>
            <select
              className="field-input"
              value={customDuration ? "custom" : String(duration)}
              onChange={(e) => {
                setCustomDuration(e.target.value === "custom");
                if (e.target.value !== "custom") setDuration(Number(e.target.value));
              }}
            >
              {durations.map((d) => (
                <option key={d} value={d}>{d} min</option>
              ))}
              <option value="custom">Custom…</option>
            </select>
          </label>
          {customDuration && (
            <label>
              <span className="field-label">Custom minutes</span>
              <input
                type="number"
                min={5}
                max={480}
                step={5}
                className="field-input"
                value={duration}
                onChange={(e) => setDuration(Number(e.target.value))}
              />
            </label>
          )}
        </div>

        <div className="mt-5 flex justify-end gap-2">
          <button type="button" className="btn-secondary" onClick={onClose}>Cancel</button>
          <button type="submit" className="btn-primary" disabled={!valid}>Check and continue</button>
        </div>
      </form>
    </div>
  );
}
