import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useToast } from "../../../components/Toast";
import { transitionAppointment, type AppointmentAction } from "../api";
import { isReschedulable, statusBadgeClass } from "../status";
import { formatTime, toWallClock } from "../time";
import { AppointmentStatus, AppointmentStatusLabels, type PagedScheduleAppointments, type ScheduleAppointment } from "../types";

const WEB_APP_URL = (import.meta.env.VITE_WEB_APP_URL as string | undefined)?.replace(/\/$/, "");

interface Props {
  data: PagedScheduleAppointments | undefined;
  isLoading: boolean;
  timezone: string;
  canManage: boolean;
  canReschedule: boolean;
  onPage: (page: number) => void;
  onSelect: (appointment: ScheduleAppointment) => void;
  onReschedule: (appointment: ScheduleAppointment) => void;
}

/** The one next step for a visit, if any -- the rest live in the details panel. */
function nextAction(status: AppointmentStatus): { action: AppointmentAction; label: string } | null {
  switch (status) {
    case AppointmentStatus.Scheduled:
    case AppointmentStatus.Confirmed:
      return { action: "check-in", label: "Check in" };
    case AppointmentStatus.CheckedIn:
      return { action: "start-visit", label: "Start visit" };
    case AppointmentStatus.InProgress:
      return { action: "complete", label: "Complete" };
    default:
      return null;
  }
}

/** All-patient appointment list: paginated server-side, a table on desktop
 * and stacked cards on a phone. Each row offers its next step inline;
 * Details opens the full panel (confirm, no-show, cancel, ...). */
export function ListView({ data, isLoading, timezone, canManage, canReschedule, onPage, onSelect, onReschedule }: Props) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const quick = useMutation({
    mutationFn: ({ id, action }: { id: string; action: AppointmentAction; label: string }) => transitionAppointment(id, action),
    onSuccess: async (_, { label }) => {
      showToast(`${label}: done.`);
      await queryClient.invalidateQueries({ queryKey: ["schedule"] });
    },
    onError: (e: Error) => showToast(e.message),
  });

  if (isLoading || !data) return <p className="p-4 text-text-muted">Loading appointments…</p>;
  if (data.items.length === 0) return <p className="p-4 text-text-muted">No appointments match these filters.</p>;

  const pages = Math.max(1, Math.ceil(data.total / data.pageSize));
  const firstRow = (data.page - 1) * data.pageSize + 1;

  const actions = (a: ScheduleAppointment) => {
    const next = canManage ? nextAction(a.status) : null;
    return (
      <div className="flex flex-wrap items-center gap-1.5">
        {next && (
          <button
            type="button"
            className="btn-primary px-2.5 py-1 text-xs"
            disabled={quick.isPending}
            onClick={() => quick.mutate({ id: a.id, action: next.action, label: next.label })}
          >
            {next.label}
          </button>
        )}
        {canReschedule && isReschedulable(a.status) && (
          <button type="button" className="btn-secondary px-2.5 py-1 text-xs" onClick={() => onReschedule(a)}>
            Reschedule
          </button>
        )}
        <button type="button" className="btn-secondary px-2.5 py-1 text-xs" onClick={() => onSelect(a)}>
          Details
        </button>
        {WEB_APP_URL && (
          <a className="px-1 text-xs font-medium text-primary hover:text-primary-deep" href={`${WEB_APP_URL}/patients/${a.patientId}`} target="_blank" rel="noreferrer">
            View patient ↗
          </a>
        )}
      </div>
    );
  };

  const when = (a: ScheduleAppointment) => {
    const start = toWallClock(a.startsAt, timezone);
    return {
      date: start.toLocaleDateString("en-US", { weekday: "short", month: "short", day: "numeric" }),
      time: formatTime(start),
    };
  };

  const badge = (a: ScheduleAppointment) => (
    <span className={`inline-block whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium ${statusBadgeClass[a.status]}`}>
      {AppointmentStatusLabels[a.status]}
    </span>
  );

  return (
    <div>
      {/* Desktop / tablet: table */}
      <div className="hidden overflow-x-auto md:block">
        <table className="data-table">
          <thead>
            <tr>
              <th>Time</th>
              <th>Patient</th>
              <th>MRN</th>
              <th>Provider</th>
              <th>Type</th>
              <th>Location</th>
              <th>Duration</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {data.items.map((a) => {
              const w = when(a);
              return (
                <tr key={a.id}>
                  <td className="whitespace-nowrap">
                    <div className="font-medium text-text">{w.time}</div>
                    <div className="text-xs text-text-muted">{w.date}</div>
                  </td>
                  <td>
                    <button type="button" className="table-link text-left" onClick={() => onSelect(a)}>
                      {a.patientName}
                    </button>
                  </td>
                  <td className="whitespace-nowrap">{a.medicalRecordNumber}</td>
                  <td>
                    {a.providerName ?? "Unassigned"}
                    {a.providerCredentials && <span className="text-text-muted">, {a.providerCredentials}</span>}
                  </td>
                  <td>{a.appointmentTypeName ?? "—"}</td>
                  <td>{a.locationName ?? "—"}</td>
                  <td className="whitespace-nowrap">{a.durationMinutes} min</td>
                  <td>{badge(a)}</td>
                  <td>{actions(a)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {/* Phone: stacked cards */}
      <ul className="divide-y divide-border md:hidden">
        {data.items.map((a) => {
          const w = when(a);
          return (
            <li key={a.id} className="space-y-1.5 p-3">
              <div className="flex items-start justify-between gap-2">
                <div>
                  <div className="font-semibold text-text">{a.patientName}</div>
                  <div className="text-xs text-text-muted">{a.medicalRecordNumber}</div>
                </div>
                {badge(a)}
              </div>
              <div className="text-sm text-text">
                {w.date} · {w.time} · {a.durationMinutes} min
              </div>
              <div className="text-sm text-text-muted">
                {a.providerName ?? "Unassigned"} · {a.appointmentTypeName ?? "Appointment"}
                {a.locationName ? ` · ${a.locationName}` : ""}
              </div>
              {actions(a)}
            </li>
          );
        })}
      </ul>

      <nav className="flex flex-wrap items-center justify-between gap-2 border-t border-border px-4 py-3 text-sm" aria-label="Pagination">
        <span className="text-text-muted">
          {firstRow}–{firstRow + data.items.length - 1} of {data.total}
        </span>
        <div className="flex items-center gap-2">
          <button type="button" className="btn-secondary disabled:opacity-50" disabled={data.page <= 1} onClick={() => onPage(data.page - 1)}>
            ‹ Previous
          </button>
          <span className="text-text-muted">
            Page {data.page} of {pages}
          </span>
          <button type="button" className="btn-secondary disabled:opacity-50" disabled={data.page >= pages} onClick={() => onPage(data.page + 1)}>
            Next ›
          </button>
        </div>
      </nav>
    </div>
  );
}
