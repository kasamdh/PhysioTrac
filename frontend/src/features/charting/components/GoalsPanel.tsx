import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useToast } from "../../../components/Toast";
import { fetchGoals, updateGoalProgress } from "../api";
import { ApproveGoalButton } from "../../encounter/components/ClinicalPanels";
import type { Goal } from "../types";

const STATUS: Record<number, string> = {
  0: "Draft",
  1: "Active",
  2: "Met",
  3: "Discontinued",
  4: "Not started",
  5: "Partially met",
};

/** Functional goals with baseline → current → target; the therapist updates
 * today's value and the progress bar follows (FunctionalGoal.ProgressPercent). */
export function GoalsPanel({
  patientId,
  readOnly,
}: {
  patientId: string;
  readOnly: boolean;
}) {
  const goals = useQuery({
    queryKey: ["chart", "goals", patientId],
    queryFn: () => fetchGoals(patientId),
  });
  const shown = (goals.data ?? []).filter(
    (g) => g.status === 0 || g.status === 1 || g.status === 4,
  );

  if (goals.isLoading) return <p className="text-text-muted">Loading…</p>;
  if (shown.length === 0)
    return (
      <p className="text-text-muted">
        No active goals. Goals are set during the evaluation (plan of care).
      </p>
    );
  return (
    <ul className="space-y-3">
      {shown.map((g) => (
        <GoalRow
          key={g.id}
          goal={g}
          patientId={patientId}
          readOnly={readOnly}
        />
      ))}
    </ul>
  );
}

function GoalRow({
  goal,
  patientId,
  readOnly,
}: {
  goal: Goal;
  patientId: string;
  readOnly: boolean;
}) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const [value, setValue] = useState("");
  const save = useMutation({
    mutationFn: () => updateGoalProgress(goal.id, Number(value)),
    onSuccess: () => {
      setValue("");
      void queryClient.invalidateQueries({
        queryKey: ["chart", "goals", patientId],
      });
    },
    onError: (e: Error) => showToast(e.message),
  });
  const pct = goal.progressPercent ?? 0;

  return (
    <li className="rounded-md border border-border p-3">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <p className="font-bold text-[#333]">
          {goal.functionalTask}
          <span className="ml-2 font-normal text-text-muted">
            {goal.term === 1 ? "Long-term" : "Short-term"} ·{" "}
            {STATUS[goal.status]} · by {goal.targetDate}
          </span>
        </p>
        <p className="text-[#333]">
          {goal.baselineValue} → <strong>{goal.currentValue ?? "—"}</strong> →{" "}
          {goal.targetValue} {goal.unit}
        </p>
      </div>
      <p className="text-text-muted">
        {goal.functionalLimitation} · measured by {goal.measurementMethod}
      </p>
      <div className="mt-2 flex items-center gap-3">
        <div
          className="h-3 flex-1 overflow-hidden rounded-full bg-surface-muted"
          role="progressbar"
          aria-valuenow={pct}
          aria-valuemin={0}
          aria-valuemax={100}
          aria-label={`${goal.functionalTask} progress`}
        >
          <div
            className={`h-full ${pct >= 100 ? "bg-success" : "bg-primary"}`}
            style={{ width: `${pct}%` }}
          />
        </div>
        <span className="w-14 text-right text-[#333]">
          {goal.progressPercent === null ? "—" : `${pct}%`}
        </span>
      </div>
      {!readOnly && goal.status === 0 && (
        <div className="mt-2">
          <ApproveGoalButton goalId={goal.id} patientId={patientId} />
        </div>
      )}
      {!readOnly && (goal.status === 1 || goal.status === 4) && (
        <div className="mt-2 flex flex-wrap items-center gap-2">
          <label className="toolbar-label">
            Today
            <input
              inputMode="decimal"
              aria-label={`Today's value for ${goal.functionalTask}`}
              className="toolbar-select w-28"
              placeholder={goal.unit}
              value={value}
              onChange={(e) =>
                setValue(e.target.value.replace(/[^0-9.-]/g, ""))
              }
            />
          </label>
          <button
            type="button"
            className="btn-refresh"
            disabled={
              value === "" || Number.isNaN(Number(value)) || save.isPending
            }
            onClick={() => save.mutate()}
          >
            {save.isPending ? "Saving…" : "Update progress"}
          </button>
        </div>
      )}
    </li>
  );
}
