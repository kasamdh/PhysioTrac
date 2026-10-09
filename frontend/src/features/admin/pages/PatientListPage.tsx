import { useEffect, useState } from "react";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useSearchParams } from "react-router-dom";
import { RowMenu } from "../../../components/RowMenu";
import { SegmentedButtons } from "../../../components/SegmentedButtons";
import { useAuth } from "../../auth/AuthProvider";
import { RoleSets, canAccess } from "../../auth/permissions";
import { EditPatientDialog } from "../../patients/EditPatientDialog";
import { AddPatientDialog } from "../../patients/AddPatientDialog";
import { useToast } from "../../../components/Toast";
import { PatientStatus } from "../../patients/types";
import { AdminPageHeader } from "../AdminPageHeader";
import { deletePatient, fetchLocations, fetchPatientDirectory, restorePatient } from "../api";
import type { PatientDirectoryFilters } from "../types";

const STATUS_LABELS: Record<PatientStatus, string> = {
  [PatientStatus.Active]: "Active",
  [PatientStatus.Inactive]: "Inactive",
  [PatientStatus.Discharged]: "Discharged",
};
const PAGE_SIZE = 25;

/** yyyy-mm-dd for a day offset from today, in the browser's timezone. */
function dayKey(offset: number): string {
  const d = new Date();
  d.setDate(d.getDate() + offset);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

type QuickRange = "all" | "today" | "yesterday" | "last3" | "next7" | "custom";
const quickRanges: Record<Exclude<QuickRange, "custom">, () => { from?: string; to?: string }> = {
  all: () => ({}),
  today: () => ({ from: dayKey(0), to: dayKey(0) }),
  yesterday: () => ({ from: dayKey(-1), to: dayKey(-1) }),
  last3: () => ({ from: dayKey(-2), to: dayKey(0) }),
  next7: () => ({ from: dayKey(0), to: dayKey(6) }),
};

function formatDate(iso: string): string {
  const [y, m, d] = iso.split("-");
  return `${m}/${d}/${y}`;
}
function formatDateTime(iso: string | null): string {
  if (!iso) return "—";
  const d = new Date(iso);
  return `${d.toLocaleDateString()} ${d.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" })}`;
}

/** Filterable patient list. The Patients module page is list-only;
 * Administration › Patient List passes `manage` to add, edit, (soft) delete
 * and restore charts. Filters live in the URL so a filtered list can be
 * bookmarked or shared. */
export function PatientListPage({
  title = "Patient List",
  back,
  manage = false,
}: {
  title?: string;
  back?: { to: string; label: string } | null;
  manage?: boolean;
}) {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  // Creating, editing and deleting a chart are Scheduling-role actions
  // server-side (PatientsController.Create/Update/Delete/Restore).
  const canManage = manage && canAccess(user, RoleSets.Scheduling);
  const [adding, setAdding] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [added, setAdded] = useState<{ name: string; mrn: string } | null>(null);
  const [params, setParams] = useSearchParams();
  const filters: PatientDirectoryFilters = {
    search: params.get("q") ?? undefined,
    status: params.get("status") ? (Number(params.get("status")) as PatientStatus) : undefined,
    locationId: params.get("location") ?? undefined,
    appointmentFrom: params.get("from") ?? undefined,
    appointmentTo: params.get("to") ?? undefined,
    deleted: canManage && params.get("deleted") === "1" ? true : undefined,
    page: Number(params.get("page") ?? 1),
    pageSize: PAGE_SIZE,
  };

  // Functional update, so the debounced search can't overwrite a filter
  // changed while it was waiting. Any filter change returns to page 1.
  const setParam = (key: string, value: string | undefined) =>
    setParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        if (value) next.set(key, value);
        else next.delete(key);
        if (key !== "page") next.delete("page");
        return next;
      },
      { replace: true },
    );

  // Debounce typing into the search box before it reaches the URL/query.
  const [searchText, setSearchText] = useState(filters.search ?? "");
  useEffect(() => {
    const timer = window.setTimeout(() => {
      if ((filters.search ?? "") !== searchText.trim()) setParam("q", searchText.trim() || undefined);
    }, 300);
    return () => window.clearTimeout(timer);
  }, [searchText]);

  const refreshList = () => void queryClient.invalidateQueries({ queryKey: ["admin", "patient-directory"] });
  const remove = useMutation({
    mutationFn: (p: { id: string; name: string }) => deletePatient(p.id),
    onSuccess: (_d, p) => {
      showToast(`${p.name} deleted. Find them under "Deleted" to restore.`);
      refreshList();
    },
    onError: (e: Error) => showToast(e.message),
  });
  const restore = useMutation({
    mutationFn: (p: { id: string; name: string }) => restorePatient(p.id),
    onSuccess: (_d, p) => {
      showToast(`${p.name} restored.`);
      refreshList();
    },
    onError: (e: Error) => showToast(e.message),
  });

  const locations = useQuery({ queryKey: ["admin", "locations", true], queryFn: () => fetchLocations(true) });
  const directory = useQuery({
    queryKey: ["admin", "patient-directory", filters],
    queryFn: ({ signal }) => fetchPatientDirectory(filters, signal),
    placeholderData: keepPreviousData,
  });

  // Which quick button matches the URL's dates (none = custom dates).
  const quickRange = ((Object.keys(quickRanges) as (keyof typeof quickRanges)[]).find((key) => {
    const r = quickRanges[key]();
    return r.from === filters.appointmentFrom && r.to === filters.appointmentTo;
  }) ?? "custom") as QuickRange;

  const page = directory.data?.page ?? 1;
  const totalPages = directory.data ? Math.max(1, Math.ceil(directory.data.total / PAGE_SIZE)) : 1;
  const hasFilters = params.toString().replace(/(^|&)page=\d+/, "") !== "";
  const dateRangeInvalid = !!filters.appointmentFrom && !!filters.appointmentTo && filters.appointmentFrom > filters.appointmentTo;

  return (
    <div>
      <AdminPageHeader
        title={title}
        back={back}
        actions={
          canManage && (
            <button
              type="button"
              className="btn-primary"
              onClick={() => {
                setAdded(null);
                setAdding(true);
              }}
            >
              + Add patient
            </button>
          )
        }
      />

      {added && (
        <div className="mb-5 flex flex-wrap items-center justify-between gap-2 rounded-xl border border-success bg-success-light/50 px-5 py-3 text-sm">
          <span>
            Added <strong>{added.name}</strong> — MRN {added.mrn}.
          </span>
          <button type="button" className="btn-secondary" onClick={() => setAdded(null)}>
            Dismiss
          </button>
        </div>
      )}

      {editingId && (
        <EditPatientDialog
          patientId={editingId}
          onClose={() => setEditingId(null)}
          onSaved={(saved) => {
            setEditingId(null);
            showToast(`${saved.fullName} saved.`);
            refreshList();
            void queryClient.invalidateQueries({ queryKey: ["admin", "patient", saved.id] });
          }}
        />
      )}

      {adding && (
        <AddPatientDialog
          onCancel={() => setAdding(false)}
          onCreated={(p) => {
            setAdding(false);
            setAdded({ name: p.fullName, mrn: p.medicalRecordNumber });
            void queryClient.invalidateQueries({ queryKey: ["admin", "patient-directory"] });
          }}
        />
      )}

      <div className="list-toolbar">
        <SegmentedButtons
          label="Appointment dates"
          value={quickRange}
          onChange={(v) => {
            if (v === "custom") return; // no button for it; shown when dates are typed
            const range = quickRanges[v]();
            setParams(
              (prev) => {
                const next = new URLSearchParams(prev);
                for (const [key, value] of [["from", range.from], ["to", range.to]] as const) {
                  if (value) next.set(key, value);
                  else next.delete(key);
                }
                next.delete("page");
                return next;
              },
              { replace: true },
            );
          }}
          options={[
            { value: "all", label: "All" },
            { value: "today", label: "Today" },
            { value: "yesterday", label: "Yesterday" },
            { value: "last3", label: "Last 3 Days" },
            { value: "next7", label: "Next 7 Days" },
          ]}
        />
        <SegmentedButtons
          label="Patient status"
          value={filters.deleted ? "deleted" : filters.status === undefined ? "all" : String(filters.status)}
          onChange={(v) =>
            setParams(
              (prev) => {
                const next = new URLSearchParams(prev);
                next.delete("page");
                next.delete("status");
                next.delete("deleted");
                if (v === "deleted") next.set("deleted", "1");
                else if (v !== "all") next.set("status", v);
                return next;
              },
              { replace: true },
            )
          }
          options={[
            { value: "all", label: "All" },
            ...Object.entries(STATUS_LABELS).map(([value, label]) => ({ value, label })),
            ...(canManage ? [{ value: "deleted", label: "Deleted" }] : []),
          ]}
        />
        <label className="toolbar-label">
          Location
          <select
            className="toolbar-select"
            value={filters.locationId ?? ""}
            onChange={(e) => setParam("location", e.target.value || undefined)}
          >
            <option value="">...All Locations...</option>
            {locations.data?.map((l) => (
              <option key={l.id} value={l.id}>
                {l.name}
                {l.isActive ? "" : " (inactive)"}
              </option>
            ))}
          </select>
        </label>
        <button type="button" className="btn-refresh ml-auto" disabled={directory.isFetching} onClick={() => void directory.refetch()}>
          {directory.isFetching ? "Refreshing…" : "Refresh"}
        </button>
      </div>
      <div className="list-toolbar">
        <input
          type="search"
          aria-label="Search patients"
          className="toolbar-select w-64"
          placeholder="Search name or MRN"
          value={searchText}
          onChange={(e) => setSearchText(e.target.value)}
        />
        <label className="toolbar-label">
          From
          <input
            type="date"
            className="toolbar-select"
            value={filters.appointmentFrom ?? ""}
            onChange={(e) => setParam("from", e.target.value || undefined)}
          />
        </label>
        <label className="toolbar-label">
          To
          <input
            type="date"
            className="toolbar-select"
            value={filters.appointmentTo ?? ""}
            onChange={(e) => setParam("to", e.target.value || undefined)}
          />
        </label>
        <button
          type="button"
          className="btn-refresh"
          disabled={!hasFilters}
          onClick={() => {
            setSearchText("");
            setParams(new URLSearchParams(), { replace: true });
          }}
        >
          Clear filters
        </button>
      </div>
      <p className="mb-3 text-xs text-text-muted">
        Dates filter to patients with a (non-cancelled) appointment in that range — at the chosen location, if one is
        picked. Without dates, a location matches patients whose primary clinic it is or who have any appointment there.
      </p>

      {dateRangeInvalid && <p className="alert-error mb-4">“Appointment from” is after “Appointment to”.</p>}

      <div className="list-wrap">
        {directory.isLoading && <p className="p-5 text-text-muted">Loading…</p>}
        {directory.isError && <p className="alert-error m-5">{directory.error.message}</p>}
        {directory.data && directory.data.items.length === 0 && (
          <p className="p-5 text-text-muted">No patients match these filters.</p>
        )}
        {directory.data && directory.data.items.length > 0 && (
          <table className={`data-table ${directory.isFetching ? "opacity-60" : ""}`}>
            <thead>
              <tr>
                <th className="cell-menu">
                  <span className="sr-only">Actions</span>
                </th>
                <th>MRN</th>
                <th>Name</th>
                <th>Date of birth</th>
                <th>Phone</th>
                <th>Primary location</th>
                <th>Status</th>
                <th>Last visit</th>
                <th>Next appointment</th>
              </tr>
            </thead>
            <tbody>
              {directory.data.items.map((p) => (
                <tr key={p.id}>
                  <td className="cell-menu">
                    <RowMenu
                      label={`Actions for ${p.fullName}`}
                      items={
                        filters.deleted
                          ? [{ label: "Restore patient", onSelect: () => restore.mutate({ id: p.id, name: p.fullName }) }]
                          : [
                              ...(canManage ? [{ label: "Edit patient", onSelect: () => setEditingId(p.id) }] : []),
                              {
                                label: "View appointments",
                                to: `/schedule?view=list&listRange=month&location=all&patient=${encodeURIComponent(p.medicalRecordNumber)}`,
                              },
                              { label: "Patient documentation", to: `/patients/${p.id}/documentation` },
                              { label: "Send message", to: `/admin/messages?patient=${p.id}` },
                              ...(canManage
                                ? [
                                    {
                                      label: "Delete patient",
                                      danger: true,
                                      onSelect: () => {
                                        if (
                                          window.confirm(
                                            `Delete ${p.fullName} (${p.medicalRecordNumber})? The chart is hidden from lists but kept, and can be restored from "Deleted".`,
                                          )
                                        )
                                          remove.mutate({ id: p.id, name: p.fullName });
                                      },
                                    },
                                  ]
                                : []),
                            ]
                      }
                    />
                  </td>
                  <td data-label="MRN">
                    <Link
                      className="table-link"
                      to={`/schedule?view=list&listRange=month&location=all&patient=${encodeURIComponent(p.medicalRecordNumber)}`}
                    >
                      {p.medicalRecordNumber}
                    </Link>
                  </td>
                  <td data-label="Name">
                    {canManage && !filters.deleted ? (
                      <button type="button" className="table-link text-left" onClick={() => setEditingId(p.id)}>
                        {p.fullName}
                      </button>
                    ) : (
                      p.fullName
                    )}
                  </td>
                  <td data-label="Date of birth">
                    {formatDate(p.dateOfBirth)} <span className="text-text-muted">({p.age})</span>
                  </td>
                  <td data-label="Phone">{p.phone}</td>
                  <td data-label="Primary location">{p.primaryLocationName ?? "—"}</td>
                  <td data-label="Status">{STATUS_LABELS[p.status]}</td>
                  <td data-label="Last visit">{formatDateTime(p.lastVisitAt)}</td>
                  <td data-label="Next appointment">{formatDateTime(p.nextAppointmentAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {directory.data && directory.data.total > 0 && (
        <div className="mt-4 flex flex-wrap items-center justify-between gap-2 text-sm text-text-muted">
          <span>
            {directory.data.total} patient{directory.data.total === 1 ? "" : "s"} · page {page} of {totalPages}
          </span>
          <div className="flex gap-2">
            <button
              type="button"
              className="btn-secondary"
              disabled={page <= 1}
              onClick={() => setParam("page", String(page - 1))}
            >
              ‹ Previous
            </button>
            <button
              type="button"
              className="btn-secondary"
              disabled={page >= totalPages}
              onClick={() => setParam("page", String(page + 1))}
            >
              Next ›
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
