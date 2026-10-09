import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useToast } from "../../../components/Toast";
import { DecimalInput } from "../measurements/DecimalInput";
import {
  deleteOutcomeScore,
  fetchPatientOutcomes,
  recordOutcomeScore,
} from "./api";
import { outcomesKey, useOutcomeDefinitions } from "./queries";
import {
  compare,
  formatDate,
  formatScore,
  interpret,
  scoreResponses,
  totalError,
  type ItemResponse,
  type OutcomeDefinition,
  type OutcomeScore,
} from "./model";
import { TrendChart } from "./TrendChart";

/** Outcome measures for a patient: record today's measure (item by item,
 * scored as you go) and compare every measure's baseline, previous and
 * current score with its trend and history. */
export function OutcomesWorkspace({
  patientId,
  noteId = null,
  serviceDate = null,
  readOnly,
}: {
  patientId: string;
  noteId?: string | null;
  serviceDate?: string | null;
  readOnly: boolean;
}) {
  const definitions = useOutcomeDefinitions();
  const scores = useQuery({
    queryKey: outcomesKey(patientId),
    queryFn: () => fetchPatientOutcomes(patientId),
  });
  const defs = definitions.data ?? [];

  return (
    <div className="space-y-4">
      {!readOnly && noteId && serviceDate && defs.length > 0 && (
        <OutcomeEntryForm
          definitions={defs}
          patientId={patientId}
          noteId={noteId}
          serviceDate={serviceDate}
        />
      )}
      {(definitions.isLoading || scores.isLoading) && (
        <p className="text-text-muted">Loading…</p>
      )}
      {definitions.error && (
        <p className="alert-error">{definitions.error.message}</p>
      )}
      {scores.data && defs.length > 0 && (
        <OutcomeComparison
          definitions={defs}
          scores={scores.data}
          noteId={noteId}
          readOnly={readOnly}
          patientId={patientId}
        />
      )}
    </div>
  );
}

