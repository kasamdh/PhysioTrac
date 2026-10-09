import { useState, type ReactNode } from "react";
import { ROM_PRESETS, normalFor } from "../../charting/presets";
import { DecimalInput } from "./DecimalInput";
import { BodySide, BodySideLabels } from "../bodychart/regions";
import {
  ASSISTANCE_LEVELS,
  BALANCE_TESTS,
  Category,
  END_FEELS,
  FUNCTIONAL_ACTIVITIES,
  GAIT_ITEMS,
  MMT_GRADES,
  NEURO_CATEGORIES,
  NEURO_RESULTS,
  NeuroLabels,
  comparison,
  formatValue,
  measurementKey,
  type Measurement,
  type MeasurementHistory,
} from "./model";

type Patch = Partial<Measurement>;

/** Range of motion as one row per joint/movement/side, holding the active
 * and passive readings (stored as two measurements). */
interface RomRow {
  item: string;
  movement: string;
  side: number | null;
  arom: Measurement | null;
  prom: Measurement | null;
}

function toRomRows(rows: Measurement[]): RomRow[] {
  const out: RomRow[] = [];
  for (const m of rows.filter((r) => r.category === Category.RangeOfMotion)) {
    const key = (r: RomRow) => `${r.item}|${r.movement}|${r.side}`;
    const k = `${m.item}|${m.movement ?? ""}|${m.side ?? null}`;
    let row = out.find((r) => key(r) === k);
    if (!row) {
      row = {
        item: m.item,
        movement: m.movement ?? "",
        side: m.side ?? null,
        arom: null,
        prom: null,
      };
      out.push(row);
    }
    if ((m.mode ?? "").toUpperCase() === "PROM") row.prom = m;
    else row.arom = m;
  }
  return out;
}

function fromRomRows(rows: RomRow[]): Measurement[] {
  return rows.flatMap((r) => {
    const base = {
      category: Category.RangeOfMotion,
      item: r.item,
      movement: r.movement,
      side: r.side,
      unit: "deg",
    };
    const arom: Measurement = {
      ...r.arom,
      ...base,
      mode: "AROM",
      numericValue: r.arom?.numericValue ?? null,
    };
    const prom: Measurement | null = r.prom
      ? { ...r.prom, ...base, mode: "PROM" }
      : null;
    return prom ? [arom, prom] : [arom];
  });
}

/** Objective measurements by category -- range of motion, strength,
 * neurological, gait, balance and functional tests -- each value with its
 * baseline, previous value and change from the patient's signed notes. */
