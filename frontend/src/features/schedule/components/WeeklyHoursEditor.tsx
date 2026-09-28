import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useToast } from "../../../components/Toast";
import { replaceWeeklyHours } from "../api";
import { formatHhmm } from "../time";
import { Weekday, type ProviderSchedule, type WeeklyHoursWindowRequest } from "../types";

interface Props {
  schedule: ProviderSchedule;
}

type Row = WeeklyHoursWindowRequest & { key: string };

const dayNames: Record<Weekday, string> = {
  0: "Monday", 1: "Tuesday", 2: "Wednesday", 3: "Thursday", 4: "Friday", 5: "Saturday", 6: "Sunday",
};
const days = [0, 1, 2, 3, 4, 5, 6] as Weekday[];
/** "HH:mm" plus some hours, capped at 23:59. */
function laterBy(hhmm: string, hours: number): string {
  const [h, m] = hhmm.split(":").map(Number);
  const total = Math.min(h * 60 + m + hours * 60, 23 * 60 + 59);
  return `${String(Math.floor(total / 60)).padStart(2, "0")}:${String(total % 60).padStart(2, "0")}`;
}

let keySeq = 0;
const nextKey = () => `w${++keySeq}`;

/** The provider's repeating week: one or more working windows per day, each
 * at one of their locations. Saved as a whole so a half-edited week never
 * reaches the booking rules. Read-only for roles that can't manage it. */
export function WeeklyHoursEditor({ schedule }: Props) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const fromServer = (): Row[] =>
    schedule.weeklyHours.map((w) => ({
      key: nextKey(),
      dayOfWeek: w.dayOfWeek,
      locationId: w.locationId,
      start: w.start,
      end: w.end,
      effectiveFrom: w.effectiveFrom,
      effectiveUntil: w.effectiveUntil,
    }));
  const [rows, setRows] = useState<Row[]>(fromServer);
  const [dirty, setDirty] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const editable = schedule.canManage;
  const defaultLocation = schedule.locations[0]?.id ?? "";

  const save = useMutation({
    mutationFn: () => replaceWeeklyHours(schedule.providerId, rows.map(({ key: _key, ...w }) => w)),
    onSuccess: async (updated) => {
      queryClient.setQueryData(["schedule", "provider-hours", schedule.providerId], updated);
      await queryClient.invalidateQueries({ queryKey: ["schedule"] });
      setDirty(false);
      showToast(`Weekly hours saved for ${schedule.providerName}.`);
    },
    onError: (e: Error) => setError(e.message),
  });

  function change(next: Row[]) {
    setRows(next);
    setDirty(true);
    setError(null);
  }

  const update = (key: string, patch: Partial<Row>) => change(rows.map((r) => (r.key === key ? { ...r, ...patch } : r)));

  function copyMondayToWeekdays() {
    const monday = rows.filter((r) => r.dayOfWeek === Weekday.Monday);
    const others = rows.filter((r) => r.dayOfWeek < Weekday.Tuesday || r.dayOfWeek > Weekday.Friday);
    const copies = ([1, 2, 3, 4] as Weekday[]).flatMap((d) => monday.map((m) => ({ ...m, key: nextKey(), dayOfWeek: d })));
    change([...others, ...copies]);
  }

  if (schedule.locations.length === 0) {
    return <p className="text-sm text-text-muted">{schedule.providerName} isn't assigned to any location yet, so no hours can be set.</p>;
  }

  return (
    <div className="space-y-3">
      <div className="divide-y divide-border rounded-lg border border-border">
        {days.map((day) => {
          const dayRows = rows.filter((r) => r.dayOfWeek === day).sort((a, b) => a.start.localeCompare(b.start));
          return (
            <div key={day} className="flex flex-col gap-2 p-3 sm:flex-row sm:items-start">
              <div className="w-28 shrink-0 pt-1.5 text-sm font-medium text-text">{dayNames[day]}</div>
              <div className="flex-1 space-y-2">
                {dayRows.length === 0 && <p className="pt-1.5 text-sm text-text-subtle">Not working</p>}
                {dayRows.map((r) =>
                  editable ? (
                    <div key={r.key} className="flex flex-wrap items-center gap-2">
                      <input
                        type="time"
                        aria-label={`${dayNames[day]} start`}
                        className="field-input w-auto py-1"
                        value={r.start}
                        onChange={(e) => update(r.key, { start: e.target.value })}
                      />
                      <span className="text-text-muted">to</span>
                      <input
                        type="time"
                        aria-label={`${dayNames[day]} end`}
                        className="field-input w-auto py-1"
                        value={r.end}
                        onChange={(e) => update(r.key, { end: e.target.value })}
                      />
                      <select
                        aria-label={`${dayNames[day]} location`}
                        className="field-input w-auto py-1"
                        value={r.locationId}
                        onChange={(e) => update(r.key, { locationId: e.target.value })}
                      >
                        {schedule.locations.map((l) => (
                          <option key={l.id} value={l.id}>{l.name}</option>
                        ))}
                      </select>
                      <button
                        type="button"
                        className="btn-secondary px-2 py-1 text-xs hover:border-danger hover:text-danger"
                        aria-label={`Remove ${dayNames[day]} ${r.start}–${r.end}`}
                        onClick={() => change(rows.filter((x) => x.key !== r.key))}
                      >
                        Remove
                      </button>
                    </div>
                  ) : (
                    <p key={r.key} className="pt-1.5 text-sm text-text">
                      {formatHhmm(r.start)} – {formatHhmm(r.end)} · {schedule.locations.find((l) => l.id === r.locationId)?.name ?? "—"}
                    </p>
                  ),
                )}
                {editable && (
                  <button
                    type="button"
                    className="text-sm font-medium text-primary hover:text-primary-deep"
                    onClick={() => {
                      const last = dayRows[dayRows.length - 1];
                      change([
                        ...rows,
                        {
                          key: nextKey(),
                          dayOfWeek: day,
                          locationId: last?.locationId ?? defaultLocation,
                          start: last ? last.end : "08:00",
                          end: last ? laterBy(last.end, 4) : "17:00",
                        },
                      ]);
                    }}
                  >
                    + Add hours
                  </button>
                )}
              </div>
            </div>
          );
        })}
      </div>

      {error && <p className="alert-error">{error}</p>}

      {editable && (
        <div className="flex flex-wrap items-center justify-between gap-2">
          <button type="button" className="btn-secondary" onClick={copyMondayToWeekdays}>
            Copy Monday to Tue–Fri
          </button>
          <div className="flex gap-2">
            <button
              type="button"
              className="btn-secondary disabled:opacity-50"
              disabled={!dirty}
              onClick={() => {
                setRows(fromServer());
                setDirty(false);
                setError(null);
              }}
            >
              Undo changes
            </button>
            <button type="button" className="btn-primary" disabled={!dirty || save.isPending} onClick={() => save.mutate()}>
              {save.isPending ? "Saving…" : "Save weekly hours"}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
