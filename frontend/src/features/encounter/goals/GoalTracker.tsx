import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useToast } from "../../../components/Toast";
import { DecimalInput } from "../measurements/DecimalInput";
import {
  approvePatientGoal,
  createPatientGoal,
  editGoal,
  fetchGoalHistory,
  fetchPatientGoals,
  goalsKey,
  type GoalDefinition,
} from "./api";
import {
  formatDate,
  GoalStatus,
  GoalStatusLabels,
  HistoryKindLabels,
  isOpen,
  narrative,
  progressPercent,
  RECORDABLE,
  statement,
  wordingAdvice,
  type Goal,
  type GoalProgressRow,
} from "./model";

const todayIso = () => new Date().toISOString().slice(0, 10);

/** Copies the goal's definition into a progress row (the server refreshes
 * it on save; it's here so the narrative reads right before saving). */
const withGoal = (row: GoalProgressRow, g: Goal): GoalProgressRow => ({
  ...row,
  term: g.term,
  functionalTask: g.functionalTask,
  functionalLimitation: g.functionalLimitation,
  baselineValue: g.baselineValue,
  targetValue: g.targetValue,
  unit: g.unit,
  measurementMethod: g.measurementMethod,
  targetDate: g.targetDate,
  previousValue: g.currentValue,
});

/** Goal tracking inside an encounter: this visit's value, status and
 * comment for each goal (saved with the note, applied to the goal when the
 * note is signed), goal history, adding and editing goals, and inserting
 * the selected goals' progress into the note's narrative. */
export function GoalTracker({
  patientId,
  value,
  onChange,
  readOnly,
  canApprove,
  insertTarget,
}: {
  patientId: string;
  value: GoalProgressRow[];
  onChange: (rows: GoalProgressRow[]) => void;
  readOnly: boolean;
  canApprove: boolean;
  insertTarget?: { label: string; insert: (text: string) => void } | null;
}) {
  const goals = useQuery({
    queryKey: goalsKey(patientId),
    queryFn: () => fetchPatientGoals(patientId),
    enabled: !readOnly,
  });
  const { showToast } = useToast();
  const [adding, setAdding] = useState(false);

  if (readOnly) return <GoalProgressSummary rows={value} />;
  if (goals.isLoading) return <p className="text-text-muted">Loading…</p>;
  if (goals.error) return <p className="alert-error">{goals.error.message}</p>;

  const all = goals.data ?? [];
  const open = all.filter((g) => isOpen(g.status));
  const closed = all.filter((g) => !isOpen(g.status));
  const rowFor = (id: string) => value.find((r) => r.goalId === id);

  const upsert = (g: Goal, patch: Partial<GoalProgressRow>) => {
    const existing = rowFor(g.id);
    const base: GoalProgressRow = existing ?? {
      goalId: g.id,
      currentValue: null,
      status: g.status,
      comment: null,
      includeInNarrative: true,
    };
    let next = withGoal({ ...base, ...patch }, g);
    // A first value moves a not-started goal to in progress.
    if (
      !existing &&
      patch.currentValue != null &&
      patch.status === undefined &&
      g.status === GoalStatus.NotStarted
    )
      next = { ...next, status: GoalStatus.InProgress };
    onChange(
      existing
        ? value.map((r) => (r.goalId === g.id ? next : r))
        : [...value, next],
    );
  };
  const clear = (id: string) => onChange(value.filter((r) => r.goalId !== id));

  const text = narrative(
    value.map((r) => {
      const g = all.find((x) => x.id === r.goalId);
      return g ? withGoal(r, g) : r;
    }),
  );

  return (
    <div className="space-y-3">
      {open.length === 0 && (
        <p className="text-text-muted">
          No open goals. Add a functional, measurable goal below.
        </p>
      )}
      <ul className="space-y-3">
        {open.map((g) => (
          <GoalCard
            key={g.id}
            goal={g}
            row={rowFor(g.id)}
            onPatch={(p) => upsert(g, p)}
            onClear={() => clear(g.id)}
            canApprove={canApprove}
            patientId={patientId}
          />
        ))}
      </ul>
      {closed.length > 0 && (
        <details>
          <summary className="cursor-pointer text-primary">
            Met, partially met and discontinued goals ({closed.length})
          </summary>
          <ul className="mt-2 space-y-3">
            {closed.map((g) => (
              <GoalCard
                key={g.id}
                goal={g}
                row={rowFor(g.id)}
                onPatch={(p) => upsert(g, p)}
                onClear={() => clear(g.id)}
                canApprove={canApprove}
                patientId={patientId}
              />
            ))}
          </ul>
        </details>
      )}

      {insertTarget && (
        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            className="btn-refresh"
            disabled={!text}
            onClick={() => {
              insertTarget.insert(text);
              showToast(`Goal progress added to ${insertTarget.label}.`);
            }}
          >
            Insert goal progress into “{insertTarget.label}”
          </button>
          <span className="text-text-muted">
            Uses the goals ticked “Include in note”.
          </span>
        </div>
      )}

      {adding ? (
        <GoalForm patientId={patientId} onDone={() => setAdding(false)} />
      ) : (
        <button
          type="button"
          className="btn-refresh"
          onClick={() => setAdding(true)}
        >
          + Add goal
        </button>
      )}
    </div>
  );
}