export function MeasurementsPanel({
  value,
  onChange,
  readOnly,
  history,
}: {
  value: Measurement[];
  onChange: (next: Measurement[]) => void;
  readOnly: boolean;
  history: MeasurementHistory[];
}) {
  const historyByKey = new Map(history.map((h) => [measurementKey(h), h]));
  const rom = toRomRows(value);
  const others = value.filter((m) => m.category !== Category.RangeOfMotion);
  const setRom = (next: RomRow[]) =>
    onChange([...fromRomRows(next), ...others]);
  const setOthers = (next: Measurement[]) =>
    onChange([...fromRomRows(rom), ...next]);
  const inCategory = (cats: number[]) =>
    others.map((m, i) => ({ m, i })).filter((x) => cats.includes(x.m.category));
  const updateOther = (i: number, patch: Patch) =>
    setOthers(others.map((m, j) => (j === i ? { ...m, ...patch } : m)));
  const removeOther = (i: number) =>
    setOthers(others.filter((_, j) => j !== i));
  const addOther = (m: Measurement) => setOthers([...others, m]);
  const compare = (m: Measurement) =>
    comparison(historyByKey.get(measurementKey(m)), m, m.unit, m.mode);

  return (
    <div className="space-y-6">
      <RomSection
        rows={rom}
        onChange={setRom}
        readOnly={readOnly}
        compare={compare}
      />

      <CategorySection
        title="Strength"
        readOnly={readOnly}
        items={inCategory([Category.Strength])}
        onAdd={() =>
          addOther({
            category: Category.Strength,
            item: "",
            side: BodySide.Right,
            mode: "MMT",
          })
        }
        onRemove={removeOther}
        compare={compare}
        fields={(m, set) => [
          [
            "Muscle / movement",
            <TextIn
              key="i"
              label="Muscle or movement"
              value={m.item}
              onChange={(v) => set({ item: v })}
              list="muscle-list"
            />,
            2,
          ],
          [
            "Side",
            <SideSelect
              key="s"
              value={m.side}
              onChange={(v) => set({ side: v })}
            />,
          ],
          [
            "Method",
            <Sel
              key="m"
              label="Method"
              value={m.mode ?? "MMT"}
              options={["MMT", "Dynamometer"]}
              onChange={(v) =>
                set({
                  mode: v,
                  textValue: null,
                  numericValue: null,
                  unit: v === "Dynamometer" ? "lb" : null,
                })
              }
            />,
          ],
          m.mode === "Dynamometer"
            ? [
                "Force",
                <NumUnit
                  key="v"
                  label="Force"
                  value={m.numericValue}
                  unit={m.unit}
                  units={["lb", "kg", "N"]}
                  onChange={set}
                />,
              ]
            : [
                "Grade (0–5)",
                <Sel
                  key="g"
                  label="MMT grade"
                  value={m.textValue ?? ""}
                  options={["", ...MMT_GRADES]}
                  onChange={(v) => set({ textValue: v || null })}
                />,
              ],
          [
            "Pain",
            <Check
              key="p"
              label="Painful"
              value={!!m.painful}
              onChange={(v) => set({ painful: v })}
            />,
          ],
          [
            "Compensation",
            <TextIn
              key="c"
              label="Compensation"
              value={m.compensation ?? ""}
              onChange={(v) => set({ compensation: v || null })}
            />,
            2,
          ],
          [
            "Comments",
            <TextIn
              key="n"
              label="Comments"
              value={m.comment ?? ""}
              onChange={(v) => set({ comment: v || null })}
            />,
            2,
          ],
        ]}
        summary={(m) =>
          `${m.item || "Muscle"} ${sideText(m.side)} — ${m.mode === "Dynamometer" ? formatValue(m, m.unit) : (m.textValue ?? "—")}${m.painful ? ", painful" : ""}${m.compensation ? `, compensation: ${m.compensation}` : ""}`
        }
        onUpdate={updateOther}
      />

      <CategorySection
        title="Neurological examination"
        readOnly={readOnly}
        items={inCategory(NEURO_CATEGORIES)}
        onAdd={() =>
          addOther({
            category: Category.Reflex,
            item: "",
            side: BodySide.Right,
          })
        }
        onRemove={removeOther}
        compare={compare}
        fields={(m, set) => [
          [
            "Test",
            <Sel
              key="c"
              label="Neurological test"
              value={String(m.category)}
              options={NEURO_CATEGORIES.map(String)}
              labels={NeuroLabels}
              onChange={(v) => set({ category: Number(v), textValue: null })}
            />,
          ],
          [
            "Level / item",
            <TextIn
              key="i"
              label="Level or item"
              value={m.item}
              placeholder="e.g. L4, Biceps (C5), CN VII"
              onChange={(v) => set({ item: v })}
            />,
            2,
          ],
          [
            "Side",
            <SideSelect
              key="s"
              value={m.side}
              onChange={(v) => set({ side: v })}
            />,
          ],
          [
            "Result",
            <TextIn
              key="r"
              label="Result"
              value={m.textValue ?? ""}
              list={`neuro-${m.category}`}
              onChange={(v) => set({ textValue: v || null })}
            />,
          ],
          [
            "Comments",
            <TextIn
              key="n"
              label="Comments"
              value={m.comment ?? ""}
              onChange={(v) => set({ comment: v || null })}
            />,
            2,
          ],
        ]}
        summary={(m) =>
          `${NeuroLabels[m.category]}: ${m.item} ${sideText(m.side)} — ${m.textValue ?? "—"}`
        }
        onUpdate={updateOther}
      />

      <CategorySection
        title="Gait"
        readOnly={readOnly}
        items={inCategory([Category.Gait])}
        onAdd={() =>
          addOther({ category: Category.Gait, item: "Distance", unit: "ft" })
        }
        onRemove={removeOther}
        compare={compare}
        fields={(m, set) => [
          [
            "Item",
            <Sel
              key="i"
              label="Gait item"
              value={m.item}
              options={GAIT_ITEMS}
              onChange={(v) => set({ item: v })}
            />,
          ],
          [
            "Value",
            <NumUnit
              key="v"
              label="Value"
              value={m.numericValue}
              unit={m.unit}
              units={["ft", "m", "m/s", "min"]}
              onChange={set}
            />,
          ],
          [
            "Description",
            <TextIn
              key="t"
              label="Description"
              value={m.textValue ?? ""}
              placeholder="e.g. decreased stance time R"
              onChange={(v) => set({ textValue: v || null })}
            />,
            2,
          ],
          [
            "Assistive device",
            <TextIn
              key="d"
              label="Assistive device"
              value={m.assistiveDevice ?? ""}
              onChange={(v) => set({ assistiveDevice: v || null })}
            />,
          ],
          [
            "Assistance",
            <Sel
              key="a"
              label="Assistance level"
              value={m.assistanceLevel ?? ""}
              options={["", ...ASSISTANCE_LEVELS]}
              onChange={(v) => set({ assistanceLevel: v || null })}
            />,
          ],
          [
            "Surface",
            <TextIn
              key="f"
              label="Surface"
              value={m.surface ?? ""}
              onChange={(v) => set({ surface: v || null })}
            />,
          ],
          [
            "Comments",
            <TextIn
              key="n"
              label="Comments"
              value={m.comment ?? ""}
              onChange={(v) => set({ comment: v || null })}
            />,
            2,
          ],
        ]}
        summary={(m) =>
          `${m.item}: ${formatValue(m, m.unit)}${m.textValue && m.numericValue != null ? ` — ${m.textValue}` : ""}${m.assistiveDevice ? `, ${m.assistiveDevice}` : ""}${m.assistanceLevel ? `, ${m.assistanceLevel}` : ""}${m.surface ? `, ${m.surface}` : ""}`
        }
        onUpdate={updateOther}
      />

      <CategorySection
        title="Balance"
        readOnly={readOnly}
        items={inCategory([Category.Balance])}
        onAdd={() =>
          addOther({
            category: Category.Balance,
            item: "Static standing",
            condition: "Eyes open",
            surface: "Firm",
            unit: "sec",
          })
        }
        onRemove={removeOther}
        compare={compare}
        fields={(m, set) => [
          [
            "Position / test",
            <Sel
              key="i"
              label="Balance test"
              value={m.item}
              options={BALANCE_TESTS}
              onChange={(v) => set({ item: v })}
            />,
          ],
          [
            "Eyes",
            <Sel
              key="c"
              label="Eyes"
              value={m.condition ?? "Eyes open"}
              options={["Eyes open", "Eyes closed"]}
              onChange={(v) => set({ condition: v })}
            />,
          ],
          [
            "Surface",
            <Sel
              key="f"
              label="Surface"
              value={m.surface ?? "Firm"}
              options={["Firm", "Foam", "Uneven"]}
              onChange={(v) => set({ surface: v })}
            />,
          ],
          [
            "Time",
            <NumUnit
              key="v"
              label="Time"
              value={m.numericValue}
              unit={m.unit ?? "sec"}
              units={["sec"]}
              onChange={set}
            />,
          ],
          [
            "Assistance",
            <Sel
              key="a"
              label="Assistance level"
              value={m.assistanceLevel ?? ""}
              options={["", ...ASSISTANCE_LEVELS]}
              onChange={(v) => set({ assistanceLevel: v || null })}
            />,
          ],
          [
            "Loss of balance",
            <Check
              key="l"
              label="Loss of balance"
              value={m.textValue === "Loss of balance"}
              onChange={(v) => set({ textValue: v ? "Loss of balance" : null })}
            />,
          ],
          [
            "Comments",
            <TextIn
              key="n"
              label="Comments"
              value={m.comment ?? ""}
              onChange={(v) => set({ comment: v || null })}
            />,
            2,
          ],
        ]}
        summary={(m) =>
          `${m.item}, ${m.condition ?? ""}, ${m.surface ?? ""}: ${formatValue({ numericValue: m.numericValue }, m.unit)}${m.textValue ? `, ${m.textValue.toLowerCase()}` : ""}${m.assistanceLevel ? `, ${m.assistanceLevel}` : ""}`
        }
        onUpdate={updateOther}
      />

      <CategorySection
        title="Functional testing"
        readOnly={readOnly}
        items={inCategory([Category.Functional])}
        onAdd={() =>
          addOther({
            category: Category.Functional,
            item: "Sit-to-stand",
            unit: "reps",
          })
        }
        onRemove={removeOther}
        compare={compare}
        fields={(m, set) => [
          [
            "Activity",
            <Sel
              key="i"
              label="Activity"
              value={m.item}
              options={FUNCTIONAL_ACTIVITIES}
              onChange={(v) => set({ item: v })}
            />,
          ],
          [
            "Side",
            <SideSelect
              key="s"
              value={m.side}
              onChange={(v) => set({ side: v })}
            />,
          ],
          [
            "Result",
            <NumUnit
              key="v"
              label="Result"
              value={m.numericValue}
              unit={m.unit}
              units={["reps", "sec", "lb", "kg", "ft", "m"]}
              onChange={set}
            />,
          ],
          [
            "Assistance",
            <Sel
              key="a"
              label="Assistance level"
              value={m.assistanceLevel ?? ""}
              options={["", ...ASSISTANCE_LEVELS]}
              onChange={(v) => set({ assistanceLevel: v || null })}
            />,
          ],
          [
            "Quality",
            <TextIn
              key="t"
              label="Movement quality"
              value={m.textValue ?? ""}
              onChange={(v) => set({ textValue: v || null })}
            />,
            2,
          ],
          [
            "Comments",
            <TextIn
              key="n"
              label="Comments"
              value={m.comment ?? ""}
              onChange={(v) => set({ comment: v || null })}
            />,
            2,
          ],
        ]}
        summary={(m) =>
          `${m.item} ${sideText(m.side)}: ${formatValue({ numericValue: m.numericValue }, m.unit)}${m.assistanceLevel ? `, ${m.assistanceLevel}` : ""}${m.textValue ? `, ${m.textValue}` : ""}`
        }
        onUpdate={updateOther}
      />

      {!readOnly && (
        <>
          <datalist id="muscle-list">
            {[
              "Quadriceps",
              "Hamstrings",
              "Gluteus medius",
              "Gluteus maximus",
              "Hip flexors",
              "Ankle dorsiflexors",
              "Gastrocnemius-soleus",
              "Deltoid",
              "Rotator cuff (ER)",
              "Rotator cuff (IR)",
              "Biceps",
              "Triceps",
              "Grip",
            ].map((m) => (
              <option key={m} value={m} />
            ))}
          </datalist>
          {Object.entries(NEURO_RESULTS).map(([c, opts]) => (
            <datalist key={c} id={`neuro-${c}`}>
              {opts.map((o) => (
                <option key={o} value={o} />
              ))}
            </datalist>
          ))}
        </>
      )}
    </div>
  );
}

