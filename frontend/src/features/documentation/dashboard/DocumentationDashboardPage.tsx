import { useState, type ReactNode } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import { openAppointmentEncounter } from "../../encounter/api";
import { fetchScheduleSettings } from "../../schedule/api";
import { PatientPicker } from "../../schedule/components/PatientPicker";
import type { SchedulePatient } from "../../schedule/types";
import {
  DocumentationStatus,
  DocumentationStatusLabels,
  NoteTypeLabels,
  TEMPLATE_NOTE_TYPES,
} from "../../workflow/types";
import {
  emptyFilters,
  fetchDocumentationDashboard,
  type DashboardDeadline,
  type DashboardFilters,
  type DashboardItem,
  type DocumentationDashboard,
} from "./api";

const formatDate = (iso: string | null | undefined) => {
  if (!iso) return "—";
  const [y, m, d] = iso.slice(0, 10).split("-");
  return `${m}/${d}/${y}`;
};
const formatTime = (iso: string) =>
  new Date(iso).toLocaleTimeString("en-US", {
    hour: "numeric",
    minute: "2-digit",
  });

const STATUS_FILTERS = [
  DocumentationStatus.NotStarted,
  DocumentationStatus.Draft,
  DocumentationStatus.ReadyToSign,
  DocumentationStatus.CosignRequired,
  DocumentationStatus.InReview,
  DocumentationStatus.ReturnedForCorrection,
  DocumentationStatus.Signed,
  DocumentationStatus.Cosigned,
];

type ItemKey =
  | "todaysSchedule"
  | "notStarted"
  | "readyToSign"
  | "drafts"
  | "awaitingCosign"
  | "returned"
  | "overdue"
  | "recentlySigned";
type DeadlineKey = "progressNotesDue" | "reevaluationsDue" | "expiringPlans";

const SECTIONS: {
  key: ItemKey | DeadlineKey;
  count: keyof DocumentationDashboard["counts"];
  title: string;
  empty: string;
  alert?: boolean;
}[] = [
  {
    key: "todaysSchedule",
    count: "todaysVisits",
    title: "Today’s scheduled patients",
    empty: "No visits scheduled today.",
  },
  {
    key: "notStarted",
    count: "notStarted",
    title: "Notes not started",
    empty: "Every visit so far has a note.",
  },
  {
    key: "readyToSign",
    count: "readyToSign",
    title: "Ready to sign",
    empty: "No notes are waiting for a signature.",
  },
  { key: "drafts", count: "drafts", title: "Draft notes", empty: "No drafts." },
  {
    key: "awaitingCosign",
    count: "awaitingCosign",
    title: "Requiring cosignature",
    empty: "No notes are waiting for a PT.",
  },
  {
    key: "returned",
    count: "returned",
    title: "Returned for correction",
    empty: "No notes were returned.",
  },
  {
    key: "overdue",
    count: "overdue",
    title: "Overdue",
    empty: "Nothing is overdue.",
    alert: true,
  },
  {
    key: "progressNotesDue",
    count: "progressNotesDue",
    title: "Progress notes due",
    empty: "No progress notes are due.",
  },
  {
    key: "reevaluationsDue",
    count: "reevaluationsDue",
    title: "Re-evaluations due",
    empty: "No re-evaluations are due.",
  },
  {
    key: "expiringPlans",
    count: "expiringPlans",
    title: "Expiring plans of care",
    empty: "No plans of care end in the next 30 days.",
  },
  {
    key: "recentlySigned",
    count: "recentlySigned",
    title: "Recently signed",
    empty: "Nothing signed recently.",
  },
];
const isDeadline = (key: string): key is DeadlineKey =>
  key === "progressNotesDue" ||
  key === "reevaluationsDue" ||
  key === "expiringPlans";

/** Documentation across the schedule: today's visits, notes by state,
 * overdue work and upcoming deadlines, with filters. */