function GoalCard({
  goal: g,
  row,
  onPatch,
  onClear,
  canApprove,
  patientId,
}: {
  goal: Goal;
  row: GoalProgressRow | undefined;
  onPatch: (p: Partial<GoalProgressRow>) => void;
  onClear: () => void;
  canApprove: boolean;
  patientId: string;
}) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const [editing, setEditing] = useState(false);
  const [showHistory, setShowHistory] = useState(false);
  const approve = useMutation({
    mutationFn: () => approvePatientGoal(g.id),
    onSuccess: () =>
      void queryClient.invalidateQueries({ queryKey: goalsKey(patientId) }),
    onError: (e: Error) => showToast(e.message),
  });
  const today = row?.currentValue ?? null;
  const pct = progressPercent(
    g.baselineValue,
    g.targetValue,
    today ?? g.currentValue,
  );
  const status = row?.status ?? g.status;
  const reachedTarget =
    today != null &&
    (g.targetValue >= g.baselineValue
      ? today >= g.targetValue
      : today <= g.targetValue);
  const id = `goal-${g.id}`;

  return (
    <li
      className={`rounded-md border p-3 ${row ? "border-primary" : "border-border"}`}
      aria-label={g.functionalTask}
    >
      <p className="font-bold text-[#333]">
        {g.term === 1 ? "Long-term" : "Short-term"}: {g.functionalTask}
      </p>
      <p className="text-text-muted">
        {statement(g)}
        {g.version > 1 ? ` · version ${g.version}` : ""}
      </p>
      {g.comments && <p className="text-text-muted">Comments: {g.comments}</p>}
      <p className="mt-1 text-[#333]">
        Baseline {g.baselineValue} → previous{" "}
        <strong>{g.currentValue ?? "—"}</strong> → today{" "}
        <strong>{today ?? "—"}</strong> → target {g.targetValue} {g.unit}
      </p>
      <div className="mt-2 flex items-center gap-3">
        <div
          className="h-3 flex-1 overflow-hidden rounded-full bg-surface-muted"
          role="progressbar"
          aria-valuenow={pct ?? 0}
          aria-valuemin={0}
          aria-valuemax={100}
          aria-label={`${g.functionalTask} progress`}
        >
          <div
            className={`h-full ${(pct ?? 0) >= 100 ? "bg-success" : "bg-primary"}`}
            style={{ width: `${pct ?? 0}%` }}
          />
        </div>
        <span className="w-14 text-right text-[#333]">
          {pct === null ? "—" : `${pct}%`}
        </span>
      </div>

      {g.status === GoalStatus.Draft ? (
        <div className="mt-2 flex flex-wrap items-center gap-2">
          <span className="text-text-muted">
            Draft — a PT approves it before progress is tracked.
          </span>
          {canApprove && (
            <button
              type="button"
              className="btn-refresh"
              disabled={approve.isPending}
              onClick={() => approve.mutate()}
            >
              Approve goal
            </button>
          )}
        </div>
      ) : (
        <div className="mt-2 grid gap-2 sm:grid-cols-[9rem_14rem_1fr]">
          <label className="block">
            <span className="text-[#333]">Today ({g.unit})</span>
            <DecimalInput
              id={`${id}-value`}
              className="field-input mt-1"
              value={today}
              onValue={(v) => onPatch({ currentValue: v })}
            />
          </label>
          <label className="block">
            <span className="text-[#333]">Status</span>
            <select
              className="field-input mt-1"
              value={status}
              onChange={(e) => onPatch({ status: Number(e.target.value) })}
            >
              {RECORDABLE.map((s) => (
                <option key={s} value={s}>
                  {GoalStatusLabels[s]}
                </option>
              ))}
            </select>
          </label>
          <label className="block">
            <span className="text-[#333]">Comment</span>
            <input
              className="field-input mt-1"
              value={row?.comment ?? ""}
              onChange={(e) => onPatch({ comment: e.target.value || null })}
            />
          </label>
        </div>
      )}
      {status === GoalStatus.Met && today != null && !reachedTarget && (
        <p className="mt-1 text-warning">
          Marked met, but today's value hasn't reached the target.
        </p>
      )}

      <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-2">
        {row && (
          <>
            <label className="flex items-center gap-1">
              <input
                type="checkbox"
                checked={row.includeInNarrative}
                onChange={(e) =>
                  onPatch({ includeInNarrative: e.target.checked })
                }
              />
              Include in note
            </label>
            <button
              type="button"
              className="text-primary underline"
              onClick={onClear}
            >
              Clear this visit's entry
            </button>
          </>
        )}
        <button
          type="button"
          className="text-primary underline"
          aria-expanded={showHistory}
          onClick={() => setShowHistory((v) => !v)}
        >
          {showHistory ? "Hide history" : "History"}
        </button>
        <button
          type="button"
          className="text-primary underline"
          onClick={() => setEditing((v) => !v)}
        >
          {editing ? "Cancel edit" : "Edit goal"}
        </button>
      </div>
      {showHistory && <GoalHistoryList goalId={g.id} />}
      {editing && (
        <GoalForm
          patientId={patientId}
          goal={g}
          onDone={() => setEditing(false)}
        />
      )}
    </li>
  );
}

