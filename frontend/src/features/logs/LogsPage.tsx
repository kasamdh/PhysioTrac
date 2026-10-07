import { useCallback, useEffect, useState } from "react";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import { RowMenu } from "../../components/RowMenu";
import { SegmentedButtons } from "../../components/SegmentedButtons";
import { useToast } from "../../components/Toast";
import { AdminPageHeader } from "../admin/AdminPageHeader";
import { fetchAuditLogs } from "./api";
import { LOG_CATEGORIES, categoryLabel, type AuditLogFilters, type AuditLogRow } from "./types";

const PAGE_SIZE = 50;

/** yyyy-mm-dd for a day offset from today (browser time zone). */
function dayKey(offset: number): string {
  const d = new Date();
  d.setDate(d.getDate() + offset);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}
type Range = "today" | "yesterday" | "7" | "30" | "custom";
const ranges: Record<Exclude<Range, "custom">, () => { from: string; to: string }> = {
  today: () => ({ from: dayKey(0), to: dayKey(0) }),
  yesterday: () => ({ from: dayKey(-1), to: dayKey(-1) }),
  "7": () => ({ from: dayKey(-6), to: dayKey(0) }),
  "30": () => ({ from: dayKey(-29), to: dayKey(0) }),
};

const formatAt = (iso: string) => {
  const d = new Date(iso);
  return `${d.toLocaleDateString("en-US")} ${d.toLocaleTimeString("en-US", { hour: "numeric", minute: "2-digit", second: "2-digit" })}`;
};

function csvCell(v: string | null | undefined): string {
  const s = v ?? "";
  return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
}

/** Administration › Logs: every change, sign-in and screen opened in the
 * organization, newest first. Filters live in the URL. */