export function DocumentationDashboardPage() {
  const { user } = useAuth();
  const settings = useQuery({
    queryKey: ["schedule", "settings"],
    queryFn: fetchScheduleSettings,
    staleTime: 5 * 60_000,
  });
  const providers = settings.data?.providers ?? [];
  const mine = providers.find((p) => p.userId === user?.id);
  // Clinicians start on their own notes; "All providers" shows everyone's.
  const [providerChoice, setProviderChoice] = useState("mine");
  const [patient, setPatient] = useState<SchedulePatient | null>(null);
  const [filters, setFilters] = useState<DashboardFilters>(emptyFilters);
  const providerId =
    providerChoice === "mine" ? (mine?.id ?? "") : providerChoice;
  const effective = { ...filters, providerId, patientId: patient?.id ?? "" };
  const dashboard = useQuery({
    queryKey: ["documentation", "dashboard", effective],
    queryFn: () => fetchDocumentationDashboard(effective),
    enabled: settings.isFetched,
  });
  const set = (k: keyof DashboardFilters, v: string) =>
    setFilters((f) => ({ ...f, [k]: v }));
  const filtered =
    providerChoice !== "mine" ||
    !!patient ||
    Object.values(filters).some(Boolean);
  // Phones start with the filters folded away; wider screens show them.
  const [showFilters, setShowFilters] = useState(
    () =>
      typeof window === "undefined" ||
      !window.matchMedia ||
      window.matchMedia("(min-width: 640px)").matches,
  );

  const d = dashboard.data;
  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold text-[#1565b8]">
          Documentation Dashboard
        </h1>
        <p className="text-text-muted">
          {d ? `Today is ${formatDate(d.today)}. ` : ""}Notes for the visits on
          the schedule, what still needs signing, and upcoming deadlines.
        </p>
      </div>

      <button
        type="button"
        className="btn-refresh sm:hidden"
        aria-expanded={showFilters}
        aria-controls="dash-filters"
        onClick={() => setShowFilters((v) => !v)}
      >
        {showFilters ? "Hide filters" : `Filters${filtered ? " (on)" : ""}`}
      </button>
      <form
        id="dash-filters"
        aria-label="Filters"
        hidden={!showFilters}
        className="grid gap-3 rounded-lg border border-border bg-white p-3 sm:grid-cols-2 lg:grid-cols-3"
        onSubmit={(e) => e.preventDefault()}
      >
        <label className="block">
          <span className="font-bold text-[#333]">Provider</span>
          <select
            className="field-input mt-1"
            value={providerChoice}
            onChange={(e) => setProviderChoice(e.target.value)}
          >
            {mine && <option value="mine">My patients ({mine.name})</option>}
            {!mine && <option value="mine">All providers</option>}
            {mine && <option value="">All providers</option>}
            {providers
              .filter((p) => p.isActive && p.id !== mine?.id)
              .map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                  {p.credentials ? `, ${p.credentials}` : ""}
                </option>
              ))}
          </select>
        </label>
        <div>
          <span className="font-bold text-[#333]">Patient</span>
          <div className="mt-1">
            <PatientPicker
              selected={patient}
              onSelect={setPatient}
              autoFocus={false}
            />
          </div>
        </div>
        <label className="block">
          <span className="font-bold text-[#333]">Note type</span>
          <select
            className="field-input mt-1"
            value={filters.noteType}
            onChange={(e) => set("noteType", e.target.value)}
          >
            <option value="">All note types</option>
            {TEMPLATE_NOTE_TYPES.map((t) => (
              <option key={t} value={t}>
                {NoteTypeLabels[t]}
              </option>
            ))}
          </select>
        </label>
        <label className="block">
          <span className="font-bold text-[#333]">Status</span>
          <select
            className="field-input mt-1"
            value={filters.status}
            onChange={(e) => set("status", e.target.value)}
          >
            <option value="">All statuses</option>
            {STATUS_FILTERS.map((s) => (
              <option key={s} value={s}>
                {DocumentationStatusLabels[s]}
              </option>
            ))}
          </select>
        </label>
        <div className="grid grid-cols-2 gap-2">
          <label className="block">
            <span className="font-bold text-[#333]">From</span>
            <input
              type="date"
              className="field-input mt-1"
              value={filters.from}
              onChange={(e) => set("from", e.target.value)}
            />
          </label>
          <label className="block">
            <span className="font-bold text-[#333]">To</span>
            <input
              type="date"
              className="field-input mt-1"
              value={filters.to}
              onChange={(e) => set("to", e.target.value)}
            />
          </label>
        </div>
        <div className="flex items-end">
          <button
            type="button"
            className="btn-refresh"
            disabled={!filtered}
            onClick={() => {
              setFilters(emptyFilters);
              setPatient(null);
              setProviderChoice("mine");
            }}
          >
            Clear filters
          </button>
        </div>
      </form>

      {dashboard.error && (
        <p className="alert-error">{dashboard.error.message}</p>
      )}
      {(settings.isLoading || dashboard.isLoading) && (
        <p className="text-text-muted">Loading…</p>
      )}

      {d && (
        <>
          <nav aria-label="Summary">
            <ul className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-6">
              {SECTIONS.map((s) => {
                const n = d.counts[s.count];
                return (
                  <li key={s.key}>
                    <a
                      href={`#dash-${s.key}`}
                      className={`block rounded-lg border p-2 no-underline ${s.alert && n > 0 ? "border-danger bg-danger-light" : "border-border bg-white"}`}
                    >
                      <span
                        className={`block text-2xl font-bold ${s.alert && n > 0 ? "text-danger" : "text-[#1565b8]"}`}
                      >
                        {n}
                      </span>
                      <span className="block text-[#333]">{s.title}</span>
                    </a>
                  </li>
                );
              })}
            </ul>
          </nav>

          {SECTIONS.map((s) => (
            <Section
              key={s.key}
              id={`dash-${s.key}`}
              title={s.title}
              count={d.counts[s.count]}
            >
              {isDeadline(s.key) ? (
                <DeadlineList items={d[s.key]} empty={s.empty} />
              ) : (
                <ItemList
                  items={d[s.key]}
                  empty={s.empty}
                  showTime={s.key === "todaysSchedule"}
                />
              )}
            </Section>
          ))}
          <p className="text-text-muted">
            A note is overdue when it isn’t signed by the end of the day after
            the visit. Progress notes are due at your clinic’s visit count (the
            10th visit by default); plans of care are listed 30 days before
            their certification ends.
          </p>
        </>
      )}
    </div>
  );
}

