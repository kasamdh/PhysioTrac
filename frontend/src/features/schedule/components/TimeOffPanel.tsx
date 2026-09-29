import { useState } from "react";
import { Link } from "react-router-dom";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useToast } from "../../../components/Toast";
import { cancelTimeOff, createTimeOff } from "../api";
import { formatTime, fromDateKey, toDateKey, toWallClock, todayKey, wallClockToIso } from "../time";
import {
  TimeOffReason, TimeOffReasonLabels, Weekday, WeekdayShort,
  type CreateTimeOffResult, type ProviderSchedule,
} from "../types";

interface Props {
  schedule: ProviderSchedule;
}

const reasons = [0, 1, 2, 3, 4, 5, 6] as TimeOffReason[];
const allDays = [0, 1, 2, 3, 4, 5, 6] as Weekday[];
const weekdays = [0, 1, 2, 3, 4] as Weekday[];

/** Upcoming time off for one provider, and a form to add more -- a single
 * block or a repeating one (e.g. lunch every weekday). Existing visits that
 * land inside new time off aren't moved; they're listed so staff can
 * reschedule each one. */
export function TimeOffPanel({ schedule }: Props) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const tz = schedule.timezone;

  const [reason, setReason] = useState<TimeOffReason>(TimeOffReason.Lunch);
  const [date, setDate] = useState(todayKey(tz));
  const [start, setStart] = useState("12:00");
  const [end, setEnd] = useState("13:00");
  const [notes, setNotes] = useState("");
  const [repeat, setRepeat] = useState(false);
  const [repeatUntil, setRepeatUntil] = useState("");
  const [repeatDays, setRepeatDays] = useState<Weekday[]>(weekdays);
  const [affected, setAffected] = useState<CreateTimeOffResult["affectedAppointments"] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [confirmCancel, setConfirmCancel] = useState<string | null>(null);

  const refresh = () => queryClient.invalidateQueries({ queryKey: ["schedule"] });

  const valid = !!date && start < end && (!repeat || (repeatUntil >= date && repeatDays.length > 0));

  const add = useMutation({
    mutationFn: () => {
      const day = fromDateKey(date);
      const at = (hhmm: string) => {
        const [h, m] = hhmm.split(":").map(Number);
        return wallClockToIso(new Date(day.getFullYear(), day.getMonth(), day.getDate(), h, m), tz);
      };
      return createTimeOff(schedule.providerId, {
        startsAt: at(start),
        endsAt: at(end),
        reason,
        notes: notes.trim() || null,
        repeatUntil: repeat ? repeatUntil : null,
        repeatDays: repeat ? repeatDays : null,
      });
    },
    onSuccess: async (result) => {
      setAffected(result.affectedAppointments);
      setNotes("");
      showToast(`${TimeOffReasonLabels[reason]} added (${result.created.length} block${result.created.length === 1 ? "" : "s"}).`);
      await refresh();
    },
    onError: (e: Error) => setError(e.message),
  });

  const cancel = useMutation({
    mutationFn: (id: string) => cancelTimeOff(schedule.providerId, id),
    onSuccess: async () => {
      setConfirmCancel(null);
      showToast("Time off removed.");
      await refresh();
    },
    onError: (e: Error) => showToast(e.message),
  });

  return (
    <div className="space-y-4">
      {schedule.canManage && (
        <form
          className="space-y-3 rounded-lg border border-border p-3"
          onSubmit={(e) => {
            e.preventDefault();
            setError(null);
            setAffected(null);
            if (valid) add.mutate();
          }}
        >
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <label>
              <span className="field-label">Reason</span>
              <select className="field-input" value={reason} onChange={(e) => setReason(Number(e.target.value) as TimeOffReason)}>
                {reasons.map((r) => (
                  <option key={r} value={r}>{TimeOffReasonLabels[r]}</option>
                ))}
              </select>
            </label>
            <label>
              <span className="field-label">Date</span>
              <input type="date" className="field-input" value={date} onChange={(e) => setDate(e.target.value)} />
            </label>
            <label>
              <span className="field-label">From</span>
              <input type="time" className="field-input" value={start} onChange={(e) => setStart(e.target.value)} />
            </label>
            <label>
              <span className="field-label">To</span>
              <input type="time" className="field-input" value={end} onChange={(e) => setEnd(e.target.value)} />
            </label>
          </div>
          <label className="block">
            <span className="field-label">Notes (optional)</span>
            <input className="field-input" maxLength={500} value={notes} onChange={(e) => setNotes(e.target.value)} />
          </label>
          <div className="flex flex-wrap items-center gap-3 text-sm">
            <label className="flex items-center gap-2">
              <input type="checkbox" checked={repeat} onChange={(e) => setRepeat(e.target.checked)} />
              Repeat on
            </label>
            {repeat && (
              <>
                <div className="flex flex-wrap gap-1" role="group" aria-label="Repeat on days">
                  {allDays.map((d) => (
                    <label
                      key={d}
                      className={`cursor-pointer rounded-md border px-2 py-1 text-xs font-medium ${
                        repeatDays.includes(d) ? "border-primary bg-primary text-white" : "border-border text-text-muted"
                      }`}
                    >
                      <input
                        type="checkbox"
                        className="sr-only"
                        checked={repeatDays.includes(d)}
                        onChange={() => setRepeatDays((cur) => (cur.includes(d) ? cur.filter((x) => x !== d) : [...cur, d].sort()))}
                      />
                      {WeekdayShort[d]}
                    </label>
                  ))}
                </div>
                <label className="flex items-center gap-2">
                  until
                  <input
                    type="date"
                    aria-label="Repeat until"
                    min={date}
                    className="field-input w-auto py-1"
                    value={repeatUntil}
                    onChange={(e) => setRepeatUntil(e.target.value)}
                  />
                </label>
              </>
            )}
          </div>
          {start >= end && <p className="text-sm text-danger">"To" must be after "From".</p>}
          {error && <p className="alert-error">{error}</p>}
          <div className="flex justify-end">
            <button type="submit" className="btn-primary" disabled={!valid || add.isPending}>
              {add.isPending ? "Adding…" : "Add time off"}
            </button>
          </div>
        </form>
      )}

      {affected && affected.length > 0 && (
        <div className="rounded-md bg-warning-light p-3 text-sm text-warning" role="alert">
          <p className="mb-1 font-semibold">
            {affected.length === 1
              ? "1 existing appointment falls inside this time off and still needs rescheduling:"
              : `${affected.length} existing appointments fall inside this time off and still need rescheduling:`}
          </p>
          <ul className="space-y-0.5">
            {affected.map((a) => {
              const s = toWallClock(a.startsAt, tz);
              return (
                <li key={a.id}>
                  <Link className="underline hover:no-underline" to={`/schedule?view=day&date=${toDateKey(s)}&location=all`}>
                    {s.toLocaleDateString("en-US", { weekday: "short", month: "short", day: "numeric" })} {formatTime(s)} · {a.patientName}
                  </Link>
                </li>
              );
            })}
          </ul>
        </div>
      )}

      <div>
        <h3 className="mb-2 text-sm font-semibold text-text">Upcoming time off</h3>
        {schedule.timeOff.length === 0 ? (
          <p className="text-sm text-text-muted">None scheduled.</p>
        ) : (
          <ul className="max-h-96 divide-y divide-border overflow-y-auto rounded-lg border border-border text-sm">
            {schedule.timeOff.map((t) => {
              const s = toWallClock(t.startsAt, tz);
              const e = toWallClock(t.endsAt, tz);
              const sameDay = toDateKey(s) === toDateKey(e);
              return (
                <li key={t.id} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2">
                  <span>
                    <span className="font-medium text-text">{TimeOffReasonLabels[t.reason]}</span>
                    <span className="ml-2 text-text-muted">
                      {s.toLocaleDateString("en-US", { weekday: "short", month: "short", day: "numeric" })} {formatTime(s)} –{" "}
                      {sameDay ? formatTime(e) : `${e.toLocaleDateString("en-US", { month: "short", day: "numeric" })} ${formatTime(e)}`}
                    </span>
                    {t.notes && <span className="ml-2 text-text-subtle">· {t.notes}</span>}
                  </span>
                  {schedule.canManage &&
                    (confirmCancel === t.id ? (
                      <span className="flex gap-1">
                        <button type="button" className="btn-secondary px-2 py-1 text-xs hover:border-danger hover:text-danger" onClick={() => cancel.mutate(t.id)}>
                          Yes, remove
                        </button>
                        <button type="button" className="btn-secondary px-2 py-1 text-xs" onClick={() => setConfirmCancel(null)}>
                          Keep
                        </button>
                      </span>
                    ) : (
                      <button type="button" className="btn-secondary px-2 py-1 text-xs" onClick={() => setConfirmCancel(t.id)}>
                        Remove
                      </button>
                    ))}
                </li>
              );
            })}
          </ul>
        )}
      </div>
    </div>
  );
}
