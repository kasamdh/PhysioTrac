import { useEffect, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useToast } from "../../../components/Toast";
import { ApiError } from "../../../lib/apiClient";
import { rescheduleAppointment, validateMove } from "../api";
import { formatTime, toWallClock } from "../time";
import type { AppointmentSlotSummary, RescheduleRequest, ScheduleAppointment, SchedulingConflictBody, SchedulingViolation } from "../types";

interface Props {
  appointment: ScheduleAppointment;
  request: RescheduleRequest;
  timezone: string;
  onClose: () => void;
  /** Called after a successful move; defaults to onClose. */
  onMoved?: () => void;
}

function Slot({ label, slot, timezone, highlight }: { label: string; slot: AppointmentSlotSummary; timezone: string; highlight?: boolean }) {
  const start = toWallClock(slot.startsAt, timezone);
  const end = toWallClock(slot.endsAt, timezone);
  return (
    <div className={`rounded-lg border p-3 text-sm ${highlight ? "border-primary bg-primary-light/40" : "border-border bg-surface-muted"}`}>
      <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-text-subtle">{label}</p>
      <p className="font-medium text-text">{slot.providerName ?? "Unassigned"}</p>
      <p className="text-text-muted">{start.toLocaleDateString("en-US", { weekday: "short", month: "short", day: "numeric" })}</p>
      <p className="text-text-muted">
        {formatTime(start)} – {formatTime(end)}
      </p>
      {slot.locationName && <p className="text-text-muted">{slot.locationName}</p>}
    </div>
  );
}

/** Confirmation for every move -- drag-and-drop, resize, or the Reschedule
 * form. Nothing moves silently: it dry-runs the move first, shows From/To,
 * and only "Move appointment" saves. If rules are broken it becomes the
 * conflict dialog instead, with Override (plus a required reason) only when
 * the server says this user may. The save re-validates server-side, so a
 * conflict that appears in between (someone else booked the slot) lands
 * back here rather than being lost. */
export function MoveAppointmentDialog({ appointment, request, timezone, onClose, onMoved }: Props) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const [reason, setReason] = useState("");
  const [lateConflict, setLateConflict] = useState<{ violations: SchedulingViolation[]; canOverride: boolean } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const firstButton = useRef<HTMLButtonElement>(null);

  const check = useQuery({
    queryKey: ["schedule", "move-check", appointment.id, request],
    queryFn: () => validateMove(appointment.id, request),
    retry: false,
    gcTime: 0,
  });

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  useEffect(() => {
    if (check.data) firstButton.current?.focus();
  }, [check.data]);

  const move = useMutation({
    mutationFn: (overrideReason: string | null) => rescheduleAppointment(appointment.id, { ...request, overrideReason }),
    onSuccess: async (_, overrideReason) => {
      showToast(overrideReason ? "Appointment moved (override recorded)." : "Appointment moved.");
      await queryClient.invalidateQueries({ queryKey: ["schedule"] });
      (onMoved ?? onClose)();
    },
    onError: (e: Error) => {
      const body = e instanceof ApiError ? (e.payload as SchedulingConflictBody | undefined) : undefined;
      if (body?.violations?.length) {
        setLateConflict({ violations: body.violations, canOverride: !!body.canOverride });
      } else {
        setError(e.message);
      }
    },
  });

  const violations = lateConflict?.violations ?? check.data?.violations ?? [];
  const canOverride = lateConflict?.canOverride ?? check.data?.canOverride ?? false;
  const isConflict = violations.length > 0;
  const providerChanged = check.data && check.data.from.providerId !== check.data.to.providerId;

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="move-dialog-title"
        className="modal-panel max-w-lg"
        onClick={(e) => e.stopPropagation()}
      >
        {check.isLoading && <p className="text-sm text-text-muted">Checking the new time…</p>}

        {check.isError && (
          <>
            <h2 id="move-dialog-title" className="mb-2 text-lg font-semibold text-text">Can't move this appointment</h2>
            <p className="alert-error mb-4">{check.error.message}</p>
            <div className="flex justify-end">
              <button type="button" className="btn-secondary" onClick={onClose}>Close</button>
            </div>
          </>
        )}

        {check.data && (
          <>
            <h2 id="move-dialog-title" className={`mb-1 text-lg font-semibold ${isConflict ? "text-danger" : "text-text"}`}>
              {isConflict ? "Scheduling conflict" : providerChanged ? "Move appointment to another provider?" : "Move appointment?"}
            </h2>
            <p className="mb-3 text-sm text-text-muted">
              Patient: <span className="font-medium text-text">{check.data.patientName}</span>
            </p>

            <div className="mb-4 grid grid-cols-1 gap-2 sm:grid-cols-2">
              <Slot label="From" slot={check.data.from} timezone={timezone} />
              <Slot label="To" slot={check.data.to} timezone={timezone} highlight={!isConflict} />
            </div>

            {isConflict && (
              <ul className="mb-4 space-y-1.5 text-sm" aria-label="Conflicts">
                {violations.map((v) => (
                  <li key={v.code + v.message} className="rounded-md bg-danger-light px-3 py-2 text-danger">
                    {v.message}
                  </li>
                ))}
              </ul>
            )}

            {isConflict && canOverride && (
              <label className="mb-4 block">
                <span className="field-label">Reason for override (required, recorded in the audit log)</span>
                <textarea
                  className="field-input"
                  rows={2}
                  maxLength={500}
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                />
              </label>
            )}

            {error && <p className="alert-error mb-3">{error}</p>}

            <div className="flex flex-wrap justify-end gap-2">
              {isConflict ? (
                <>
                  <button ref={firstButton} type="button" className="btn-secondary" onClick={onClose}>
                    Choose another time
                  </button>
                  {canOverride && (
                    <button
                      type="button"
                      className="btn-primary bg-danger hover:bg-danger"
                      disabled={!reason.trim() || move.isPending}
                      onClick={() => move.mutate(reason.trim())}
                    >
                      {move.isPending ? "Moving…" : "Override and move"}
                    </button>
                  )}
                </>
              ) : (
                <>
                  <button type="button" className="btn-secondary" onClick={onClose}>
                    Cancel
                  </button>
                  <button ref={firstButton} type="button" className="btn-primary" disabled={move.isPending} onClick={() => move.mutate(null)}>
                    {move.isPending ? "Moving…" : "Move appointment"}
                  </button>
                </>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  );
}