function OutcomeEntryForm({
  definitions,
  patientId,
  noteId,
  serviceDate,
}: {
  definitions: OutcomeDefinition[];
  patientId: string;
  noteId: string;
  serviceDate: string;
}) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const [measure, setMeasure] = useState(definitions[0].measure);
  const [byItem, setByItem] = useState(true);
  const [responses, setResponses] = useState<ItemResponse[]>([]);
  const [total, setTotal] = useState<number | null>(null);
  const [notes, setNotes] = useState("");
  const [touched, setTouched] = useState(false);
  const d = definitions.find((x) => x.measure === measure) ?? definitions[0];

  const result = byItem ? scoreResponses(d, responses) : null;
  const totalProblem = byItem ? null : totalError(d, total);
  const score = byItem ? result!.score : totalProblem ? null : total;

  const reset = () => {
    setResponses([]);
    setTotal(null);
    setNotes("");
    setTouched(false);
  };
  const save = useMutation({
    mutationFn: () =>
      recordOutcomeScore({
        patientId,
        noteId,
        measure: d.measure,
        measuredOn: serviceDate,
        score: byItem ? null : total,
        maximumScore: d.scoreMax,
        notes: notes.trim() || null,
        itemResponses: byItem
          ? responses.filter((r) => r.value !== null)
          : null,
      }),
    onSuccess: (s) => {
      showToast(`${d.abbreviation} recorded: ${s.score}.`);
      reset();
      void queryClient.invalidateQueries({ queryKey: outcomesKey(patientId) });
    },
    onError: (e: Error) => showToast(e.message),
  });

  const setItem = (key: string, patch: Partial<ItemResponse>) => {
    setTouched(true);
    setResponses((rs) => {
      const existing = rs.find((r) => r.key === key) ?? {
        key,
        value: null,
        label: null,
      };
      return [...rs.filter((r) => r.key !== key), { ...existing, ...patch }];
    });
  };
  const response = (key: string) => responses.find((r) => r.key === key);
  const answered = responses.filter((r) => r.value !== null).length;

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    setTouched(true);
    if (score !== null) save.mutate();
  };

  return (
    <form
      onSubmit={onSubmit}
      aria-label="Record outcome measure"
      className="space-y-3 rounded-md border border-border p-3"
    >
      <div className="flex flex-wrap items-end gap-3">
        <label className="block min-w-[14rem] flex-1">
          <span className="font-bold text-[#333]">Measure</span>
          <select
            className="field-input mt-1"
            value={measure}
            onChange={(e) => {
              setMeasure(Number(e.target.value));
              setByItem(true);
              reset();
            }}
          >
            {definitions.map((m) => (
              <option key={m.measure} value={m.measure}>
                {m.name} ({m.abbreviation})
              </option>
            ))}
          </select>
        </label>
        <fieldset className="flex flex-wrap gap-3">
          <legend className="sr-only">Entry</legend>
          <label className="flex items-center gap-1">
            <input
              type="radio"
              checked={byItem}
              onChange={() => setByItem(true)}
            />
            Item by item
          </label>
          <label className="flex items-center gap-1">
            <input
              type="radio"
              checked={!byItem}
              onChange={() => setByItem(false)}
            />
            Total only
          </label>
        </fieldset>
      </div>
      <p className="text-text-muted">
        {d.description} Administer the official form; enter each item's score
        here.
      </p>

      {byItem ? (
        <div className="grid gap-x-4 gap-y-2 md:grid-cols-2">
          {d.items.map((item, i) => {
            const r = response(item.key);
            return (
              <div key={item.key} className="min-w-0">
                {d.itemsNamedByPatient ? (
                  <div className="grid grid-cols-[1fr_7rem] gap-2">
                    <label className="block">
                      <span className="text-[#333]">{item.label}</span>
                      <input
                        className="field-input mt-1"
                        placeholder="Activity the patient named"
                        value={r?.label ?? ""}
                        onChange={(e) =>
                          setItem(item.key, { label: e.target.value })
                        }
                      />
                    </label>
                    <ItemValue
                      d={d}
                      label={`${item.label} rating`}
                      visibleLabel="Rating"
                      value={r?.value ?? null}
                      onValue={(v) => setItem(item.key, { value: v })}
                    />
                  </div>
                ) : (
                  <ItemValue
                    d={d}
                    label={item.label}
                    visibleLabel={`${i + 1}. ${item.label}`}
                    value={r?.value ?? null}
                    onValue={(v) => setItem(item.key, { value: v })}
                  />
                )}
              </div>
            );
          })}
        </div>
      ) : (
        <label className="block max-w-xs">
          <span className="font-bold text-[#333]">
            Total (
            {d.scoreMax !== null ? `${d.scoreMin}–${d.scoreMax}` : d.unit})
          </span>
          <DecimalInput
            className="field-input mt-1"
            value={total}
            onValue={(v) => {
              setTouched(true);
              setTotal(v);
            }}
          />
        </label>
      )}

      <label className="block">
        <span className="text-[#333]">Notes (optional)</span>
        <input
          className="field-input mt-1"
          placeholder="e.g. assistive device, test conditions"
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
        />
      </label>

      <div
        role="status"
        className="rounded-md bg-surface-muted p-2 text-[#333]"
      >
        {score !== null ? (
          <>
            <strong>
              {d.abbreviation} score: {formatScore(d, score)}
            </strong>{" "}
            — {byItem ? result!.interpretation : interpret(d, score)}
          </>
        ) : byItem ? (
          `${answered} of ${d.items.length} items answered.`
        ) : (
          "Enter the total score."
        )}
        {touched && (result?.errors.length || totalProblem) ? (
          <ul className="mt-1 list-disc pl-5 text-danger">
            {(result?.errors ?? [totalProblem!]).map((e) => (
              <li key={e}>{e}</li>
            ))}
          </ul>
        ) : null}
      </div>
      <button
        type="submit"
        className="btn-primary"
        disabled={score === null || save.isPending}
      >
        {save.isPending ? "Saving…" : `Record ${d.abbreviation}`}
      </button>
    </form>
  );
}