/** Every version and progress entry of a goal, newest first. */
export function GoalHistoryList({ goalId }: { goalId: string }) {
  const history = useQuery({
    queryKey: ["goal-history", goalId],
    queryFn: () => fetchGoalHistory(goalId),
  });
  if (history.isLoading) return <p className="text-text-muted">Loading…</p>;
  if (history.error)
    return <p className="alert-error">{history.error.message}</p>;
  const rows = [...(history.data ?? [])].reverse();
  if (!rows.length)
    return <p className="text-text-muted">No history recorded yet.</p>;
  return (
    <ol className="mt-2 space-y-1 border-l-2 border-border pl-3">
      {rows.map((h) => (
        <li key={h.id}>
          <span className="font-bold">{HistoryKindLabels[h.kind]}</span>{" "}
          <span className="text-text-muted">
            {formatDate(h.recordedAt)}
            {h.recordedByName ? ` · ${h.recordedByName}` : ""} · version{" "}
            {h.goalVersion}
          </span>
          <br />
          {h.kind === 3 ? (
            <>
              {h.currentValue != null &&
                `Value ${h.currentValue} ${h.snapshot.unit}`}
              {h.progressPercent != null && ` (${h.progressPercent}%)`} ·{" "}
              {GoalStatusLabels[h.status]}
              {h.noteServiceDate &&
                ` · note of ${formatDate(h.noteServiceDate)}`}
              {h.comment && ` — ${h.comment}`}
            </>
          ) : (
            <span className="text-[#333]">{statement(h.snapshot)}</span>
          )}
        </li>
      ))}
    </ol>
  );
}

/** Add a goal (saved as a draft for a PT to approve) or edit one (the
 * previous version stays in its history). */
