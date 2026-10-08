import { useState } from "react";
import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { SpecialtyLabels } from "../../templates/types";
import { BodySideLabels } from "../bodychart/regions";
import { fetchSpecialTests, setSpecialTestFavorite } from "./api";
import { DecimalInput } from "./DecimalInput";
import { OutcomeLabels } from "./model";
import type {
  SpecialTest,
  SpecialTestDefinition,
  SpecialTestHistory,
} from "./types";

const fmtDate = (iso: string) => {
  const [y, m, d] = iso.split("-");
  return `${m}/${d}/${y}`;
};

/** Special tests from the clinic's library: search or pick a favorite, see
 * any contraindication warning first, then record the result (positive,
 * negative or not tested, and a number where the test has one), the
 * therapist's interpretation and comments. Last result is shown alongside.
 * Nothing here interprets results automatically. */
export function SpecialTestsPanel({
  value,
  onChange,
  readOnly,
  history,
  definitions,
}: {
  value: SpecialTest[];
  onChange: (next: SpecialTest[]) => void;
  readOnly: boolean;
  history: SpecialTestHistory[];
  /** Library entries by id, for tests loaded from the server. */
  definitions?: ReadonlyMap<string, SpecialTestDefinition>;
}) {
  const update = (i: number, patch: Partial<SpecialTest>) =>
    onChange(value.map((t, j) => (j === i ? { ...t, ...patch } : t)));
  const previous = (t: SpecialTest) =>
    history.find(
      (h) =>
        h.testName.toLowerCase() === t.testName.toLowerCase() &&
        (h.side ?? null) === (t.side ?? null),
    );

  return (
    <div className="space-y-3">
      {!readOnly && (
        <Library
          onPick={(d) =>
            onChange([
              ...value,
              {
                testName: d.name,
                definitionId: d.id,
                specialty: d.specialty,
                bodyRegion: d.bodyRegion,
                side: null,
                outcome: 0,
                numericValue: null,
                unit: d.unit,
                interpretation: null,
                comment: null,
                definition: d,
              },
            ])
          }
        />
      )}
      {value.length === 0 && (
        <p className="text-text-muted">No special tests recorded.</p>
      )}
      <ol className="space-y-2" aria-label="Special tests">
        {value.map((t, i) => {
          const prev = previous(t);
          const d =
            t.definition ??
            (t.definitionId ? definitions?.get(t.definitionId) : undefined);
          const numeric = d
            ? d.resultKind !== 0
            : t.numericValue != null || !!t.unit;
          const posNeg = d ? d.resultKind !== 1 : true;
          return (
            <li key={i} className="rounded-md border border-border p-3">
              <div className="flex flex-wrap items-baseline justify-between gap-2">
                <p className="font-bold text-[#333]">
                  {t.testName}
                  <span className="ml-2 font-normal text-text-muted">
                    {[t.bodyRegion, SpecialtyLabels[t.specialty]]
                      .filter(Boolean)
                      .join(" · ")}
                  </span>
                </p>
                {!readOnly && (
                  <button
                    type="button"
                    className="btn-refresh"
                    aria-label={`Remove ${t.testName}`}
                    onClick={() => onChange(value.filter((_, j) => j !== i))}
                  >
                    Remove
                  </button>
                )}
              </div>
              {d?.contraindicationWarning && !readOnly && (
                <p
                  role="note"
                  className="mt-1 rounded-md border border-danger bg-danger-light px-3 py-1 text-danger"
                >
                  <strong>Before testing:</strong> {d.contraindicationWarning}
                </p>
              )}
              {readOnly ? (
                <p className="mt-1 text-[#333]">
                  {t.side != null ? `${BodySideLabels[t.side]}: ` : ""}
                  {posNeg ? OutcomeLabels[t.outcome] : ""}
                  {t.numericValue != null
                    ? `${posNeg ? " · " : ""}${t.numericValue} ${t.unit ?? ""}`
                    : ""}
                  {t.interpretation ? ` · ${t.interpretation}` : ""}
                  {t.comment ? ` · ${t.comment}` : ""}
                </p>
              ) : (
                <div className="mt-2 grid grid-cols-2 gap-3 md:grid-cols-4">
                  <label className="block text-text-muted">
                    Side
                    <select
                      className="field-input mt-1"
                      value={t.side ?? ""}
                      onChange={(e) =>
                        update(i, {
                          side:
                            e.target.value === ""
                              ? null
                              : Number(e.target.value),
                        })
                      }
                    >
                      <option value="">—</option>
                      {Object.entries(BodySideLabels).map(([v, l]) => (
                        <option key={v} value={v}>
                          {l}
                        </option>
                      ))}
                    </select>
                  </label>
                  {posNeg && (
                    <div className="col-span-2 md:col-span-2">
                      <span
                        className="block text-text-muted"
                        id={`st-${i}-result`}
                      >
                        Result
                      </span>
                      <div
                        role="radiogroup"
                        aria-labelledby={`st-${i}-result`}
                        className="mt-1 flex flex-wrap gap-1"
                      >
                        {[1, 2, 0].map((o) => (
                          <button
                            key={o}
                            type="button"
                            role="radio"
                            aria-checked={t.outcome === o}
                            className="seg-btn min-h-11 rounded border border-border px-3"
                            onClick={() => update(i, { outcome: o })}
                          >
                            {OutcomeLabels[o]}
                          </button>
                        ))}
                      </div>
                    </div>
                  )}
                  {numeric && (
                    <label className="block text-text-muted">
                      Result ({t.unit ?? "value"})
                      <DecimalInput
                        className="field-input mt-1"
                        value={t.numericValue}
                        onValue={(v) => update(i, { numericValue: v })}
                      />
                    </label>
                  )}
                  <label className="col-span-2 block text-text-muted">
                    Interpretation (therapist)
                    <input
                      className="field-input mt-1"
                      maxLength={500}
                      value={t.interpretation ?? ""}
                      onChange={(e) =>
                        update(i, { interpretation: e.target.value || null })
                      }
                    />
                  </label>
                  <label className="col-span-2 block text-text-muted">
                    Comments
                    <input
                      className="field-input mt-1"
                      maxLength={1000}
                      value={t.comment ?? ""}
                      onChange={(e) =>
                        update(i, { comment: e.target.value || null })
                      }
                    />
                  </label>
                </div>
              )}
              {prev && (
                <p className="mt-1 text-text-muted">
                  Last result ({fmtDate(prev.serviceDate)}):{" "}
                  {OutcomeLabels[prev.outcome]}
                  {prev.numericValue != null
                    ? ` · ${prev.numericValue} ${prev.unit ?? ""}`
                    : ""}
                </p>
              )}
              {d?.interpretationGuide && !readOnly && (
                <details className="mt-1 text-text-muted">
                  <summary className="cursor-pointer">
                    Interpretation guide (reference only)
                  </summary>
                  <p className="mt-1">{d.interpretationGuide}</p>
                </details>
              )}
            </li>
          );
        })}
      </ol>
    </div>
  );
}

