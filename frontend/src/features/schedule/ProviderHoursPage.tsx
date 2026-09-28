import { useQuery } from "@tanstack/react-query";
import { Link, useSearchParams } from "react-router-dom";
import { fetchProviderSchedule, fetchScheduleSettings } from "./api";
import { TimeOffPanel } from "./components/TimeOffPanel";
import { WeeklyHoursEditor } from "./components/WeeklyHoursEditor";

/** Provider working hours and time off -- what the Day view shades and the
 * booking rules enforce. Everyone on staff can look; Admin, Director, and
 * Scheduler can change it. */
export function ProviderHoursPage() {
  const [params, setParams] = useSearchParams();
  const settingsQuery = useQuery({ queryKey: ["schedule", "settings"], queryFn: fetchScheduleSettings });
  const providers = (settingsQuery.data?.providers ?? []).filter((p) => p.isActive);
  const providerId = params.get("provider") || providers[0]?.id || "";

  const scheduleQuery = useQuery({
    queryKey: ["schedule", "provider-hours", providerId],
    queryFn: () => fetchProviderSchedule(providerId),
    enabled: !!providerId,
  });
  const schedule = scheduleQuery.data;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <Link to="/schedule" className="text-sm text-text-muted hover:text-primary">← Back to schedule</Link>
          <h1 className="text-xl font-semibold text-text">Provider hours &amp; time off</h1>
        </div>
        <label className="flex items-center gap-2 text-sm text-text-muted">
          Provider
          <select
            className="field-input w-auto"
            value={providerId}
            onChange={(e) => setParams({ provider: e.target.value }, { replace: true })}
          >
            {providers.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}{p.credentials ? `, ${p.credentials}` : ""}
              </option>
            ))}
          </select>
        </label>
      </div>

      {(settingsQuery.isLoading || scheduleQuery.isLoading) && <p className="text-text-muted">Loading…</p>}
      {settingsQuery.isError && <p className="alert-error">{settingsQuery.error.message}</p>}
      {scheduleQuery.isError && <p className="alert-error">{scheduleQuery.error.message}</p>}
      {settingsQuery.data && providers.length === 0 && <p className="text-text-muted">No active providers.</p>}

      {schedule && (
        <>
          {!schedule.canManage && (
            <p className="rounded-md bg-info-light px-3 py-2 text-sm text-info">
              You can view these hours. Changes are made by an administrator or the front desk.
            </p>
          )}
          <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
            <section className="card" aria-labelledby="weekly-hours-title">
              <h2 id="weekly-hours-title" className="mb-1 text-base font-semibold text-text">Weekly working hours</h2>
              <p className="mb-3 text-sm text-text-muted">
                Appointments outside these hours need an administrator's override. Times are in {schedule.timezone.replace("_", " ")} time.
              </p>
              <WeeklyHoursEditor key={schedule.providerId} schedule={schedule} />
            </section>
            <section className="card" aria-labelledby="time-off-title">
              <h2 id="time-off-title" className="mb-1 text-base font-semibold text-text">Time off, lunch &amp; blocked time</h2>
              <p className="mb-3 text-sm text-text-muted">Blocks new bookings. Existing appointments aren't moved — you'll be told which ones to reschedule.</p>
              <TimeOffPanel key={schedule.providerId} schedule={schedule} />
            </section>
          </div>
        </>
      )}
    </div>
  );
}