export function GoalForm({
  patientId,
  goal,
  onDone,
}: {
  patientId: string;
  goal?: Goal;
  onDone: () => void;
}) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const [g, setG] = useState({
    term: String(goal?.term ?? 0),
    functionalTask: goal?.functionalTask ?? "",
    functionalLimitation: goal?.functionalLimitation ?? "",
    baselineValue: goal?.baselineValue ?? (null as number | null),
    targetValue: goal?.targetValue ?? (null as number | null),
    unit: goal?.unit ?? "",
    measurementMethod: goal?.measurementMethod ?? "",
    targetDate: goal?.targetDate ?? "",
    comments: goal?.comments ?? "",
  });
  const body = (): GoalDefinition => ({
    term: Number(g.term),
    functionalTask: g.functionalTask.trim(),
    functionalLimitation: g.functionalLimitation.trim(),
    baselineValue: g.baselineValue ?? 0,
    targetValue: g.targetValue ?? 0,
    unit: g.unit.trim(),
    measurementMethod: g.measurementMethod.trim(),
    targetDate: g.targetDate,
    comments: g.comments.trim() || null,
  });
  const save = useMutation({
    mutationFn: () =>
      goal ? editGoal(goal.id, body()) : createPatientGoal(patientId, body()),
    onSuccess: () => {
      showToast(goal ? "Goal updated." : "Goal added.");
      void queryClient.invalidateQueries({ queryKey: goalsKey(patientId) });
      if (goal)
        void queryClient.invalidateQueries({
          queryKey: ["goal-history", goal.id],
        });
      onDone();
    },
    onError: (e: Error) => showToast(e.message),
  });
  const ready =
    g.functionalTask.trim() &&
    g.baselineValue !== null &&
    g.targetValue !== null &&
    g.baselineValue !== g.targetValue &&
    g.unit.trim() &&
    g.targetDate;
  const advice = wordingAdvice(
    g.functionalTask,
    g.measurementMethod,
    g.targetDate,
    todayIso(),
  );
  const prefix = goal ? `edit-${goal.id}` : "new-goal";
  const text = (k: keyof typeof g, label: string, placeholder = "") => (
    <label className="block">
      <span className="font-bold text-[#333]">{label}</span>
      <input
        id={`${prefix}-${k}`}
        className="field-input mt-1"
        placeholder={placeholder}
        value={String(g[k] ?? "")}
        onChange={(e) => setG({ ...g, [k]: e.target.value })}
      />
    </label>
  );
  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (ready) save.mutate();
  };

  return (
    <form
      aria-label={goal ? "Edit goal" : "Add goal"}
      className="mt-2 grid gap-3 rounded-md border border-border p-3 md:grid-cols-2"
      onSubmit={onSubmit}
    >
      <label className="block">
        <span className="font-bold text-[#333]">Term</span>
        <select
          className="field-input mt-1"
          value={g.term}
          onChange={(e) => setG({ ...g, term: e.target.value })}
        >
          <option value="0">Short-term</option>
          <option value="1">Long-term</option>
        </select>
      </label>
      <label className="block">
        <span className="font-bold text-[#333]">Target date</span>
        <input
          type="date"
          className="field-input mt-1"
          value={g.targetDate}
          onChange={(e) => setG({ ...g, targetDate: e.target.value })}
        />
      </label>
      <div className="md:col-span-2">
        {text(
          "functionalTask",
          "Functional goal",
          "e.g. Climb 12 stairs reciprocally with one rail",
        )}
      </div>
      <div className="md:col-span-2">
        {text(
          "functionalLimitation",
          "Functional limitation",
          "e.g. Unable to climb stairs without pain",
        )}
      </div>
      <label className="block">
        <span className="font-bold text-[#333]">Baseline</span>
        <DecimalInput
          className="field-input mt-1"
          value={g.baselineValue}
          onValue={(v) => setG({ ...g, baselineValue: v })}
        />
      </label>
      <label className="block">
        <span className="font-bold text-[#333]">Target</span>
        <DecimalInput
          className="field-input mt-1"
          value={g.targetValue}
          onValue={(v) => setG({ ...g, targetValue: v })}
        />
      </label>
      {text("unit", "Unit", "e.g. stairs, sec, deg, points")}
      {text("measurementMethod", "How it's measured", "e.g. Stair count, LEFS")}
      <div className="md:col-span-2">
        {text("comments", "Therapist comments")}
      </div>
      {g.functionalTask.trim() && (
        <div
          className="rounded-md bg-surface-muted p-2 md:col-span-2"
          role="status"
        >
          <p className="text-[#333]">
            <span className="text-text-muted">Reads as: </span>
            {statement({
              term: Number(g.term),
              functionalTask: g.functionalTask,
              baselineValue: g.baselineValue ?? undefined,
              targetValue: g.targetValue ?? undefined,
              unit: g.unit,
              measurementMethod: g.measurementMethod,
              targetDate: g.targetDate || null,
            })}
          </p>
          {advice.length > 0 && (
            <ul className="mt-1 list-disc pl-5 text-warning">
              {advice.map((a) => (
                <li key={a}>{a}</li>
              ))}
            </ul>
          )}
        </div>
      )}
      {g.baselineValue !== null && g.baselineValue === g.targetValue && (
        <p className="text-danger md:col-span-2">
          The target must differ from the baseline.
        </p>
      )}
      {goal && (
        <p className="text-text-muted md:col-span-2">
          Saving creates version {goal.version + 1}; earlier versions stay in
          the goal's history and in notes that documented them.
        </p>
      )}
      <div className="flex gap-2 md:col-span-2">
        <button
          type="submit"
          className="btn-primary"
          disabled={!ready || save.isPending}
        >
          {save.isPending ? "Saving…" : goal ? "Save changes" : "Save goal"}
        </button>
        <button type="button" className="btn-refresh" onClick={onDone}>
          Cancel
        </button>
      </div>
    </form>
  );
}