const sideText = (s: number | null | undefined) =>
  s == null ? "" : `(${BodySideLabels[s]})`;

function SectionTitle({ children }: { children: ReactNode }) {
  return <h3 className="mb-2 text-xl font-bold text-[#333]">{children}</h3>;
}

function RomSection({
  rows,
  onChange,
  readOnly,
  compare,
}: {
  rows: RomRow[];
  onChange: (rows: RomRow[]) => void;
  readOnly: boolean;
  compare: (m: Measurement) => string | null;
}) {
  const [region, setRegion] = useState("Knee");
  const [side, setSide] = useState<number>(BodySide.Right);
  const update = (i: number, patch: Partial<RomRow>) =>
    onChange(rows.map((r, j) => (j === i ? { ...r, ...patch } : r)));
  const setReading = (
    i: number,
    mode: "arom" | "prom",
    value: number | null,
  ) => {
    const r = rows[i];
    const existing = r[mode];
    update(i, {
      [mode]: {
        ...(existing ?? {
          category: Category.RangeOfMotion,
          item: r.item,
          mode: mode.toUpperCase(),
        }),
        numericValue: value,
      },
    });
  };
  const reading = (r: RomRow, mode: "AROM" | "PROM") =>
    mode === "AROM" ? r.arom : r.prom;
  const stored = (r: RomRow, mode: "AROM" | "PROM"): Measurement => ({
    category: Category.RangeOfMotion,
    item: r.item,
    movement: r.movement,
    side: r.side,
    mode,
    unit: "deg",
    numericValue: reading(r, mode)?.numericValue ?? null,
  });

  return (
    <section aria-labelledby="mx-rom">
      <SectionTitle>
        <span id="mx-rom">Range of motion</span>
      </SectionTitle>
      {!readOnly && (
        <div className="mb-3 flex flex-wrap items-end gap-2">
          <Sel
            label="Region"
            value={region}
            options={Object.keys(ROM_PRESETS)}
            onChange={setRegion}
            showLabel
          />
          <SideSelect
            value={side}
            onChange={(v) => setSide(v ?? BodySide.Right)}
            showLabel
          />
          <button
            type="button"
            className="btn-refresh"
            onClick={() =>
              onChange([
                ...rows,
                ...ROM_PRESETS[region]
                  .filter(
                    (p) =>
                      !rows.some(
                        (r) =>
                          r.item === region &&
                          r.movement === p.motion &&
                          r.side === side,
                      ),
                  )
                  .map((p) => ({
                    item: region,
                    movement: p.motion,
                    side,
                    arom: null,
                    prom: null,
                  })),
              ])
            }
          >
            + Add {region} motions
          </button>
          <button
            type="button"
            className="btn-refresh"
            onClick={() =>
              onChange([
                ...rows,
                { item: "", movement: "", side, arom: null, prom: null },
              ])
            }
          >
            + Add one
          </button>
        </div>
      )}
      {rows.length === 0 && (
        <p className="text-text-muted">No range of motion recorded.</p>
      )}
      <ol className="space-y-2">
        {rows.map((r, i) => {
          const normal = normalFor(r.item, r.movement);
          const lines = (["AROM", "PROM"] as const)
            .map((mode) =>
              reading(r, mode) || mode === "AROM"
                ? compare(stored(r, mode))
                : null,
            )
            .map((line, k) =>
              line ? `${k === 0 ? "AROM" : "PROM"}: ${line}` : null,
            )
            .filter(Boolean);
          return (
            <li key={i} className="rounded-md border border-border p-3">
              {readOnly ? (
                <p className="text-[#333]">
                  <strong>
                    {r.item} {r.movement} {sideText(r.side)}
                  </strong>{" "}
                  AROM{" "}
                  {formatValue({ numericValue: r.arom?.numericValue }, "deg")} ·
                  PROM{" "}
                  {formatValue({ numericValue: r.prom?.numericValue }, "deg")}
                  {r.prom?.endFeel
                    ? ` · end feel ${r.prom.endFeel.toLowerCase()}`
                    : ""}
                  {r.arom?.painful ? " · painful" : ""}
                  {normal != null ? ` · normal ${normal} deg` : ""}
                  {r.arom?.comment ? ` · ${r.arom.comment}` : ""}
                </p>
              ) : (
                <div className="grid grid-cols-2 gap-3 md:grid-cols-4 xl:grid-cols-8">
                  <Field label="Joint">
                    <TextIn
                      label="Joint"
                      value={r.item}
                      onChange={(v) => update(i, { item: v })}
                    />
                  </Field>
                  <Field label="Movement">
                    <TextIn
                      label="Movement"
                      value={r.movement}
                      onChange={(v) => update(i, { movement: v })}
                    />
                  </Field>
                  <Field label="Side">
                    <SideSelect
                      value={r.side}
                      onChange={(v) => update(i, { side: v })}
                    />
                  </Field>
                  <Field label="AROM (deg)">
                    <DecimalInput
                      aria-label={`${r.item} ${r.movement} AROM degrees`}
                      className="field-input mt-1"
                      value={r.arom?.numericValue}
                      onValue={(v) => setReading(i, "arom", v)}
                    />
                  </Field>
                  <Field label="PROM (deg)">
                    <DecimalInput
                      aria-label={`${r.item} ${r.movement} PROM degrees`}
                      className="field-input mt-1"
                      value={r.prom?.numericValue}
                      onValue={(v) => setReading(i, "prom", v)}
                    />
                  </Field>
                  <Field label="End feel">
                    <Sel
                      label="End feel"
                      value={r.prom?.endFeel ?? ""}
                      options={["", ...END_FEELS]}
                      onChange={(v) =>
                        update(i, {
                          prom: {
                            ...(r.prom ?? {
                              category: Category.RangeOfMotion,
                              item: r.item,
                              mode: "PROM",
                              numericValue: null,
                            }),
                            endFeel: v || null,
                          },
                        })
                      }
                    />
                  </Field>
                  <Field label="Pain">
                    <Check
                      label="Painful"
                      value={!!r.arom?.painful}
                      onChange={(v) =>
                        update(i, {
                          arom: {
                            ...(r.arom ?? { category: 0, item: r.item }),
                            painful: v,
                          },
                        })
                      }
                    />
                  </Field>
                  <Field label="">
                    <button
                      type="button"
                      className="btn-refresh mt-1"
                      aria-label={`Remove ${r.item} ${r.movement}`}
                      onClick={() => onChange(rows.filter((_, j) => j !== i))}
                    >
                      Remove
                    </button>
                  </Field>
                  <div className="col-span-2 md:col-span-4 xl:col-span-8">
                    <TextIn
                      label={`${r.item} ${r.movement} comments`}
                      placeholder="Comments"
                      value={r.arom?.comment ?? ""}
                      onChange={(v) =>
                        update(i, {
                          arom: {
                            ...(r.arom ?? { category: 0, item: r.item }),
                            comment: v || null,
                          },
                        })
                      }
                    />
                  </div>
                </div>
              )}
              {(normal != null || lines.length > 0) && !readOnly && (
                <p className="mt-1 text-text-muted">
                  {normal != null ? `Normal ${normal} deg` : ""}
                  {normal != null && lines.length ? " · " : ""}
                  {lines.join(" | ")}
                </p>
              )}
              {readOnly && lines.length > 0 && (
                <p className="mt-1 text-text-muted">{lines.join(" | ")}</p>
              )}
            </li>
          );
        })}
      </ol>
    </section>
  );
}