export function LogsPage() {
  const { showToast } = useToast();
  const [params, setParams] = useSearchParams();
  const [details, setDetails] = useState<AuditLogRow | null>(null);
  const [exporting, setExporting] = useState(false);

  const filters: AuditLogFilters = {
    from: params.get("from") ?? dayKey(0),
    to: params.get("to") ?? params.get("from") ?? dayKey(0),
    userId: params.get("user") ?? undefined,
    category: params.get("category") ?? undefined,
    search: params.get("q") ?? undefined,
    page: Number(params.get("page") ?? 1),
    pageSize: PAGE_SIZE,
  };
  const setParam = (changes: Record<string, string | undefined>) =>
    setParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        for (const [k, v] of Object.entries(changes)) {
          if (v) next.set(k, v);
          else next.delete(k);
        }
        if (!("page" in changes)) next.delete("page");
        return next;
      },
      { replace: true },
    );

  // Debounced search box.
  const [searchText, setSearchText] = useState(filters.search ?? "");
  useEffect(() => {
    const t = window.setTimeout(() => {
      if ((filters.search ?? "") !== searchText.trim()) setParam({ q: searchText.trim() || undefined });
    }, 300);
    return () => window.clearTimeout(t);
  }, [searchText]);

  const logs = useQuery({
    queryKey: ["audit-logs", filters],
    queryFn: ({ signal }) => fetchAuditLogs(filters, signal),
    placeholderData: keepPreviousData,
  });
  const closeDetails = useCallback(() => setDetails(null), []);

  const range =
    ((Object.keys(ranges) as (keyof typeof ranges)[]).find((k) => {
      const r = ranges[k]();
      return r.from === filters.from && r.to === filters.to;
    }) as Range | undefined) ?? "custom";
  const data = logs.data;
  const totalPages = data ? Math.max(1, Math.ceil(data.total / PAGE_SIZE)) : 1;

  const exportCsv = async () => {
    setExporting(true);
    try {
      const all = await fetchAuditLogs({ ...filters, page: 1, pageSize: 5000 });
      const header = ["Time", "User", "User ID", "Activity", "Category", "Item", "Patient", "MRN", "IP address", "Action code"];
      const lines = all.items.map((r) =>
        [formatAt(r.at), r.userName, r.userLogin, r.description, categoryLabel(r.category), r.objectType, r.patientName, r.patientMrn, r.ipAddress, r.action]
          .map(csvCell)
          .join(","),
      );
      const blob = new Blob([[header.join(","), ...lines].join("\r\n")], { type: "text/csv;charset=utf-8" });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `activity-log_${filters.from}_to_${filters.to}.csv`;
      a.click();
      URL.revokeObjectURL(url);
      if (all.total > all.items.length) showToast(`Exported the newest ${all.items.length} of ${all.total} entries.`);
    } catch (e) {
      showToast((e as Error).message);
    } finally {
      setExporting(false);
    }
  };

  return (
    <div>
      <AdminPageHeader title="Logs" />

      <div className="list-toolbar">
        <SegmentedButtons
          label="Date range"
          value={range}
          onChange={(v) => {
            if (v === "custom") return;
            setParam(ranges[v]());
          }}
          options={[
            { value: "today", label: "Today" },
            { value: "yesterday", label: "Yesterday" },
            { value: "7", label: "Last 7 Days" },
            { value: "30", label: "Last 30 Days" },
          ]}
        />
        <label className="toolbar-label">
          From
          <input type="date" className="toolbar-select" value={filters.from} onChange={(e) => setParam({ from: e.target.value || undefined })} />
        </label>
        <label className="toolbar-label">
          To
          <input type="date" className="toolbar-select" value={filters.to} onChange={(e) => setParam({ to: e.target.value || undefined })} />
        </label>
        <button type="button" className="btn-refresh ml-auto" disabled={logs.isFetching} onClick={() => void logs.refetch()}>
          {logs.isFetching ? "Refreshing…" : "Refresh"}
        </button>
      </div>
      <div className="list-toolbar">
        <label className="toolbar-label">
          User
          <select className="toolbar-select" value={filters.userId ?? ""} onChange={(e) => setParam({ user: e.target.value || undefined })}>
            <option value="">...All Users...</option>
            {data?.users.map((u) => (
              <option key={u.id} value={u.id}>
                {u.name || u.userName}
                {u.userName ? ` (${u.userName})` : ""}
              </option>
            ))}
          </select>
        </label>
        <label className="toolbar-label">
          Category
          <select className="toolbar-select" value={filters.category ?? ""} onChange={(e) => setParam({ category: e.target.value || undefined })}>
            <option value="">...All Categories...</option>
            {LOG_CATEGORIES.map((c) => (
              <option key={c.value} value={c.value}>
                {c.label}
              </option>
            ))}
          </select>
        </label>
        <input
          type="search"
          aria-label="Search logs"
          className="toolbar-select w-64"
          placeholder="Search activity, user, patient, MRN, IP"
          value={searchText}
          onChange={(e) => setSearchText(e.target.value)}
        />
        <button type="button" className="btn-refresh" disabled={exporting || !data?.total} onClick={() => void exportCsv()}>
          {exporting ? "Exporting…" : "Export CSV"}
        </button>
      </div>

      {logs.isError && <p className="alert-error mb-3">{logs.error.message}</p>}

      <div className="list-wrap">
        {logs.isLoading && <p className="p-5 text-text-muted">Loading…</p>}
        {data && data.items.length === 0 && <p className="p-5 text-text-muted">No activity matches these filters.</p>}
        {data && data.items.length > 0 && (
          <table className={`data-table ${logs.isFetching ? "opacity-60" : ""}`}>
            <thead>
              <tr>
                <th className="cell-menu">
                  <span className="sr-only">Actions</span>
                </th>
                <th>Time</th>
                <th>User</th>
                <th>Activity</th>
                <th>Category</th>
                <th>Patient</th>
                <th>IP address</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((r) => (
                <tr key={r.id}>
                  <td className="cell-menu">
                    <RowMenu label={`Actions for log entry at ${formatAt(r.at)}`} items={[{ label: "Show details", onSelect: () => setDetails(r) }]} />
                  </td>
                  <td data-label="Time" className="whitespace-nowrap">
                    {formatAt(r.at)}
                  </td>
                  <td data-label="User">
                    <div>
                      {r.userName}
                      {r.userLogin && <span className="block text-text-muted">{r.userLogin}</span>}
                    </div>
                  </td>
                  <td data-label="Activity">
                    <button type="button" className="table-link text-left" onClick={() => setDetails(r)}>
                      {r.description}
                    </button>
                  </td>
                  <td data-label="Category">{categoryLabel(r.category)}</td>
                  <td data-label="Patient">
                    {r.patientName ? (
                      <div>
                        {r.patientName}
                        {r.patientMrn && <span className="block text-text-muted">{r.patientMrn}</span>}
                      </div>
                    ) : (
                      "—"
                    )}
                  </td>
                  <td data-label="IP address">{r.ipAddress ?? "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {data && data.total > 0 && (
        <div className="mt-4 flex flex-wrap items-center justify-between gap-2 text-text-muted">
          <span>
            {data.total} entr{data.total === 1 ? "y" : "ies"} · page {data.page} of {totalPages} · times in your browser’s time zone
          </span>
          <div className="flex gap-2">
            <button type="button" className="btn-refresh" disabled={data.page <= 1} onClick={() => setParam({ page: String(data.page - 1) })}>
              ‹ Previous
            </button>
            <button type="button" className="btn-refresh" disabled={data.page >= totalPages} onClick={() => setParam({ page: String(data.page + 1) })}>
              Next ›
            </button>
          </div>
        </div>
      )}

      {details && <LogDetailsDialog row={details} onClose={closeDetails} />}
    </div>
  );
}

function LogDetailsDialog({ row, onClose }: { row: AuditLogRow; onClose: () => void }) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [onClose]);
  let metadata = row.metadataJson;
  try {
    metadata = JSON.stringify(JSON.parse(row.metadataJson), null, 2);
  } catch {
    // shown as stored
  }
  const field = (label: string, value: string | null | undefined) => (
    <div className="grid grid-cols-1 gap-1 sm:grid-cols-[10rem_1fr] sm:gap-5">
      <dt className="font-bold text-[#333] sm:text-right">{label}</dt>
      <dd className="min-w-0 break-words">{value || "—"}</dd>
    </div>
  );
  return (
    <div className="modal-overlay" onClick={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="log-details-title"
        onClick={(e) => e.stopPropagation()}
        className="flex max-h-[92vh] w-full max-w-2xl flex-col overflow-hidden rounded-lg bg-white shadow-2xl"
      >
        <h2 id="log-details-title" className="border-b border-border px-6 py-4 text-3xl text-[#333]">
          Log Entry
        </h2>
        <dl className="space-y-3 overflow-y-auto px-6 py-5">
          {field("Time", formatAt(row.at))}
          {field("User", row.userLogin ? `${row.userName} (${row.userLogin})` : row.userName)}
          {field("Activity", row.description)}
          {field("Category", categoryLabel(row.category))}
          {field("Item", row.objectType)}
          {field("Item ID", row.objectId)}
          {field("Patient", row.patientName ? `${row.patientName}${row.patientMrn ? ` (${row.patientMrn})` : ""}` : null)}
          {field("IP address", row.ipAddress)}
          {field("Action code", row.action)}
          <div className="grid grid-cols-1 gap-1 sm:grid-cols-[10rem_1fr] sm:gap-5">
            <dt className="font-bold text-[#333] sm:text-right">Details</dt>
            <dd className="min-w-0">
              <pre className="overflow-x-auto rounded bg-surface-muted p-3 text-sm whitespace-pre-wrap">{metadata}</pre>
            </dd>
          </div>
        </dl>
        <div className="flex justify-end border-t border-border px-6 py-4">
          <button type="button" className="btn-refresh" onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
}