/** A note's goal progress as documented (read-only, for signed notes and
 * the printed note). */
export function GoalProgressSummary({ rows }: { rows: GoalProgressRow[] }) {
  if (!rows.length)
    return (
      <p className="text-text-muted">No goal progress recorded on this note.</p>
    );
  return (
    <ul className="space-y-2">
      {rows.map((r) => {
        const pct = progressPercent(
          r.baselineValue ?? 0,
          r.targetValue ?? 0,
          r.currentValue,
        );
        return (
          <li
            key={r.goalId}
            className="break-inside-avoid rounded-md border border-border p-2"
          >
            <p className="font-bold text-[#333]">
              {r.term === 1 ? "Long-term" : "Short-term"}: {r.functionalTask}
            </p>
            <p className="text-[#333]">
              Baseline {r.baselineValue} → previous {r.previousValue ?? "—"} →
              this visit <strong>{r.currentValue ?? "—"}</strong> → target{" "}
              {r.targetValue} {r.unit} by {formatDate(r.targetDate)}
              {pct !== null ? ` · ${pct}%` : ""} · {GoalStatusLabels[r.status]}
            </p>
            {r.comment && <p className="text-text-muted">{r.comment}</p>}
          </li>
        );
      })}
    </ul>
  );
}

/** All of a patient's goals, read-only, with progress and history — for
 * the patient's documentation page. */
export function GoalsOverview({ patientId }: { patientId: string }) {
  const goals = useQuery({
    queryKey: goalsKey(patientId),
    queryFn: () => fetchPatientGoals(patientId),
  });
  const [open, setOpen] = useState<string | null>(null);
  if (goals.isLoading) return <p className="text-text-muted">Loading…</p>;
  if (goals.error) return <p className="alert-error">{goals.error.message}</p>;
  const list = [...(goals.data ?? [])].sort(
    (a, b) =>
      Number(!isOpen(a.status)) - Number(!isOpen(b.status)) ||
      a.targetDate.localeCompare(b.targetDate),
  );
  if (!list.length)
    return (
      <p className="text-text-muted">
        No goals yet. Goals are set during the evaluation.
      </p>
    );
  return (
    <ul className="space-y-2">
      {list.map((g) => {
        const pct = progressPercent(
          g.baselineValue,
          g.targetValue,
          g.currentValue,
        );
        return (
          <li key={g.id} className="rounded-md border border-border p-3">
            <p className="font-bold text-[#333]">
              {g.term === 1 ? "Long-term" : "Short-term"}: {g.functionalTask}
              <span className="ml-2 font-normal text-text-muted">
                {GoalStatusLabels[g.status]}
              </span>
            </p>
            <p className="text-[#333]">
              Baseline {g.baselineValue} → current{" "}
              <strong>{g.currentValue ?? "—"}</strong> → target {g.targetValue}{" "}
              {g.unit} by {formatDate(g.targetDate)}
              {pct !== null ? ` · ${pct}%` : ""}
            </p>
            {g.comments && <p className="text-text-muted">{g.comments}</p>}
            <button
              type="button"
              className="mt-1 text-primary underline"
              aria-expanded={open === g.id}
              onClick={() => setOpen(open === g.id ? null : g.id)}
            >
              {open === g.id ? "Hide history" : "History"}
              <span className="sr-only"> of {g.functionalTask}</span>
            </button>
            {open === g.id && <GoalHistoryList goalId={g.id} />}
          </li>
        );
      })}
    </ul>
  );
}
