import { toDateKey } from "../time";

interface Props {
  year: number;
  counts: Map<string, number>; // YYYY-MM-DD -> appointments
  todayKey: string;
  onPickDate: (dateKey: string) => void;
}

const weekdayInitials = ["M", "T", "W", "T", "F", "S", "S"];

/** Volume shade relative to the busiest day of the year, so a quiet clinic
 * and a busy one both get a readable spread. */
function shade(count: number, max: number): string {
  if (count === 0) return "text-text-muted hover:bg-surface-muted";
  const ratio = count / Math.max(max, 1);
  if (ratio > 0.75) return "bg-primary-deep text-white hover:opacity-90";
  if (ratio > 0.5) return "bg-primary text-white hover:opacity-90";
  if (ratio > 0.25) return "bg-primary/40 text-text hover:bg-primary/50";
  return "bg-primary-light text-text hover:bg-primary/30";
}

/** All twelve months as compact calendars with appointment-volume shading
 * -- a year view shows *how busy*, never every appointment. Clicking a day
 * opens it in the Day view. */
export function YearView({ year, counts, todayKey, onPickDate }: Props) {
  const max = Math.max(0, ...counts.values());

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2 text-xs text-text-muted" aria-hidden="true">
        <span>Fewer</span>
        {["bg-primary-light", "bg-primary/40", "bg-primary", "bg-primary-deep"].map((c) => (
          <span key={c} className={`h-3 w-5 rounded ${c}`} />
        ))}
        <span>More appointments</span>
      </div>
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
        {Array.from({ length: 12 }, (_, month) => {
          const first = new Date(year, month, 1);
          const daysInMonth = new Date(year, month + 1, 0).getDate();
          const lead = (first.getDay() + 6) % 7; // Monday-first
          const days = Array.from({ length: daysInMonth }, (_, i) => new Date(year, month, i + 1));
          const monthTotal = days.reduce((sum, d) => sum + (counts.get(toDateKey(d)) ?? 0), 0);
          return (
            <section key={month} className="card p-3" aria-label={first.toLocaleDateString("en-US", { month: "long", year: "numeric" })}>
              <header className="mb-2 flex items-baseline justify-between">
                <h3 className="text-sm font-semibold text-text">{first.toLocaleDateString("en-US", { month: "long" })}</h3>
                <span className="text-xs text-text-muted">{monthTotal > 0 ? `${monthTotal} appts` : "—"}</span>
              </header>
              <div className="grid grid-cols-7 gap-0.5 text-center text-sm">
                {weekdayInitials.map((w, i) => (
                  <span key={i} className="pb-1 font-medium text-text-subtle">{w}</span>
                ))}
                {Array.from({ length: lead }, (_, i) => (
                  <span key={`lead-${i}`} />
                ))}
                {days.map((d) => {
                  const key = toDateKey(d);
                  const count = counts.get(key) ?? 0;
                  const label = `${d.toLocaleDateString("en-US", { weekday: "long", month: "long", day: "numeric" })}: ${count} appointment${count === 1 ? "" : "s"}`;
                  return (
                    <button
                      key={key}
                      type="button"
                      title={label}
                      aria-label={label}
                      onClick={() => onPickDate(key)}
                      className={`aspect-square rounded text-sm leading-none transition ${shade(count, max)} ${
                        key === todayKey ? "ring-2 ring-warning ring-offset-1" : ""
                      }`}
                    >
                      {d.getDate()}
                    </button>
                  );
                })}
              </div>
            </section>
          );
        })}
      </div>
    </div>
  );
}