function Section({
  id,
  title,
  count,
  children,
}: {
  id: string;
  title: string;
  count: number;
  children: ReactNode;
}) {
  return (
    <section
      id={id}
      aria-labelledby={`${id}-title`}
      className="scroll-mt-28 rounded-lg border border-border bg-white p-3"
    >
      <h2 id={`${id}-title`} className="text-xl font-bold text-[#1565b8]">
        {title} <span className="text-text-muted">({count})</span>
      </h2>
      <div className="mt-2">{children}</div>
    </section>
  );
}

function ItemList({
  items,
  empty,
  showTime,
}: {
  items: DashboardItem[];
  empty: string;
  showTime: boolean;
}) {
  const [all, setAll] = useState(false);
  if (!items.length) return <p className="text-text-muted">{empty}</p>;
  return (
    <>
      <ul className="divide-y divide-border">
        {(all ? items : items.slice(0, PAGE)).map((i) => (
          <li
            key={`${i.noteId ?? ""}${i.appointmentId ?? ""}`}
            className="flex flex-wrap items-center justify-between gap-2 py-2"
          >
            <div className="min-w-0">
              <p className="text-[#333]">
                {showTime && i.startsAt && (
                  <span className="mr-2 font-bold">
                    {formatTime(i.startsAt)}
                  </span>
                )}
                <Link
                  to={`/patients/${i.patientId}/documentation`}
                  className="font-bold text-primary hover:underline"
                >
                  {i.patientName}
                </Link>{" "}
                <span className="text-text-muted">{i.medicalRecordNumber}</span>
              </p>
              <p className="text-text-muted">
                {i.noteType != null ? NoteTypeLabels[i.noteType] : "Visit"} ·{" "}
                {formatDate(i.date)}
                {i.providerName ? ` · ${i.providerName}` : ""}
                {i.authorName ? ` · by ${i.authorName}` : ""}
                {i.signedAt
                  ? ` · signed ${new Date(i.signedAt).toLocaleDateString("en-US")}`
                  : ""}
              </p>
              {i.detail && <p className="text-[#333]">{i.detail}</p>}
            </div>
            <div className="flex flex-wrap items-center gap-2">
              {i.overdue && (
                <span className="rounded bg-danger-light px-2 text-danger">
                  Overdue · {i.daysSinceService} days
                </span>
              )}
              {i.documentationStatus != null && (
                <span className="rounded bg-surface-muted px-2 text-[#333]">
                  {DocumentationStatusLabels[i.documentationStatus]}
                </span>
              )}
              <ItemAction item={i} />
            </div>
          </li>
        ))}
      </ul>
      <ShowAll
        total={items.length}
        all={all}
        onToggle={() => setAll((v) => !v)}
      />
    </>
  );
}

