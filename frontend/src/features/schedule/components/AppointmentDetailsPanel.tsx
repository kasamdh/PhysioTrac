import { useEffect, useRef, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useToast } from "../../../components/Toast";
import { transitionAppointment, type AppointmentAction } from "../api";
import { isReschedulable, statusBadgeClass } from "../status";
import { formatTime, toWallClock } from "../time";
import { AppointmentStatus, AppointmentStatusLabels, type ScheduleAppointment } from "../types";

// Patient charts and clinical documentation still live in the Blazor staff
// app; link out to it when its URL is configured.
const WEB_APP_URL = (import.meta.env.VITE_WEB_APP_URL as string | undefined)?.replace(/\/$/, "");

interface Props {
  appointment: ScheduleAppointment;
  timezone: string;
  canManage: boolean;
  canReschedule: boolean;
  onReschedule: () => void;
  onClose: () => void;
}

const actionLabels: Record<AppointmentAction, string> = {
  confirm: "Confirm",
  "check-in": "Check in",
  "start-visit": "Start visit",
  complete: "Complete",
  "no-show": "No show",
  cancel: "Cancel appointment",
};

const actionToast: Record<AppointmentAction, string> = {
  confirm: "Appointment confirmed.",
  "check-in": "Patient checked in.",
  "start-visit": "Visit started.",
  complete: "Appointment completed.",
  "no-show": "Marked as no show.",
  cancel: "Appointment cancelled.",
};

/** Which transitions the Api allows from each status (AppointmentService). */
function actionsFor(status: AppointmentStatus): AppointmentAction[] {
  switch (status) {
    case AppointmentStatus.Scheduled:
      return ["confirm", "check-in", "no-show", "cancel"];
    case AppointmentStatus.Confirmed:
      return ["check-in", "no-show", "cancel"];
    case AppointmentStatus.CheckedIn:
      return ["start-visit", "complete", "cancel"];
    case AppointmentStatus.InProgress:
      return ["complete"];
    default:
      return [];
  }
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <>
      <dt className="text-text-muted">{label}</dt>
      <dd className="text-text">{children}</dd>
    </>
  );
}

/** Quick appointment details: a right-hand drawer on desktop, full-width on
 * a phone. Status actions are offered only for the caller's role and only
 * the ones valid from the current status; the Api enforces both anyway. */
export function AppointmentDetailsPanel({ appointment: a, timezone, canManage, canReschedule, onReschedule, onClose }: Props) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const closeRef = useRef<HTMLButtonElement>(null);
  const [confirmingCancel, setConfirmingCancel] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    closeRef.current?.focus();
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  const mutation = useMutation({
    mutationFn: (action: AppointmentAction) => transitionAppointment(a.id, action),
    onSuccess: async (_, action) => {
      showToast(actionToast[action]);
      setConfirmingCancel(false);
      await queryClient.invalidateQueries({ queryKey: ["schedule"] });
      onClose();
    },
    onError: (e: Error) => setError(e.message),
  });

  const start = toWallClock(a.startsAt, timezone);
  const end = toWallClock(a.endsAt, timezone);
  const actions = canManage ? actionsFor(a.status) : [];
  const reschedulable = canReschedule && isReschedulable(a.status);

  function run(action: AppointmentAction) {
    setError(null);
    if (action === "cancel" && !confirmingCancel) {
      setConfirmingCancel(true);
      return;
    }
    mutation.mutate(action);
  }

  return (
    <div className="fixed inset-0 z-40 flex justify-end bg-slate-900/30" onClick={onClose}>
      <aside
        role="dialog"
        aria-modal="true"
        aria-labelledby="appt-panel-title"
        className="flex h-full w-full max-w-md flex-col overflow-y-auto border-l border-border bg-surface shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        <header className="flex items-start justify-between gap-3 border-b border-border px-5 py-4">
          <div>
            <h2 id="appt-panel-title" className="text-lg font-semibold text-text">{a.patientName}</h2>
            <span className={`mt-1 inline-block rounded-full px-2 py-0.5 text-xs font-medium ${statusBadgeClass[a.status]}`}>
              {AppointmentStatusLabels[a.status]}
            </span>
          </div>
          <button ref={closeRef} type="button" onClick={onClose} className="btn-secondary" aria-label="Close details">
            Close
          </button>
        </header>

        <dl className="grid grid-cols-[120px_1fr] gap-x-3 gap-y-2 px-5 py-4 text-sm">
          <Row label="Date of birth">{a.patientDateOfBirth}</Row>
          <Row label="MRN">{a.medicalRecordNumber}</Row>
          <Row label="Phone">{a.patientPhone ?? "—"}</Row>
          <Row label="Provider">
            {a.providerName ?? "Unassigned"}
            {a.providerCredentials && <span className="text-text-muted">, {a.providerCredentials}</span>}
          </Row>
          <Row label="Date">{start.toLocaleDateString("en-US", { weekday: "long", month: "long", day: "numeric", year: "numeric" })}</Row>
          <Row label="Time">{formatTime(start)} – {formatTime(end)}</Row>
          <Row label="Duration">{a.durationMinutes} min</Row>
          <Row label="Type">{a.appointmentTypeName ?? "—"}</Row>
          <Row label="Location">{a.locationName ?? "—"}</Row>
          {a.reasonForVisit && <Row label="Reason">{a.reasonForVisit}</Row>}
          <Row label="Confirmation">{a.confirmationNumber}</Row>
        </dl>

        <div className="mt-auto space-y-3 border-t border-border px-5 py-4">
          {error && <p className="alert-error">{error}</p>}
          {confirmingCancel && (
            <p className="text-sm text-danger">Cancel this appointment? This can't be undone here.</p>
          )}
          <div className="flex flex-wrap gap-2">
            {actions.map((action) => (
              <button
                key={action}
                type="button"
                disabled={mutation.isPending}
                onClick={() => run(action)}
                className={
                  action === "cancel" || action === "no-show"
                    ? "btn-secondary hover:border-danger hover:text-danger"
                    : action === actions[0]
                      ? "btn-primary"
                      : "btn-secondary"
                }
              >
                {action === "cancel" && confirmingCancel ? "Yes, cancel it" : actionLabels[action]}
              </button>
            ))}
            {confirmingCancel && (
              <button type="button" className="btn-secondary" onClick={() => setConfirmingCancel(false)}>
                Keep it
              </button>
            )}
            {reschedulable && !confirmingCancel && (
              <button type="button" className="btn-secondary" onClick={onReschedule}>
                Reschedule
              </button>
            )}
          </div>
          {WEB_APP_URL && (
            <div className="flex flex-wrap gap-3 text-sm">
              <a className="font-medium text-primary hover:text-primary-deep" href={`${WEB_APP_URL}/patients/${a.patientId}`} target="_blank" rel="noreferrer">
                View patient ↗
              </a>
              {a.status === AppointmentStatus.InProgress && (
                <a className="font-medium text-primary hover:text-primary-deep" href={`${WEB_APP_URL}/patients/${a.patientId}`} target="_blank" rel="noreferrer">
                  Open documentation ↗
                </a>
              )}
            </div>
          )}
        </div>
      </aside>
    </div>
  );
}