type FieldSpec = [label: string, control: ReactNode, span?: number];

function CategorySection({
  title,
  items,
  readOnly,
  fields,
  summary,
  onAdd,
  onUpdate,
  onRemove,
  compare,
}: {
  title: string;
  items: { m: Measurement; i: number }[];
  readOnly: boolean;
  fields: (m: Measurement, set: (p: Patch) => void) => FieldSpec[];
  summary: (m: Measurement) => string;
  onAdd: () => void;
  onUpdate: (i: number, p: Patch) => void;
  onRemove: (i: number) => void;
  compare: (m: Measurement) => string | null;
}) {
  if (readOnly && items.length === 0) return null;
  const id = `mx-${title.replace(/\s+/g, "-").toLowerCase()}`;
  return (
    <section aria-labelledby={id}>
      <SectionTitle>
        <span id={id}>{title}</span>
      </SectionTitle>
      {items.length === 0 && <p className="text-text-muted">None recorded.</p>}
      <ol className="space-y-2">
        {items.map(({ m, i }, n) => {
          const line = compare(m);
          return (
            <li key={i} className="rounded-md border border-border p-3">
              {readOnly ? (
                <p className="text-[#333]">
                  {summary(m)}
                  {m.comment ? ` · ${m.comment}` : ""}
                </p>
              ) : (
                <div className="grid grid-cols-2 gap-3 md:grid-cols-4 xl:grid-cols-6">
                  {fields(m, (p) => onUpdate(i, p)).map(
                    ([label, control, span]) => (
                      <div
                        key={label}
                        className={span === 2 ? "col-span-2" : ""}
                      >
                        <span className="block text-text-muted">{label}</span>
                        {control}
                      </div>
                    ),
                  )}
                  <div className="flex items-end">
                    <button
                      type="button"
                      className="btn-refresh"
                      aria-label={`Remove ${title.toLowerCase()} ${n + 1}`}
                      onClick={() => onRemove(i)}
                    >
                      Remove
                    </button>
                  </div>
                </div>
              )}
              {line && <p className="mt-1 text-text-muted">{line}</p>}
            </li>
          );
        })}
      </ol>
      {!readOnly && (
        <button type="button" className="btn-refresh mt-2" onClick={onAdd}>
          + Add{" "}
          {title
            .toLowerCase()
            .replace(" examination", "")
            .replace(" testing", " test")}
        </button>
      )}
    </section>
  );
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <span className="block text-text-muted">{label || " "}</span>
      {children}
    </div>
  );
}