const PAGE = 10;

function ShowAll({
  total,
  all,
  onToggle,
}: {
  total: number;
  all: boolean;
  onToggle: () => void;
}) {
  if (total <= PAGE) return null;
  return (
    <button
      type="button"
      className="mt-2 text-primary underline"
      onClick={onToggle}
    >
      {all ? "Show fewer" : `Show all ${total}`}
    </button>
  );
}

function ItemAction({ item }: { item: DashboardItem }) {
  const navigate = useNavigate();
  const open = useMutation({
    mutationFn: (missedVisit: boolean) =>
      openAppointmentEncounter(item.appointmentId!, missedVisit),
    onSuccess: (r) => navigate(`/chart/${r.noteId}`),
  });
  if (item.noteId)
    return (
      <Link className="btn-refresh" to={`/chart/${item.noteId}`}>
        Open note
      </Link>
    );
  if (!item.appointmentId) return null;
  const missed = item.documentationStatus == null;
  return (
    <>
      <button
        type="button"
        className="btn-primary"
        disabled={open.isPending}
        onClick={() => open.mutate(missed)}
      >
        {open.isPending
          ? "Opening…"
          : missed
            ? "Missed-visit note"
            : "Start note"}
      </button>
      {open.isError && (
        <span className="text-danger">{open.error.message}</span>
      )}
    </>
  );
}

function DeadlineList({
  items,
  empty,
}: {
  items: DashboardDeadline[];
  empty: string;
}) {
  const [all, setAll] = useState(false);
  if (!items.length) return <p className="text-text-muted">{empty}</p>;
  return (
    <>
      <ul className="divide-y divide-border">
        {(all ? items : items.slice(0, PAGE)).map((i) => (
          <li
            key={`${i.patientId}-${i.noteType}`}
            className="flex flex-wrap items-center justify-between gap-2 py-2"
          >
            <div className="min-w-0">
              <p className="text-[#333]">
                <Link
                  to={`/patients/${i.patientId}/documentation`}
                  className="font-bold text-primary hover:underline"
                >
                  {i.patientName}
                </Link>{" "}
                <span className="text-text-muted">{i.medicalRecordNumber}</span>
              </p>
              <p className="text-text-muted">
                {NoteTypeLabels[i.noteType]}
                {i.dueDate ? ` · ${formatDate(i.dueDate)}` : ""} · {i.detail}
              </p>
            </div>
            {i.overdue ? (
              <span className="rounded bg-danger-light px-2 text-danger">
                Due now
              </span>
            ) : (
              <span className="rounded bg-warning-light px-2 text-[#333]">
                Due soon
              </span>
            )}
          </li>
        ))}
      </ul>
      <ShowAll
        total={items.length}
        all={all}
        onToggle={() => setAll((v) => !v)}
      />
    </>
  );
}
