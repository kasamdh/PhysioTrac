import { useState } from "react";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useNavigate, useSearchParams } from "react-router-dom";
import { RowMenu } from "../../components/RowMenu";
import { SegmentedButtons } from "../../components/SegmentedButtons";
import { useToast } from "../../components/Toast";
import { useAuth } from "../auth/AuthProvider";
import { RoleSets, canAccess } from "../auth/permissions";
import { transitionAppointment, type AppointmentAction } from "../schedule/api";
import { appointmentColors } from "../schedule/status";
import { AppointmentStatus, AppointmentStatusLabels } from "../schedule/types";
import { openAppointmentEncounter } from "../encounter/api";
import { fetchWorkflowToday } from "./api";
import { NoteQueuesPanel } from "./NoteQueuesPanel";
import { NoteStatus, NoteStatusLabels, type WorkflowAppointment } from "./types";

type StatusFilter = "all" | "todo" | "checked-in" | "in-progress" | "completed" | "missed";
const inFilter: Record<StatusFilter, (s: AppointmentStatus) => boolean> = {
  all: () => true,
  todo: (s) => s === AppointmentStatus.Scheduled || s === AppointmentStatus.Confirmed,
  "checked-in": (s) => s === AppointmentStatus.CheckedIn,
  "in-progress": (s) => s === AppointmentStatus.InProgress,
  completed: (s) => s === AppointmentStatus.Completed,
  missed: (s) => s === AppointmentStatus.Cancelled || s === AppointmentStatus.NoShow,
};

/** The next status step for a row, as a one-click button. */
function nextStep(s: AppointmentStatus): { action: AppointmentAction; label: string } | null {
  switch (s) {
    case AppointmentStatus.Scheduled:
    case AppointmentStatus.Confirmed:
      return { action: "check-in", label: "Check in" };
    case AppointmentStatus.CheckedIn:
      return { action: "start-visit", label: "Start visit" };
    default:
      return null;
  }
}

function noteLabel(a: WorkflowAppointment): string {
  if (a.noteStatus === null) return "Not started";
  return NoteStatusLabels[a.noteStatus] ?? "Signed";
}

const formatTime = (iso: string) => new Date(iso).toLocaleTimeString("en-US", { hour: "numeric", minute: "2-digit" });

/** Today’s appointments for one therapist (their own by default), with
 * check-in / start-visit and visit documentation in one place. */
