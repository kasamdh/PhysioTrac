import { useState, type KeyboardEvent, type ReactNode } from "react";
import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { useToast } from "../../../components/Toast";
import { ASSISTANCE_LEVELS } from "../measurements/model";
import { DecimalInput } from "../measurements/DecimalInput";
import {
  createInterventionGroup,
  deleteInterventionGroup,
  fetchInterventionGroups,
  searchInterventions,
  setInterventionFavorite,
} from "./api";
import {
  InterventionCategories,
  StatusLabels,
  analyze,
  blankEntry,
  doseText,
  fromGroupItem,
  fromLibrary,
  minutesBetween,
  type FlowsheetEntry,
  type LibraryItem,
  type PreviousFlowsheet,
} from "./model";

const fmtDate = (iso: string) => {
  const [y, m, d] = iso.split("-");
  return `${m}/${d}/${y}`;
};
const hhmm = (t: string | null) => (t ? t.slice(0, 5) : "");

/** Intervention and exercise flowsheet: fast entry from the approved
 * library (search + Enter, favorites, reusable groups), selective carry-
 * forward from the last visit (each carried entry must be reviewed),
 * per-entry dosage, timing, pain and response, and live totals with billing
 * checks that are advice only -- nothing is billed or submitted here. */
export function FlowsheetPanel({
  value,
  onChange,
  readOnly,
  previous,
  ruleVariant,
  canShareGroups,
}: {
  value: FlowsheetEntry[];
  onChange: (next: FlowsheetEntry[]) => void;
  readOnly: boolean;
  previous: PreviousFlowsheet | null | undefined;
  ruleVariant: string;
  canShareGroups: boolean;
}) {
  const summary = analyze(value, ruleVariant);
  const update = (i: number, patch: Partial<FlowsheetEntry>) =>
    onChange(value.map((e, j) => (j === i ? { ...e, ...patch } : e)));
  const move = (i: number, by: number) => {
    const next = [...value];
    const [e] = next.splice(i, 1);
    next.splice(i + by, 0, e);
    onChange(next);
  };
  const add = (entries: FlowsheetEntry[]) => {
    onChange([...value, ...entries]);
    window.setTimeout(
      () => document.getElementById(`fs-${value.length}-sets`)?.focus(),
      0,
    );
  };
  const lastVisit = (e: FlowsheetEntry) =>
    previous?.entries.find(
      (p) =>
        (e.libraryItemId && p.libraryItemId === e.libraryItemId) ||
        p.description.toLowerCase() === e.description.toLowerCase(),
    );

  return (
    <div className="space-y-3">
      {!readOnly && (
        <QuickAdd
          onAdd={add}
          current={value}
          previous={previous}
          canShareGroups={canShareGroups}
        />
      )}

      <div
        role="status"
        className="rounded-md border border-border bg-surface-muted px-3 py-2 text-[#333]"
      >
        <strong>{summary.timedMinutes}</strong> timed minutes ·{" "}
        <strong>{summary.untimedServices}</strong> untimed service
        {summary.untimedServices === 1 ? "" : "s"} · estimated{" "}
        <strong>{summary.estimatedTimedUnits}</strong> timed unit
        {summary.estimatedTimedUnits === 1 ? "" : "s"} (
        {summary.ruleVariant === "RoundedFifteenMinute"
          ? "rounded 15-minute units"
          : "Medicare 8-minute rule"}
        )
        {summary.enteredTimedUnits != null
          ? ` · ${summary.enteredTimedUnits} entered`
          : ""}
        {summary.warnings.length > 0 && !readOnly && (
          <ul className="mt-1 ml-5 list-disc text-[#7a4a00]">
            {summary.warnings.map((w, k) => (
              <li key={k}>{w.message}</li>
            ))}
          </ul>
        )}
        <p className="mt-1 text-text-muted">
          Billing figures are advice for review only; nothing is billed or
          submitted from the flowsheet.
        </p>
      </div>

      {value.length === 0 && (
        <p className="text-text-muted">No interventions recorded yet.</p>
      )}
      <ol className="space-y-2" aria-label="Flowsheet">
        {value.map((e, i) => {
          const last = lastVisit(e);
          return readOnly ? (
            <li
              key={i}
              className="rounded-md border border-border p-3 text-[#333]"
            >
              <p>
                <strong>{e.description}</strong>
                {e.cptCode ? ` (${e.cptCode})` : ""} · {StatusLabels[e.status]}
                {e.startTime
                  ? ` · ${hhmm(e.startTime)}–${hhmm(e.endTime)}`
                  : ""}
                {doseText(e) ? ` · ${doseText(e)}` : ""}
                {e.units != null
                  ? ` · ${e.units} unit${e.units === 1 ? "" : "s"}`
                  : ""}
                {e.isTimed ? "" : " · untimed"}
              </p>
              <p className="text-text-muted">
                {[
                  e.position && `Position: ${e.position}`,
                  e.equipment && `Equipment: ${e.equipment}`,
                  e.cueing && `Cueing: ${e.cueing}`,
                  e.modification && `Modified: ${e.modification}`,
                  e.painBefore != null &&
                    `Pain ${e.painBefore}→${e.painAfter ?? "—"}/10`,
                  e.patientResponse && `Response: ${e.patientResponse}`,
                  e.comment,
                ]
                  .filter(Boolean)
                  .join(" · ")}
              </p>
            </li>
          ) : (
            <EntryCard
              key={i}
              index={i}
              count={value.length}
              entry={e}
              last={last}
              lastDate={previous?.serviceDate}
              onChange={(p) => update(i, p)}
              onMove={(by) => move(i, by)}
              onRemove={() => onChange(value.filter((_, j) => j !== i))}
            />
          );
        })}
      </ol>
    </div>
  );
}