function ItemValue({
  d,
  label,
  visibleLabel,
  value,
  onValue,
}: {
  d: OutcomeDefinition;
  label: string;
  visibleLabel: string;
  value: number | null;
  onValue: (v: number | null) => void;
}) {
  return (
    <label className="block">
      <span className="text-[#333]">{visibleLabel}</span>
      {d.options.length ? (
        <select
          aria-label={label}
          className="field-input mt-1"
          value={value ?? ""}
          onChange={(e) =>
            onValue(e.target.value === "" ? null : Number(e.target.value))
          }
        >
          <option value="">—</option>
          {d.options.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
      ) : (
        <DecimalInput
          aria-label={label}
          className="field-input mt-1"
          placeholder={`${d.itemMin}–${d.itemMax}`}
          value={value}
          onValue={onValue}
        />
      )}
    </label>
  );
}

/** Per measure: baseline, previous and current scores, change from
 * baseline and from the previous score, interpretation, trend and history. */
export function OutcomeComparison({
  definitions,
  scores,
  noteId,
  readOnly,
  patientId,
}: {
  definitions: OutcomeDefinition[];
  scores: OutcomeScore[];
  noteId: string | null;
  readOnly: boolean;
  patientId: string;
}) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const remove = useMutation({
    mutationFn: (id: string) => deleteOutcomeScore(id),
    onSuccess: () =>
      void queryClient.invalidateQueries({ queryKey: outcomesKey(patientId) }),
    onError: (e: Error) => showToast(e.message),
  });
  const compared = definitions
    .map((d) => ({ d, c: compare(d, scores) }))
    .filter((x) => x.c !== null);
  if (!compared.length)
    return (
      <p className="text-text-muted">
        No outcome measures recorded for this patient yet.
      </p>
    );

  return (
    <ul className="space-y-3">
      {compared.map(({ d, c }) => {
        const cmp = c!;
        const thisVisit = noteId
          ? cmp.history.find((s) => s.noteId === noteId)
          : undefined;
        return (
          <li
            key={d.measure}
            className="rounded-md border border-border p-3"
            aria-label={d.name}
          >
            <p className="font-bold text-[#333]">
              {d.name} ({d.abbreviation})
              {thisVisit && (
                <span className="ml-2 font-normal text-text-muted">
                  recorded this visit
                </span>
              )}
            </p>
            <dl className="mt-2 grid grid-cols-2 gap-x-4 gap-y-2 sm:grid-cols-4">
              <Stat label="Baseline" score={cmp.baseline} d={d} />
              <Stat label="Previous" score={cmp.previous} d={d} />
              <Stat label="Current" score={cmp.current} d={d} />
              <div>
                <dt className="text-text-muted">Change from baseline</dt>
                <dd className={cmp.fromBaseline?.tone ?? ""}>
                  {cmp.fromBaseline?.text ?? "—"}
                </dd>
              </div>
            </dl>
            <p className="mt-2 text-[#333]">
              <span className="text-text-muted">Interpretation: </span>
              {cmp.current.interpretation ?? interpret(d, cmp.current.score)}
              {cmp.fromPrevious && cmp.history.length > 2 && (
                <span className="text-text-muted">
                  {" "}
                  · since previous:{" "}
                  <span className={cmp.fromPrevious.tone}>
                    {cmp.fromPrevious.text}
                  </span>
                </span>
              )}
            </p>
            {(d.meaningfulChangeNote || d.reference) && (
              <p className="text-text-muted">
                {[d.meaningfulChangeNote, d.reference]
                  .filter(Boolean)
                  .join(" ")}
              </p>
            )}
            <div className="mt-2">
              <TrendChart definition={d} history={cmp.history} />
            </div>
            <details className="mt-2">
              <summary className="cursor-pointer text-primary">
                Score history ({cmp.history.length})
              </summary>
              <ul className="mt-2 space-y-1">
                {[...cmp.history].reverse().map((s) => (
                  <li
                    key={s.id}
                    className="flex flex-wrap items-baseline gap-x-2 border-b border-border pb-1"
                  >
                    <span className="font-bold">
                      {formatDate(s.measuredOn)}
                    </span>
                    <span>{formatScore(d, s.score)}</span>
                    <span className="text-text-muted">
                      {s.interpretation}
                      {s.itemResponses ? "" : " · total only"}
                      {s.notes ? ` · ${s.notes}` : ""}
                    </span>
                    {!readOnly &&
                      noteId &&
                      s.noteId === noteId &&
                      !s.isLocked && (
                        <button
                          type="button"
                          className="ml-auto text-danger underline"
                          disabled={remove.isPending}
                          onClick={() => remove.mutate(s.id)}
                        >
                          Remove
                          <span className="sr-only">
                            {" "}
                            {d.abbreviation} score of {formatDate(s.measuredOn)}
                          </span>
                        </button>
                      )}
                  </li>
                ))}
              </ul>
            </details>
          </li>
        );
      })}
    </ul>
  );
}

function Stat({
  label,
  score,
  d,
}: {
  label: string;
  score: OutcomeScore | null;
  d: OutcomeDefinition;
}) {
  return (
    <div>
      <dt className="text-text-muted">{label}</dt>
      <dd className="text-[#333]">
        {score ? (
          <>
            <strong>{formatScore(d, score.score)}</strong>{" "}
            <span className="text-text-muted">
              {formatDate(score.measuredOn)}
            </span>
          </>
        ) : (
          "—"
        )}
      </dd>
    </div>
  );
}
