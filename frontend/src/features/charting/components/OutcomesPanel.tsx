import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useToast } from "../../../components/Toast";
import { fetchOutcomes, recordOutcome } from "../api";
import { describeChange, OUTCOME_MEASURES } from "../presets";
import type { OutcomeScore } from "../types";

const measureInfo = (m: number) => OUTCOME_MEASURES.find((x) => x.value === m);
const formatDate = (iso: string) => {
  const [y, mo, d] = iso.split("-");
  return `${mo}/${d}/${y}`;
};

/** Standardized outcome measures: record today's score and see it against
 * the previous one for the same tool. */
export function OutcomesPanel({
  patientId,
  noteId,
  serviceDate,
  readOnly,
}: {
  patientId: string;
  noteId: string;
  serviceDate: string;
  readOnly: boolean;
}) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const [measure, setMeasure] = useState(0);
  const [score, setScore] = useState("");

  const scores = useQuery({
    queryKey: ["chart", "outcomes", patientId],
    queryFn: () => fetchOutcomes(patientId),
  });
  const record = useMutation({
    mutationFn: () =>
      recordOutcome({
        patientId,
        noteId,
        measure,
        measuredOn: serviceDate,
        score: Number(score),
        maximumScore: measureInfo(measure)?.max ?? null,
        notes: null,
      }),
    onSuccess: () => {
      setScore("");
      void queryClient.invalidateQueries({
        queryKey: ["chart", "outcomes", patientId],
      });
    },
    onError: (e: Error) => showToast(e.message),
  });

  const max = measureInfo(measure)?.max ?? null;
  const scoreInvalid =
    score !== "" &&
    (Number.isNaN(Number(score)) ||
      Number(score) < 0 ||
      (max !== null && Number(score) > max));

  // Newest first per measure: [latest, previous].
  const byMeasure = new Map<number, OutcomeScore[]>();
  for (const s of [...(scores.data ?? [])].sort((a, b) =>
    b.measuredOn.localeCompare(a.measuredOn),
  )) {
    byMeasure.set(s.measure, [...(byMeasure.get(s.measure) ?? []), s]);
  }

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (score !== "" && !scoreInvalid) record.mutate();
  };

  return (
    <div className="space-y-4">
      {!readOnly && (
        <form
          onSubmit={onSubmit}
          className="flex flex-wrap items-end gap-3"
          aria-label="Record outcome measure"
        >
          <label className="text-[#333]">
            <span className="font-bold">Measure</span>
            <select
              className="field-input mt-1"
              value={measure}
              onChange={(e) => setMeasure(Number(e.target.value))}
            >
              {OUTCOME_MEASURES.map((m) => (
                <option key={m.value} value={m.value}>
                  {m.label}
                </option>
              ))}
            </select>
          </label>
          <label className="text-[#333]">
            <span className="font-bold">Score</span>
            <span className="ml-1 text-text-muted">
              {max !== null
                ? `(0–${max} ${measureInfo(measure)?.unit})`
                : `(${measureInfo(measure)?.unit})`}
            </span>
            <input
              inputMode="decimal"
              className="field-input mt-1 w-40"
              value={score}
              onChange={(e) => setScore(e.target.value.replace(/[^0-9.]/g, ""))}
            />
          </label>
          <button
            type="submit"
            className="btn-primary"
            disabled={score === "" || scoreInvalid || record.isPending}
          >
            {record.isPending ? "Saving…" : "Record score"}
          </button>
          {scoreInvalid && (
            <span className="text-danger">
              Enter a score from 0{max !== null ? ` to ${max}` : ""}.
            </span>
          )}
        </form>
      )}

      {scores.isLoading && <p className="text-text-muted">Loading…</p>}
      {scores.data && byMeasure.size === 0 && (
        <p className="text-text-muted">
          No outcome measures recorded for this patient yet.
        </p>
      )}
      {byMeasure.size > 0 && (
        <div className="list-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>Measure</th>
                <th>Latest</th>
                <th>Previous</th>
                <th>Change</th>
              </tr>
            </thead>
            <tbody>
              {[...byMeasure.entries()].map(([m, list]) => {
                const [latest, prev] = list;
                const change = prev
                  ? describeChange(m, prev.score, latest.score)
                  : null;
                return (
                  <tr key={m}>
                    <td data-label="Measure">{measureInfo(m)?.label ?? m}</td>
                    <td data-label="Latest">
                      {latest.score}
                      {latest.maximumScore !== null
                        ? `/${latest.maximumScore}`
                        : ""}{" "}
                      · {formatDate(latest.measuredOn)}
                      {latest.noteId === noteId && (
                        <span className="ml-1 text-text-muted">
                          (this visit)
                        </span>
                      )}
                    </td>
                    <td data-label="Previous">
                      {prev
                        ? `${prev.score} · ${formatDate(prev.measuredOn)}`
                        : "—"}
                    </td>
                    <td data-label="Change" className={change?.tone}>
                      {change?.text ?? "—"}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