function EntryCard({
  index: i,
  count,
  entry: e,
  last,
  lastDate,
  onChange,
  onMove,
  onRemove,
}: {
  index: number;
  count: number;
  entry: FlowsheetEntry;
  last: FlowsheetEntry | undefined;
  lastDate: string | undefined;
  onChange: (p: Partial<FlowsheetEntry>) => void;
  onMove: (by: number) => void;
  onRemove: () => void;
}) {
  const id = (f: string) => `fs-${i}-${f}`;
  const text = (
    f: keyof FlowsheetEntry,
    label: string,
    max = 60,
    span = false,
  ) => (
    <Field label={label} htmlFor={id(f)} span={span}>
      <input
        id={id(f)}
        className="field-input mt-1"
        maxLength={max}
        value={(e[f] as string | null) ?? ""}
        onChange={(ev) => onChange({ [f]: ev.target.value || null })}
      />
    </Field>
  );
  const int = (
    f: "sets" | "repetitions" | "units" | "painBefore" | "painAfter",
    label: string,
    max: number,
  ) => (
    <Field label={label} htmlFor={id(f)}>
      <DecimalInput
        id={id(f)}
        className="field-input mt-1"
        value={e[f]}
        onValue={(v) =>
          onChange({
            [f]: v == null ? null : Math.max(0, Math.min(max, Math.round(v))),
          })
        }
      />
    </Field>
  );
  // Minutes follow the start/end times until the therapist types their own.
  const setTime = (f: "startTime" | "endTime", v: string) => {
    const derived = minutesBetween(e.startTime, e.endTime);
    const next = { ...e, [f]: v || null };
    const span = minutesBetween(next.startTime, next.endTime);
    const follow = !e.minutes || e.minutes === derived;
    onChange({
      [f]: v || null,
      ...(span != null && follow ? { minutes: span } : {}),
    });
  };

  return (
    <li
      className={`rounded-md border p-3 ${e.carriedForwardFromNoteId && !e.carryForwardReviewed ? "border-warning bg-warning-light/40" : "border-border"}`}
    >
      <div className="mb-2 flex flex-wrap items-center gap-2">
        <span className="font-bold text-[#333]">
          {i + 1}. {e.description || "New intervention"}
        </span>
        {e.carriedForwardFromNoteId && (
          <label className="flex min-h-11 items-center gap-2 rounded-full bg-warning-light px-3 text-[#333]">
            <input
              type="checkbox"
              className="h-5 w-5"
              checked={e.carryForwardReviewed}
              onChange={(ev) =>
                onChange({ carryForwardReviewed: ev.target.checked })
              }
            />
            Carried forward{lastDate ? ` from ${fmtDate(lastDate)}` : ""} —
            reviewed
          </label>
        )}
        <span className="ml-auto flex gap-1">
          <button
            type="button"
            className="btn-refresh"
            aria-label={`Move ${e.description} up`}
            disabled={i === 0}
            onClick={() => onMove(-1)}
          >
            ↑
          </button>
          <button
            type="button"
            className="btn-refresh"
            aria-label={`Move ${e.description} down`}
            disabled={i === count - 1}
            onClick={() => onMove(1)}
          >
            ↓
          </button>
          <button
            type="button"
            className="btn-refresh"
            aria-label={`Remove ${e.description}`}
            onClick={onRemove}
          >
            Remove
          </button>
        </span>
      </div>
      <div className="grid grid-cols-2 gap-3 md:grid-cols-4 xl:grid-cols-8">
        {text("description", "Intervention", 300, true)}
        <Field label="Category" htmlFor={id("category")}>
          <select
            id={id("category")}
            className="field-input mt-1"
            value={e.category ?? ""}
            onChange={(ev) =>
              onChange({
                category:
                  ev.target.value === "" ? null : Number(ev.target.value),
              })
            }
          >
            <option value="">—</option>
            {Object.entries(InterventionCategories).map(([v, l]) => (
              <option key={v} value={v}>
                {l}
              </option>
            ))}
          </select>
        </Field>
        {text("cptCode", "CPT", 10)}
        <Field label="Status" htmlFor={id("status")}>
          <select
            id={id("status")}
            className="field-input mt-1"
            value={e.status}
            onChange={(ev) => onChange({ status: Number(ev.target.value) })}
          >
            {Object.entries(StatusLabels).map(([v, l]) => (
              <option key={v} value={v}>
                {l}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Timed" htmlFor={id("timed")}>
          <label className="mt-1 flex min-h-11 items-center gap-2">
            <input
              id={id("timed")}
              type="checkbox"
              className="h-5 w-5"
              checked={e.isTimed}
              onChange={(ev) => onChange({ isTimed: ev.target.checked })}
            />
            Timed service
          </label>
        </Field>
        <Field label="Start" htmlFor={id("start")}>
          <input
            id={id("start")}
            type="time"
            className="field-input mt-1"
            value={hhmm(e.startTime)}
            onChange={(ev) => setTime("startTime", ev.target.value)}
          />
        </Field>
        <Field label="End" htmlFor={id("end")}>
          <input
            id={id("end")}
            type="time"
            className="field-input mt-1"
            value={hhmm(e.endTime)}
            onChange={(ev) => setTime("endTime", ev.target.value)}
          />
        </Field>
        <Field label="Minutes" htmlFor={id("minutes")}>
          <DecimalInput
            id={id("minutes")}
            className="field-input mt-1"
            value={e.minutes}
            onValue={(v) =>
              onChange({
                minutes:
                  v == null ? 0 : Math.max(0, Math.min(480, Math.round(v))),
              })
            }
          />
        </Field>
        {int("units", "Units", 32)}
        {int("sets", "Sets", 100)}
        {int("repetitions", "Reps", 1000)}
        {text("resistance", "Resistance")}
        {text("duration", "Duration")}
        {text("distance", "Distance")}
        {text("position", "Position")}
        {text("equipment", "Equipment", 100)}
        <Field label="Assistance" htmlFor={id("assistance")}>
          <select
            id={id("assistance")}
            className="field-input mt-1"
            value={e.assistanceLevel ?? ""}
            onChange={(ev) =>
              onChange({ assistanceLevel: ev.target.value || null })
            }
          >
            <option value="">—</option>
            {ASSISTANCE_LEVELS.map((a) => (
              <option key={a}>{a}</option>
            ))}
          </select>
        </Field>
        {text("cueing", "Cueing")}
        {text("modification", "Modification", 200, true)}
        {int("painBefore", "Pain before (0–10)", 10)}
        {int("painAfter", "Pain after (0–10)", 10)}
        {text("patientResponse", "Patient response", 1000, true)}
        {text("comment", "Therapist comments", 1000, true)}
      </div>
      {last && (
        <p className="mt-1 text-text-muted">
          Last visit{lastDate ? ` (${fmtDate(lastDate)})` : ""}:{" "}
          {doseText(last) || "recorded"}
          {last.patientResponse ? ` · ${last.patientResponse}` : ""}
        </p>
      )}
    </li>
  );
}

function Field({
  label,
  htmlFor,
  span = false,
  children,
}: {
  label: string;
  htmlFor: string;
  span?: boolean;
  children: ReactNode;
}) {
  return (
    <div className={span ? "col-span-2" : ""}>
      <label htmlFor={htmlFor} className="block text-text-muted">
        {label}
      </label>
      {children}
    </div>
  );
}

function QuickAdd({
  onAdd,
  current,
  previous,
  canShareGroups,
}: {
  onAdd: (entries: FlowsheetEntry[]) => void;
  current: FlowsheetEntry[];
  previous: PreviousFlowsheet | null | undefined;
  canShareGroups: boolean;
}) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const [search, setSearch] = useState("");
  const [carryOpen, setCarryOpen] = useState(false);
  const [picked, setPicked] = useState<Set<number>>(new Set());
  const [saveOpen, setSaveOpen] = useState(false);
  const [groupName, setGroupName] = useState("");
  const [shared, setShared] = useState(false);

  const results = useQuery({
    queryKey: ["intervention-library", search.trim()],
    queryFn: () => searchInterventions({ search }),
    placeholderData: keepPreviousData,
  });
  const favorites = (results.data ?? []).filter((i) => i.isFavorite);
  const groups = useQuery({
    queryKey: ["intervention-groups"],
    queryFn: fetchInterventionGroups,
  });
  const favorite = useMutation({
    mutationFn: (i: LibraryItem) =>
      setInterventionFavorite(i.id, !i.isFavorite),
    onSuccess: () =>
      void queryClient.invalidateQueries({
        queryKey: ["intervention-library"],
      }),
  });
  const saveGroup = useMutation({
    mutationFn: () =>
      createInterventionGroup({
        name: groupName.trim(),
        description: null,
        isShared: shared,
        items: current.map((e) => ({
          name: e.description,
          category: e.category ?? 7,
          isTimed: e.isTimed,
          cptCode: e.cptCode,
          sets: e.sets,
          repetitions: e.repetitions,
          resistance: e.resistance,
          duration: e.duration,
          equipment: e.equipment,
          position: e.position,
          libraryItemId: e.libraryItemId,
        })),
      }),
    onSuccess: (g) => {
      showToast(`Saved group ${g.name}.`);
      setSaveOpen(false);
      setGroupName("");
      void queryClient.invalidateQueries({ queryKey: ["intervention-groups"] });
    },
    onError: (e: Error) => showToast(e.message),
  });
  const retireGroup = useMutation({
    mutationFn: (id: string) => deleteInterventionGroup(id),
    onSuccess: () =>
      void queryClient.invalidateQueries({ queryKey: ["intervention-groups"] }),
  });

  const pick = (i: LibraryItem) => {
    onAdd([fromLibrary(i)]);
    setSearch("");
  };
  const onSearchKey = async (ev: KeyboardEvent<HTMLInputElement>) => {
    if (ev.key !== "Enter") return;
    ev.preventDefault();
    const term = search.trim();
    if (!term) return;
    // The list may still show the previous search's matches while the new
    // one loads; Enter must pick from what was actually typed.
    const matches = results.isPlaceholderData
      ? await queryClient.fetchQuery({
          queryKey: ["intervention-library", term],
          queryFn: () => searchInterventions({ search: term }),
        })
      : results.data;
    const first = matches?.[0];
    if (first) pick(first);
    else {
      onAdd([blankEntry({ description: term })]);
      setSearch("");
    }
  };

  return (
    <div className="space-y-2 rounded-md border border-border p-3">
      <div className="flex flex-wrap items-end gap-2">
        <label className="block min-w-[16rem] flex-1">
          <span className="block text-text-muted">Add intervention</span>
          <input
            type="search"
            className="field-input mt-1"
            placeholder="Type a name or CPT code, Enter adds the first match"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onKeyDown={(ev) => void onSearchKey(ev)}
            aria-controls="fs-results"
          />
        </label>
        <button
          type="button"
          className="btn-refresh"
          onClick={() => onAdd([blankEntry()])}
        >
          + Blank entry
        </button>
        {!!previous?.entries.length && (
          <button
            type="button"
            className="btn-refresh"
            aria-expanded={carryOpen}
            onClick={() => setCarryOpen((o) => !o)}
          >
            Carry forward from {fmtDate(previous.serviceDate)}…
          </button>
        )}
        {current.length > 0 && (
          <button
            type="button"
            className="btn-refresh"
            aria-expanded={saveOpen}
            onClick={() => setSaveOpen((o) => !o)}
          >
            Save as group…
          </button>
        )}
      </div>

      {search.trim() && (
        <ul
          id="fs-results"
          className="max-h-56 overflow-auto rounded-md border border-border"
          aria-label="Matching interventions"
        >
          {(results.data ?? []).slice(0, 12).map((i) => (
            <li
              key={i.id}
              className="flex items-center gap-1 border-b border-border last:border-0"
            >
              <button
                type="button"
                className="min-h-11 min-w-11 text-xl text-warning"
                aria-pressed={i.isFavorite}
                aria-label={
                  i.isFavorite
                    ? `Remove ${i.name} from favorites`
                    : `Add ${i.name} to favorites`
                }
                onClick={() => favorite.mutate(i)}
              >
                {i.isFavorite ? "★" : "☆"}
              </button>
              <button
                type="button"
                className="min-h-11 flex-1 px-2 text-left hover:underline"
                onClick={() => pick(i)}
              >
                <span className="font-bold text-[#333]">{i.name}</span>
                <span className="ml-2 text-text-muted">
                  {[
                    i.cptCode,
                    InterventionCategories[i.category],
                    i.isTimed ? "timed" : "untimed",
                  ]
                    .filter(Boolean)
                    .join(" · ")}
                </span>
              </button>
            </li>
          ))}
          {results.data?.length === 0 && (
            <li className="px-3 py-2 text-text-muted">
              No match — press Enter to add “{search.trim()}” as written.
            </li>
          )}
        </ul>
      )}

      {!search.trim() && favorites.length > 0 && (
        <div
          className="flex flex-wrap items-center gap-1"
          aria-label="Favorite interventions"
        >
          <span className="text-text-muted">★</span>
          {favorites.slice(0, 12).map((i) => (
            <button
              key={i.id}
              type="button"
              className="btn-refresh"
              onClick={() => pick(i)}
            >
              {i.name}
            </button>
          ))}
        </div>
      )}

      {!!groups.data?.length && (
        <div
          className="flex flex-wrap items-center gap-1"
          aria-label="Intervention groups"
        >
          <span className="text-text-muted">Groups:</span>
          {groups.data.map((g) => (
            <span key={g.id} className="inline-flex items-center">
              <button
                type="button"
                className="btn-refresh"
                onClick={() => onAdd(g.items.map(fromGroupItem))}
              >
                + {g.name} ({g.items.length}){g.isShared ? " · clinic" : ""}
              </button>
              {g.isMine && (
                <button
                  type="button"
                  className="min-h-11 min-w-11 text-text-muted"
                  aria-label={`Delete group ${g.name}`}
                  onClick={() => retireGroup.mutate(g.id)}
                >
                  ×
                </button>
              )}
            </span>
          ))}
        </div>
      )}

      {carryOpen && previous && (
        <fieldset className="rounded-md border border-warning bg-warning-light/40 p-3">
          <legend className="px-1 font-bold text-[#333]">
            Carry forward from {fmtDate(previous.serviceDate)}
          </legend>
          <p className="text-text-muted">
            Choose what to bring forward. Each carried entry must be reviewed
            before the note can be signed.
          </p>
          <ul className="mt-1">
            {previous.entries.map((e, k) => (
              <li key={k}>
                <label className="flex min-h-11 items-center gap-2">
                  <input
                    type="checkbox"
                    className="h-5 w-5"
                    checked={picked.has(k)}
                    onChange={(ev) => {
                      const next = new Set(picked);
                      if (ev.target.checked) next.add(k);
                      else next.delete(k);
                      setPicked(next);
                    }}
                  />
                  <span>
                    <strong>{e.description}</strong>{" "}
                    <span className="text-text-muted">{doseText(e)}</span>
                  </span>
                </label>
              </li>
            ))}
          </ul>
          <div className="mt-2 flex gap-2">
            <button
              type="button"
              className="btn-primary"
              disabled={picked.size === 0}
              onClick={() => {
                onAdd(
                  previous.entries
                    .filter((_, k) => picked.has(k))
                    .map((e) => ({
                      ...e,
                      id: null,
                      startTime: null,
                      endTime: null,
                      minutes: 0,
                      units: null,
                      patientResponse: null,
                      painBefore: null,
                      painAfter: null,
                      status: 0,
                      carriedForwardFromNoteId: previous.noteId,
                      carryForwardReviewed: false,
                    })),
                );
                setPicked(new Set());
                setCarryOpen(false);
              }}
            >
              Add {picked.size || ""} selected
            </button>
            <button
              type="button"
              className="btn-refresh"
              onClick={() => setCarryOpen(false)}
            >
              Cancel
            </button>
          </div>
        </fieldset>
      )}

      {saveOpen && (
        <form
          aria-label="Save as group"
          className="flex flex-wrap items-end gap-2 rounded-md border border-border p-3"
          onSubmit={(ev) => {
            ev.preventDefault();
            saveGroup.mutate();
          }}
        >
          <label className="block">
            <span className="block text-text-muted">Group name</span>
            <input
              className="field-input mt-1"
              maxLength={120}
              value={groupName}
              onChange={(ev) => setGroupName(ev.target.value)}
            />
          </label>
          {canShareGroups && (
            <label className="flex min-h-11 items-center gap-2">
              <input
                type="checkbox"
                className="h-5 w-5"
                checked={shared}
                onChange={(ev) => setShared(ev.target.checked)}
              />
              Share with the clinic
            </label>
          )}
          <button
            type="submit"
            className="btn-primary"
            disabled={!groupName.trim() || saveGroup.isPending}
          >
            Save {current.length} as group
          </button>
        </form>
      )}
    </div>
  );
}