function Library({ onPick }: { onPick: (d: SpecialTestDefinition) => void }) {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [specialty, setSpecialty] = useState("");
  const [favoritesOnly, setFavoritesOnly] = useState(false);
  const filter = {
    search,
    specialty: specialty === "" ? null : Number(specialty),
    favoritesOnly,
  };
  const list = useQuery({
    queryKey: ["special-tests", filter],
    queryFn: () => fetchSpecialTests(filter),
    placeholderData: keepPreviousData,
  });
  const favorite = useMutation({
    mutationFn: (d: SpecialTestDefinition) =>
      setSpecialTestFavorite(d.id, !d.isFavorite),
    onSuccess: () =>
      void queryClient.invalidateQueries({ queryKey: ["special-tests"] }),
  });
  return (
    <div className="rounded-md border border-border p-3">
      <div className="flex flex-wrap items-end gap-2">
        <label className="block min-w-[14rem] flex-1">
          <span className="block text-text-muted">Find a test</span>
          <input
            type="search"
            className="field-input mt-1"
            placeholder="Name or body region, e.g. knee"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </label>
        <label className="block">
          <span className="block text-text-muted">Specialty</span>
          <select
            className="field-input mt-1"
            value={specialty}
            onChange={(e) => setSpecialty(e.target.value)}
          >
            <option value="">All</option>
            {Object.entries(SpecialtyLabels)
              .filter(([v]) => v !== "0")
              .map(([v, l]) => (
                <option key={v} value={v}>
                  {l}
                </option>
              ))}
          </select>
        </label>
        <label className="flex min-h-11 items-center gap-2">
          <input
            type="checkbox"
            className="h-5 w-5"
            checked={favoritesOnly}
            onChange={(e) => setFavoritesOnly(e.target.checked)}
          />
          Favorites only
        </label>
      </div>
      <ul
        className="mt-2 max-h-64 overflow-auto"
        aria-label="Special test library"
      >
        {(list.data ?? []).map((d) => (
          <li
            key={d.id}
            className="flex items-center gap-2 border-b border-border py-1 last:border-0"
          >
            <button
              type="button"
              className="min-h-11 min-w-11 text-xl text-warning"
              aria-pressed={d.isFavorite}
              aria-label={
                d.isFavorite
                  ? `Remove ${d.name} from favorites`
                  : `Add ${d.name} to favorites`
              }
              onClick={() => favorite.mutate(d)}
            >
              {d.isFavorite ? "★" : "☆"}
            </button>
            <button
              type="button"
              className="min-h-11 flex-1 text-left hover:underline"
              onClick={() => onPick(d)}
            >
              <span className="font-bold text-[#333]">{d.name}</span>
              <span className="ml-2 text-text-muted">
                {[d.bodyRegion, SpecialtyLabels[d.specialty]]
                  .filter(Boolean)
                  .join(" · ")}
                {d.contraindicationWarning ? " · ⚠ precaution" : ""}
              </span>
            </button>
          </li>
        ))}
        {list.data?.length === 0 && (
          <li className="py-2 text-text-muted">No tests match.</li>
        )}
      </ul>
    </div>
  );
}
