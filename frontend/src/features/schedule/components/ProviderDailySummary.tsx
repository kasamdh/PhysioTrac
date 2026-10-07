import { formatHhmm, formatHours, formatTime, toWallClock } from "../time";
import type { ProviderDayColumn, ScheduleAppointment } from "../types";
import { disciplineLabel } from "../status";

interface Props {
  title: string;
  columns: ProviderDayColumn[];
  appointments: ScheduleAppointment[];
  timezone: string;
  onSelectAppointment: (appointment: ScheduleAppointment) => void;
}

function Stat({ label, value, tone }: { label: string; value: string | number; tone?: string }) {
  return (
    <div className="flex justify-between gap-2">
      <dt className="text-text-muted">{label}</dt>
      <dd className={`font-medium ${tone ?? "text-text"}`}>{value}</dd>
    </div>
  );
}

/** "Today's PT schedule": one compact card per provider with the day's
 * counts, booked vs. available hours, utilization, and who's current/next. */
export function ProviderDailySummary({ title, columns, appointments, timezone, onSelectAppointment }: Props) {
  if (columns.length === 0) return null;
  const byId = new Map(appointments.map((a) => [a.id, a]));

  return (
    <section aria-label="Provider daily summary">
      <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-text-subtle">{title}</h2>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {columns.map(({ provider, workingHours, summary: s }) => {
          const current = s.currentAppointmentId ? byId.get(s.currentAppointmentId) : undefined;
          const next = s.nextAppointmentId ? byId.get(s.nextAppointmentId) : undefined;
          const discipline = disciplineLabel[provider.discipline];
          return (
            <article key={provider.id} className="card p-4 text-sm">
              <header className="mb-2">
                <h3 className="font-semibold text-text">
                  {provider.name}
                  {provider.credentials && <span className="font-normal text-text-muted">, {provider.credentials}</span>}
                </h3>
                <p className="text-xs text-text-muted">
                  {discipline && <span className="mr-1 rounded bg-primary-light px-1.5 py-0.5 font-medium text-primary-deep">{discipline}</span>}
                  {workingHours.length === 0
                    ? "No working hours set"
                    : workingHours.map((w) => `${formatHhmm(w.start)} – ${formatHhmm(w.end)}`).join(", ")}
                </p>
              </header>
              <dl className="space-y-0.5">
                <Stat label="Appointments" value={s.appointments} />
                <Stat label="Completed" value={s.completed} tone="text-success" />
                {s.checkedIn > 0 && <Stat label="Checked in" value={s.checkedIn} tone="text-warning" />}
                {s.inProgress > 0 && <Stat label="In progress" value={s.inProgress} tone="text-primary-deep" />}
                {s.cancelled > 0 && <Stat label="Cancelled" value={s.cancelled} tone="text-danger" />}
                {s.noShow > 0 && <Stat label="No show" value={s.noShow} tone="text-danger" />}
                <Stat label="Remaining" value={s.remaining} />
                <Stat label="Booked" value={formatHours(s.bookedMinutes)} />
                {s.workingMinutes > 0 && <Stat label="Available" value={formatHours(s.availableMinutes)} />}
                {s.utilizationPercent !== null && <Stat label="Utilization" value={`${s.utilizationPercent}%`} />}
                {s.firstAppointmentAt && s.lastAppointmentEndsAt && (
                  <Stat
                    label="First – last"
                    value={`${formatTime(toWallClock(s.firstAppointmentAt, timezone))} – ${formatTime(toWallClock(s.lastAppointmentEndsAt, timezone))}`}
                  />
                )}
              </dl>
              {(current || next) && (
                <div className="mt-2 space-y-1 border-t border-border pt-2 text-xs">
                  {current && (
                    <button type="button" className="touch-line block w-full truncate text-left hover:text-primary" onClick={() => onSelectAppointment(current)}>
                      <span className="text-text-muted">Now: </span>
                      {current.patientName}
                    </button>
                  )}
                  {next && (
                    <button type="button" className="touch-line block w-full truncate text-left hover:text-primary" onClick={() => onSelectAppointment(next)}>
                      <span className="text-text-muted">Next: </span>
                      {next.patientName} at {formatTime(toWallClock(next.startsAt, timezone))}
                    </button>
                  )}
                </div>
              )}
            </article>
          );
        })}
      </div>
    </section>
  );
}
