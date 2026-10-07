import { useState } from "react";
import {
  COMMON_SPECIAL_TESTS,
  MMT_GRADES,
  ROM_PRESETS,
  newId,
  normalFor,
} from "../presets";
import type {
  MmtRow,
  ObjectiveDetails,
  RomRow,
  Side,
  SpecialTestRow,
} from "../types";

const SIDES: { value: Side; label: string }[] = [
  { value: "", label: "—" },
  { value: "L", label: "Left" },
  { value: "R", label: "Right" },
  { value: "B", label: "Both" },
];

const key = (joint: string, motion: string, side: string) =>
  `${joint}|${motion}|${side}`.toLowerCase();

interface Props {
  value: ObjectiveDetails;
  previous: ObjectiveDetails | null;
  onChange: (v: ObjectiveDetails) => void;
  readOnly: boolean;
}

/** Examination measurements: ROM, MMT, special tests and vitals, each row
 * showing last visit's value for the same measurement so progress is visible
 * while charting. */
export function MeasurementTables({
  value,
  previous,
  onChange,
  readOnly,
}: Props) {
  const prevRom = new Map(
    (previous?.rom ?? []).map((r) => [key(r.joint, r.motion, r.side), r]),
  );
  const prevMmt = new Map(
    (previous?.mmt ?? []).map((r) => [key(r.muscle, "", r.side), r]),
  );
  const [region, setRegion] = useState("Knee");
  const [regionSide, setRegionSide] = useState<Side>("R");
  const [newMuscle, setNewMuscle] = useState("");

  const setRom = (rom: RomRow[]) => onChange({ ...value, rom });
  const setMmt = (mmt: MmtRow[]) => onChange({ ...value, mmt });
  const setTests = (specialTests: SpecialTestRow[]) =>
    onChange({ ...value, specialTests });
  const patchRom = (id: string, patch: Partial<RomRow>) =>
    setRom(value.rom.map((r) => (r.id === id ? { ...r, ...patch } : r)));
  const patchMmt = (id: string, patch: Partial<MmtRow>) =>
    setMmt(value.mmt.map((r) => (r.id === id ? { ...r, ...patch } : r)));
  const patchTest = (id: string, patch: Partial<SpecialTestRow>) =>
    setTests(
      value.specialTests.map((r) => (r.id === id ? { ...r, ...patch } : r)),
    );

  const addRegion = () => {
    const existing = new Set(
      value.rom.map((r) => key(r.joint, r.motion, r.side)),
    );
    const rows = ROM_PRESETS[region]
      .filter((m) => !existing.has(key(region, m.motion, regionSide)))
      .map((m) => ({
        id: newId(),
        joint: region,
        motion: m.motion,
        side: regionSide,
        arom: "",
        prom: "",
      }));
    setRom([...value.rom, ...rows]);
  };

  /** Same motions/muscles as last visit, values left blank to re-measure. */
  const remeasureLastVisit = () =>
    onChange({
      ...value,
      rom: (previous?.rom ?? []).map((r) => ({
        ...r,
        id: newId(),
        arom: "",
        prom: "",
      })),
      mmt: (previous?.mmt ?? []).map((r) => ({ ...r, id: newId(), grade: "" })),
    });

  const degrees = (v: string) => (v === "" ? "" : `${v}°`);
  const cell = "field-input py-1.5";

  return (
    <div className="space-y-6">
      {!readOnly &&
        previous &&
        (previous.rom.length > 0 || previous.mmt.length > 0) &&
        value.rom.length === 0 &&
        value.mmt.length === 0 && (
          <button
            type="button"
            className="btn-refresh"
            onClick={remeasureLastVisit}
          >
            Re-measure last visit’s ROM &amp; strength (
            {previous.rom.length + previous.mmt.length} items)
          </button>
        )}

      {/* ROM */}
      <section aria-labelledby="rom-title">
        <h3 id="rom-title" className="mb-2 text-xl font-bold text-[#333]">
          Range of motion
        </h3>
        {!readOnly && (
          <div className="mb-3 flex flex-wrap items-end gap-2">
            <label className="toolbar-label">
              Region
              <select
                className="toolbar-select"
                value={region}
                onChange={(e) => setRegion(e.target.value)}
              >
                {Object.keys(ROM_PRESETS).map((r) => (
                  <option key={r}>{r}</option>
                ))}
              </select>
            </label>
            <label className="toolbar-label">
              Side
              <select
                className="toolbar-select"
                value={regionSide}
                onChange={(e) => setRegionSide(e.target.value as Side)}
              >
                {SIDES.map((s) => (
                  <option key={s.value} value={s.value}>
                    {s.label}
                  </option>
                ))}
              </select>
            </label>
            <button type="button" className="btn-refresh" onClick={addRegion}>
              + Add {region} motions
            </button>
            <button
              type="button"
              className="btn-refresh"
              onClick={() =>
                setRom([
                  ...value.rom,
                  {
                    id: newId(),
                    joint: "",
                    motion: "",
                    side: "",
                    arom: "",
                    prom: "",
                  },
                ])
              }
            >
              + Other motion
            </button>
          </div>
        )}
        {value.rom.length === 0 ? (
          <p className="text-text-muted">No range of motion recorded.</p>
        ) : (
          <div className="list-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Joint</th>
                  <th>Motion</th>
                  <th>Side</th>
                  <th>AROM</th>
                  <th>PROM</th>
                  <th>Normal</th>
                  {previous && <th>Last visit</th>}
                  {!readOnly && (
                    <th>
                      <span className="sr-only">Remove</span>
                    </th>
                  )}
                </tr>
              </thead>
              <tbody>
                {value.rom.map((r) => {
                  const prev = prevRom.get(key(r.joint, r.motion, r.side));
                  const normal = normalFor(r.joint, r.motion);
                  return (
                    <tr key={r.id}>
                      <td data-label="Joint">
                        {readOnly ? (
                          r.joint
                        ) : (
                          <input
                            aria-label="Joint"
                            className={cell}
                            value={r.joint}
                            onChange={(e) =>
                              patchRom(r.id, { joint: e.target.value })
                            }
                          />
                        )}
                      </td>
                      <td data-label="Motion">
                        {readOnly ? (
                          r.motion
                        ) : (
                          <input
                            aria-label="Motion"
                            className={cell}
                            value={r.motion}
                            onChange={(e) =>
                              patchRom(r.id, { motion: e.target.value })
                            }
                          />
                        )}
                      </td>
                      <td data-label="Side">
                        {readOnly ? (
                          SIDES.find((s) => s.value === r.side)?.label
                        ) : (
                          <select
                            aria-label="Side"
                            className={cell}
                            value={r.side}
                            onChange={(e) =>
                              patchRom(r.id, { side: e.target.value as Side })
                            }
                          >
                            {SIDES.map((s) => (
                              <option key={s.value} value={s.value}>
                                {s.label}
                              </option>
                            ))}
                          </select>
                        )}
                      </td>
                      <td data-label="AROM">
                        {readOnly ? (
                          degrees(r.arom)
                        ) : (
                          <input
                            aria-label={`${r.joint} ${r.motion} AROM degrees`}
                            inputMode="numeric"
                            className={`${cell} w-24`}
                            value={r.arom}
                            onChange={(e) =>
                              patchRom(r.id, {
                                arom: e.target.value.replace(/[^0-9-]/g, ""),
                              })
                            }
                          />
                        )}
                      </td>
                      <td data-label="PROM">
                        {readOnly ? (
                          degrees(r.prom)
                        ) : (
                          <input
                            aria-label={`${r.joint} ${r.motion} PROM degrees`}
                            inputMode="numeric"
                            className={`${cell} w-24`}
                            value={r.prom}
                            onChange={(e) =>
                              patchRom(r.id, {
                                prom: e.target.value.replace(/[^0-9-]/g, ""),
                              })
                            }
                          />
                        )}
                      </td>
                      <td data-label="Normal">
                        {normal === undefined ? "—" : `${normal}°`}
                      </td>
                      {previous && (
                        <td data-label="Last visit">
                          {prev
                            ? `${degrees(prev.arom) || "—"} / ${degrees(prev.prom) || "—"}`
                            : "—"}
                        </td>
                      )}
                      {!readOnly && (
                        <td>
                          <button
                            type="button"
                            className="btn-refresh px-3"
                            aria-label={`Remove ${r.joint} ${r.motion}`}
                            onClick={() =>
                              setRom(value.rom.filter((x) => x.id !== r.id))
                            }
                          >
                            ×
                          </button>
                        </td>
                      )}
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {/* MMT */}
      <section aria-labelledby="mmt-title">
        <h3 id="mmt-title" className="mb-2 text-xl font-bold text-[#333]">
          Strength (MMT)
        </h3>
        {!readOnly && (
          <div className="mb-3 flex flex-wrap items-end gap-2">
            <input
              aria-label="Muscle or movement"
              className="toolbar-select w-72"
              placeholder="Muscle / movement, e.g. Quadriceps"
              value={newMuscle}
              onChange={(e) => setNewMuscle(e.target.value)}
            />
            <button
              type="button"
              className="btn-refresh"
              disabled={!newMuscle.trim()}
              onClick={() => {
                setMmt([
                  ...value.mmt,
                  {
                    id: newId(),
                    muscle: newMuscle.trim(),
                    side: "R",
                    grade: "",
                  },
                ]);
                setNewMuscle("");
              }}
            >
              + Add
            </button>
          </div>
        )}
        {value.mmt.length === 0 ? (
          <p className="text-text-muted">No strength testing recorded.</p>
        ) : (
          <div className="list-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Muscle / movement</th>
                  <th>Side</th>
                  <th>Grade (0–5)</th>
                  {previous && <th>Last visit</th>}
                  {!readOnly && (
                    <th>
                      <span className="sr-only">Remove</span>
                    </th>
                  )}
                </tr>
              </thead>
              <tbody>
                {value.mmt.map((r) => (
                  <tr key={r.id}>
                    <td data-label="Muscle">
                      {readOnly ? (
                        r.muscle
                      ) : (
                        <input
                          aria-label="Muscle"
                          className={cell}
                          value={r.muscle}
                          onChange={(e) =>
                            patchMmt(r.id, { muscle: e.target.value })
                          }
                        />
                      )}
                    </td>
                    <td data-label="Side">
                      {readOnly ? (
                        SIDES.find((s) => s.value === r.side)?.label
                      ) : (
                        <select
                          aria-label="Side"
                          className={cell}
                          value={r.side}
                          onChange={(e) =>
                            patchMmt(r.id, { side: e.target.value as Side })
                          }
                        >
                          {SIDES.map((s) => (
                            <option key={s.value} value={s.value}>
                              {s.label}
                            </option>
                          ))}
                        </select>
                      )}
                    </td>
                    <td data-label="Grade">
                      {readOnly ? (
                        r.grade ? (
                          `${r.grade}/5`
                        ) : (
                          "—"
                        )
                      ) : (
                        <select
                          aria-label={`${r.muscle} grade`}
                          className={cell}
                          value={r.grade}
                          onChange={(e) =>
                            patchMmt(r.id, { grade: e.target.value })
                          }
                        >
                          <option value="">—</option>
                          {MMT_GRADES.map((g) => (
                            <option key={g} value={g}>
                              {g}/5
                            </option>
                          ))}
                        </select>
                      )}
                    </td>
                    {previous && (
                      <td data-label="Last visit">
                        {prevMmt.get(key(r.muscle, "", r.side))?.grade
                          ? `${prevMmt.get(key(r.muscle, "", r.side))!.grade}/5`
                          : "—"}
                      </td>
                    )}
                    {!readOnly && (
                      <td>
                        <button
                          type="button"
                          className="btn-refresh px-3"
                          aria-label={`Remove ${r.muscle}`}
                          onClick={() =>
                            setMmt(value.mmt.filter((x) => x.id !== r.id))
                          }
                        >
                          ×
                        </button>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {/* Special tests */}
      <section aria-labelledby="tests-title">
        <h3 id="tests-title" className="mb-2 text-xl font-bold text-[#333]">
          Special tests
        </h3>
        <datalist id="special-tests">
          {COMMON_SPECIAL_TESTS.map((t) => (
            <option key={t} value={t} />
          ))}
        </datalist>
        {value.specialTests.length === 0 && (
          <p className="mb-2 text-text-muted">No special tests recorded.</p>
        )}
        <div className="space-y-2">
          {value.specialTests.map((t) => (
            <div key={t.id} className="flex flex-wrap items-center gap-2">
              {readOnly ? (
                <span>
                  <strong>{t.test}</strong>{" "}
                  {t.side &&
                    `(${SIDES.find((s) => s.value === t.side)?.label})`}{" "}
                  —{" "}
                  {t.result === "positive"
                    ? "Positive"
                    : t.result === "negative"
                      ? "Negative"
                      : "not recorded"}
                </span>
              ) : (
                <>
                  <input
                    list="special-tests"
                    aria-label="Special test"
                    className="toolbar-select w-64"
                    value={t.test}
                    onChange={(e) => patchTest(t.id, { test: e.target.value })}
                  />
                  <select
                    aria-label="Side"
                    className="toolbar-select"
                    value={t.side}
                    onChange={(e) =>
                      patchTest(t.id, { side: e.target.value as Side })
                    }
                  >
                    {SIDES.map((s) => (
                      <option key={s.value} value={s.value}>
                        {s.label}
                      </option>
                    ))}
                  </select>
                  <div
                    role="group"
                    aria-label={`${t.test || "Test"} result`}
                    className="seg-group"
                  >
                    {(["positive", "negative"] as const).map((r) => (
                      <button
                        key={r}
                        type="button"
                        className="seg-btn"
                        aria-pressed={t.result === r}
                        onClick={() => patchTest(t.id, { result: r })}
                      >
                        {r === "positive" ? "Positive (+)" : "Negative (−)"}
                      </button>
                    ))}
                  </div>
                  <button
                    type="button"
                    className="btn-refresh px-3"
                    aria-label={`Remove ${t.test || "test"}`}
                    onClick={() =>
                      setTests(value.specialTests.filter((x) => x.id !== t.id))
                    }
                  >
                    ×
                  </button>
                </>
              )}
            </div>
          ))}
        </div>
        {!readOnly && (
          <button
            type="button"
            className="btn-refresh mt-2"
            onClick={() =>
              setTests([
                ...value.specialTests,
                { id: newId(), test: "", side: "", result: "" },
              ])
            }
          >
            + Add special test
          </button>
        )}
      </section>

      {/* Vitals */}
      {readOnly ? (
        (value.vitals.bloodPressure ||
          value.vitals.heartRate ||
          value.vitals.spo2) && (
          <section aria-labelledby="vitals-title">
            <h3
              id="vitals-title"
              className="mb-2 text-xl font-bold text-[#333]"
            >
              Vitals
            </h3>
            <p className="text-[#333]">
              {[
                value.vitals.bloodPressure &&
                  `BP ${value.vitals.bloodPressure}`,
                value.vitals.heartRate && `HR ${value.vitals.heartRate} bpm`,
                value.vitals.spo2 && `SpO₂ ${value.vitals.spo2}%`,
              ]
                .filter(Boolean)
                .join(" · ")}
            </p>
          </section>
        )
      ) : (
        <section aria-labelledby="vitals-title">
          <h3 id="vitals-title" className="mb-2 text-xl font-bold text-[#333]">
            Vitals
          </h3>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            {(
              [
                ["bloodPressure", "Blood pressure", "120/80"],
                ["heartRate", "Heart rate (bpm)", "72"],
                ["spo2", "SpO₂ (%)", "98"],
              ] as const
            ).map(([k, label, ph]) => (
              <label key={k} className="text-[#333]">
                <span className="font-bold">{label}</span>
                <input
                  className="field-input mt-1"
                  readOnly={readOnly}
                  placeholder={readOnly ? "" : ph}
                  value={value.vitals[k]}
                  onChange={(e) =>
                    onChange({
                      ...value,
                      vitals: { ...value.vitals, [k]: e.target.value },
                    })
                  }
                />
              </label>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