function TextIn({
  label,
  value,
  onChange,
  placeholder,
  list,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  placeholder?: string;
  list?: string;
}) {
  return (
    <input
      aria-label={label}
      className="field-input mt-1"
      value={value}
      placeholder={placeholder}
      list={list}
      maxLength={300}
      onChange={(e) => onChange(e.target.value)}
    />
  );
}

function Sel({
  label,
  value,
  options,
  onChange,
  labels,
  showLabel = false,
}: {
  label: string;
  value: string;
  options: string[];
  onChange: (v: string) => void;
  labels?: Record<string | number, string>;
  showLabel?: boolean;
}) {
  const select = (
    <select
      aria-label={showLabel ? undefined : label}
      className="field-input mt-1"
      value={value}
      onChange={(e) => onChange(e.target.value)}
    >
      {options.map((o) => (
        <option key={o} value={o}>
          {labels?.[o] ?? (o === "" ? "—" : o)}
        </option>
      ))}
    </select>
  );
  return showLabel ? (
    <label className="block">
      <span className="block text-text-muted">{label}</span>
      {select}
    </label>
  ) : (
    select
  );
}

function SideSelect({
  value,
  onChange,
  showLabel = false,
}: {
  value: number | null | undefined;
  onChange: (v: number | null) => void;
  showLabel?: boolean;
}) {
  const select = (
    <select
      aria-label={showLabel ? undefined : "Side"}
      className="field-input mt-1"
      value={value ?? ""}
      onChange={(e) =>
        onChange(e.target.value === "" ? null : Number(e.target.value))
      }
    >
      <option value="">—</option>
      {Object.entries(BodySideLabels).map(([v, l]) => (
        <option key={v} value={v}>
          {l}
        </option>
      ))}
    </select>
  );
  return showLabel ? (
    <label className="block">
      <span className="block text-text-muted">Side</span>
      {select}
    </label>
  ) : (
    select
  );
}

function NumUnit({
  label,
  value,
  unit,
  units,
  onChange,
}: {
  label: string;
  value: number | null | undefined;
  unit: string | null | undefined;
  units: string[];
  onChange: (p: Patch) => void;
}) {
  return (
    <div className="mt-1 flex gap-1">
      <DecimalInput
        aria-label={label}
        className="field-input min-w-0"
        value={value}
        onValue={(v) => onChange({ numericValue: v })}
      />
      {units.length > 1 ? (
        <select
          aria-label={`${label} unit`}
          className="field-input w-24"
          value={unit ?? units[0]}
          onChange={(e) => onChange({ unit: e.target.value })}
        >
          {units.map((u) => (
            <option key={u}>{u}</option>
          ))}
        </select>
      ) : (
        <span className="self-center text-text-muted">{units[0]}</span>
      )}
    </div>
  );
}

function Check({
  label,
  value,
  onChange,
}: {
  label: string;
  value: boolean;
  onChange: (v: boolean) => void;
}) {
  return (
    <label className="mt-1 flex min-h-11 items-center gap-2">
      <input
        type="checkbox"
        className="h-5 w-5"
        checked={value}
        onChange={(e) => onChange(e.target.checked)}
      />
      {label}
    </label>
  );
}