export function WorkflowPage() {
  const { user } = useAuth();
  const { showToast } = useToast();
  const queryClient = useQueryClient();
  const [params, setParams] = useSearchParams();
  const providerParam = params.get("provider") ?? "";
  const all = providerParam === "all";
  const [status, setStatus] = useState<StatusFilter>("all");
  const navigate = useNavigate();
  const canDocument = canAccess(user, RoleSets.Clinical);

  const day = useQuery({
    queryKey: ["workflow", "today", providerParam],
    queryFn: () => fetchWorkflowToday({ providerId: all ? undefined : providerParam || undefined, all }),
    placeholderData: keepPreviousData,
    refetchInterval: 60_000, // front desk check-ins show up without a manual refresh
  });
  const refresh = () => void queryClient.invalidateQueries({ queryKey: ["workflow", "today"] });

  const transition = useMutation({
    mutationFn: (v: { a: WorkflowAppointment; action: AppointmentAction; label: string }) =>
      transitionAppointment(v.a.appointmentId, v.action),
    onSuccess: (_d, v) => {
      showToast(`${v.a.patientName}: ${v.label.toLowerCase()} done.`);
      refresh();
    },
    onError: (e: Error) => showToast(e.message),
  });
  // Document = open this visit's note in Clinical Charting, creating its
  // draft first when the visit has none yet (one note per visit).
  // The server decides the visit's note type and template, one note per visit.
  const openChart = useMutation({
    mutationFn: async (a: WorkflowAppointment) => a.noteId ?? (await openAppointmentEncounter(a.appointmentId)).noteId,
    onSuccess: (noteId) => navigate(`/chart/${noteId}`),
    onError: (e: Error) => showToast(e.message),
  });

  const data = day.data;
  const rows = (data?.appointments ?? []).filter((a) => inFilter[status](a.status));
  const count = (f: StatusFilter) => (data?.appointments ?? []).filter((a) => inFilter[f](a.status)).length;
  const dateLabel = data
    ? new Date(`${data.date}T12:00:00`).toLocaleDateString("en-US", { weekday: "long", month: "long", day: "numeric", year: "numeric" })
    : "";
  const showProviderColumn = !!data?.allProviders;
  const noProvider = data && !data.allProviders && !data.selectedProviderId;

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-baseline gap-x-4 gap-y-1">
        <h1 className="text-2xl font-bold text-[#1565b8]">Workflow</h1>
        <span className="text-lg text-[#333]">Today — {dateLabel}</span>
      </div>

      {data && <NoteQueuesPanel today={data.date} />}

      <div className="list-toolbar">
        {data && !data.ownDayOnly && (
          <label className="toolbar-label">
            Therapist
            <select
              className="toolbar-select"
              value={all ? "all" : (data.selectedProviderId ?? "")}
              onChange={(e) =>
                setParams(
                  (prev) => {
                    const next = new URLSearchParams(prev);
                    if (e.target.value) next.set("provider", e.target.value);
                    else next.delete("provider");
                    return next;
                  },
                  { replace: true },
                )
              }
            >
              {!data.myProviderId && <option value="">Choose a therapist…</option>}
              {data.providers.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                  {p.credentials ? `, ${p.credentials}` : ""}
                  {p.id === data.myProviderId ? " (me)" : ""}
                </option>
              ))}
              <option value="all">All therapists</option>
            </select>
          </label>
        )}
        <SegmentedButtons
          label="Appointment status"
          value={status}
          onChange={setStatus}
          options={[
            { value: "all", label: `All (${count("all")})` },
            { value: "todo", label: `To check in (${count("todo")})` },
            { value: "checked-in", label: `Checked in (${count("checked-in")})` },
            { value: "in-progress", label: `In progress (${count("in-progress")})` },
            { value: "completed", label: `Completed (${count("completed")})` },
            { value: "missed", label: `Cancelled / no-show (${count("missed")})` },
          ]}
        />
        <button type="button" className="btn-refresh ml-auto" disabled={day.isFetching} onClick={() => void day.refetch()}>
          {day.isFetching ? "Refreshing…" : "Refresh"}
        </button>
      </div>

      <div className="list-wrap">
        {day.isLoading && <p className="p-5 text-text-muted">Loading…</p>}
        {day.isError && <p className="alert-error m-5">{day.error.message}</p>}
        {noProvider && (
          <p className="p-5 text-text-muted">You don’t have a provider schedule. Choose a therapist above to see their day.</p>
        )}
        {data && !noProvider && rows.length === 0 && (
          <p className="p-5 text-text-muted">No appointments {status === "all" ? "today" : "with this status today"}.</p>
        )}
        {rows.length > 0 && (
          <table className="data-table">
            <thead>
              <tr>
                <th className="cell-menu">
                  <span className="sr-only">Actions</span>
                </th>
                <th>Time</th>
                <th>Patient</th>
                <th>Visit type</th>
                {showProviderColumn && <th>Therapist</th>}
                <th>Status</th>
                <th>Note</th>
                <th>Next step</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((a) => {
                const step = nextStep(a.status);
                const voided = a.status === AppointmentStatus.Cancelled || a.status === AppointmentStatus.NoShow;
                const canWrite = canDocument && !voided;
                const noteDone = a.noteStatus !== null && a.noteStatus !== NoteStatus.Draft;
                const docLabel = noteDone ? "View note" : a.noteStatus === NoteStatus.Draft ? "Continue note" : "Document";
                return (
                  <tr key={a.appointmentId} className={voided ? "opacity-60" : ""}>
                    <td className="cell-menu">
                      <RowMenu
                        label={`Actions for ${a.patientName}`}
                        items={[
                          ...(canWrite || (canDocument && a.noteId) ? [{ label: docLabel, onSelect: () => openChart.mutate(a) }] : []),
                          ...(step ? [{ label: step.label, onSelect: () => transition.mutate({ a, ...step }) }] : []),
                          ...(a.status === AppointmentStatus.CheckedIn || a.status === AppointmentStatus.InProgress
                            ? [{ label: "Complete without note", onSelect: () => transition.mutate({ a, action: "complete" as const, label: "Complete" }) }]
                            : []),
                          {
                            label: "Open in Schedule",
                            to: `/schedule?view=day&date=${data!.date}&location=all${a.providerId ? `&provider=${a.providerId}` : ""}`,
                          },
                          { label: "Patient documentation", to: `/patients/${a.patientId}/documentation` },
                          { label: "Send message", to: `/admin/messages?patient=${a.patientId}` },
                        ]}
                      />
                    </td>
                    <td data-label="Time" className="whitespace-nowrap">
                      {formatTime(a.startsAt)}
                    </td>
                    <td data-label="Patient">
                      {/* One block, so name and MRN stack in the phone card too. */}
                      <div>
                        {canDocument && (canWrite || a.noteId) ? (
                          <button type="button" className="table-link text-left" disabled={openChart.isPending} onClick={() => openChart.mutate(a)}>
                            {a.patientName}
                          </button>
                        ) : (
                          a.patientName
                        )}
                        <span className="block text-text-muted">{a.medicalRecordNumber}</span>
                      </div>
                    </td>
                    <td data-label="Visit type">
                      <span className="flex items-center gap-2">
                        <span aria-hidden="true" className="h-3 w-3 shrink-0 rounded-full" style={{ background: appointmentColors(a).accent }} />
                        {a.appointmentTypeName ?? "Visit"}
                      </span>
                    </td>
                    {showProviderColumn && <td data-label="Therapist">{a.providerName ?? "—"}</td>}
                    <td data-label="Status">{AppointmentStatusLabels[a.status]}</td>
                    <td data-label="Note">{voided && a.noteStatus === null ? "—" : noteLabel(a)}</td>
                    <td data-label="Next step">
                      <div className="flex flex-wrap gap-2">
                        {step && (
                          <button
                            type="button"
                            className="btn-refresh"
                            disabled={transition.isPending}
                            onClick={() => transition.mutate({ a, ...step })}
                          >
                            {step.label}
                          </button>
                        )}
                        {canWrite && !noteDone && (
                          <button type="button" className="btn-primary" disabled={openChart.isPending} onClick={() => openChart.mutate(a)}>
                            {docLabel}
                          </button>
                        )}
                        {!step && !(canWrite && !noteDone) && <span className="text-text-muted">—</span>}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>

    </div>
  );
}
