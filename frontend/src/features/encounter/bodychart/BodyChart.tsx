import { useRef, useState, type PointerEvent } from "react";
import type { BodyFinding } from "../types";
import {
  BodySide,
  BodySideLabels,
  BodyView,
  FindingColors,
  FindingMarks,
  FindingTypeLabels,
  REGIONS,
  RegionLabels,
  VIEW_H,
  VIEW_W,
  centerOf,
  describe,
  regionKeys,
  sideForPoint,
  type Region,
} from "./regions";

const VIEW_LABELS: Record<number, string> = { 0: "Front", 1: "Back" };

/** Interactive body chart: tap or click the front or back drawing to mark
 * a finding (mouse, pen and touch use the same pointer events); select a
 * marker to edit or drag it. The table below is the full non-visual
 * alternative -- every finding can be added and edited there with the
 * keyboard. Read-only after signing; last visit's findings can be overlaid. */
export function BodyChart({
  findings,
  onChange,
  readOnly,
  previous,
  previousDate,
}: {
  findings: BodyFinding[];
  onChange?: (next: BodyFinding[]) => void;
  readOnly: boolean;
  previous?: BodyFinding[];
  previousDate?: string;
}) {
  const [selected, setSelected] = useState<number | null>(null);
  const [tool, setTool] = useState(0);
  const [showPrevious, setShowPrevious] = useState(false);
  const set = (next: BodyFinding[]) => onChange?.(next);
  const update = (i: number, patch: Partial<BodyFinding>) =>
    set(findings.map((f, j) => (j === i ? { ...f, ...patch } : f)));
  const remove = (i: number) => {
    set(findings.filter((_, j) => j !== i));
    setSelected(null);
  };
  const add = (f: BodyFinding) => {
    set([...findings, f]);
    setSelected(findings.length);
  };

  return (
    <div className="space-y-3">
      {!readOnly && (
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-bold text-[#333]" id="bc-tool-label">
            Mark
          </span>
          <div
            role="radiogroup"
            aria-labelledby="bc-tool-label"
            className="flex flex-wrap gap-1"
          >
            {Object.entries(FindingTypeLabels).map(([v, label]) => (
              <button
                key={v}
                type="button"
                role="radio"
                aria-checked={tool === Number(v)}
                className="seg-btn min-h-11 rounded border border-border px-2"
                onClick={() => setTool(Number(v))}
              >
                <span
                  aria-hidden="true"
                  className="mr-1 font-bold"
                  style={{ color: FindingColors[Number(v)] }}
                >
                  {FindingMarks[Number(v)]}
                </span>
                {label}
              </button>
            ))}
          </div>
        </div>
      )}
      {!!previous?.length && (
        <label className="flex min-h-11 items-center gap-2">
          <input
            type="checkbox"
            className="h-5 w-5"
            checked={showPrevious}
            onChange={(e) => setShowPrevious(e.target.checked)}
          />
          Show last visit’s findings{previousDate ? ` (${previousDate})` : ""} —
          dashed
        </label>
      )}

      <div
        className="grid grid-cols-2 gap-3 sm:gap-6"
        style={{ maxWidth: 560 }}
      >
        {[BodyView.Front, BodyView.Back].map((view) => (
          <Figure
            key={view}
            view={view}
            findings={findings}
            previous={showPrevious ? (previous ?? []) : []}
            selected={selected}
            readOnly={readOnly}
            onAdd={(point) =>
              add({
                ...point,
                findingType: tool,
                severity: null,
                radiatesTo: null,
                annotation: null,
                comment: null,
              })
            }
            onSelect={setSelected}
            onMove={(i, x, y) => update(i, { x, y })}
          />
        ))}
      </div>
      {!readOnly && (
        <p className="text-text-muted">
          Choose what to mark, then tap the body. Select a marker to change or
          drag it.
        </p>
      )}

      <FindingsTable
        findings={findings}
        readOnly={readOnly}
        selected={selected}
        onSelect={setSelected}
        onUpdate={update}
        onRemove={remove}
        onAdd={add}
      />
      {showPrevious && !!previous?.length && (
        <div>
          <p className="font-bold text-[#333]">
            Last visit{previousDate ? ` (${previousDate})` : ""}
          </p>
          <ul className="ml-5 list-disc text-[#333]">
            {previous.map((f, i) => (
              <li key={i}>{describe(f)}</li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

function Figure({
  view,
  findings,
  previous,
  selected,
  readOnly,
  onAdd,
  onSelect,
  onMove,
}: {
  view: number;
  findings: BodyFinding[];
  previous: BodyFinding[];
  selected: number | null;
  readOnly: boolean;
  onAdd: (p: Pick<BodyFinding, "view" | "region" | "side" | "x" | "y">) => void;
  onSelect: (i: number) => void;
  onMove: (i: number, x: number, y: number) => void;
}) {
  const svgRef = useRef<SVGSVGElement>(null);
  const drag = useRef<number | null>(null);
  const regions = REGIONS[view];

  const point = (e: PointerEvent) => {
    const box = svgRef.current!.getBoundingClientRect();
    const x = Math.min(1, Math.max(0, (e.clientX - box.left) / box.width));
    const y = Math.min(1, Math.max(0, (e.clientY - box.top) / box.height));
    return {
      x: Math.round(x * 10000) / 10000,
      y: Math.round(y * 10000) / 10000,
    };
  };

  return (
    <figure>
      <svg
        ref={svgRef}
        viewBox={`0 0 ${VIEW_W} ${VIEW_H}`}
        role="img"
        aria-label={`Body chart, ${VIEW_LABELS[view].toLowerCase()} view${findings.some((f) => f.view === view) ? `, ${findings.filter((f) => f.view === view).length} findings` : ""}`}
        className={`w-full touch-none select-none rounded-md border border-border bg-white ${readOnly ? "" : "cursor-crosshair"}`}
        onPointerMove={(e) => {
          if (drag.current === null) return;
          const p = point(e);
          onMove(drag.current, p.x, p.y);
        }}
        onPointerUp={() => (drag.current = null)}
        onPointerCancel={() => (drag.current = null)}
      >
        {regions.map((g: Region, i) => {
          const key = `${g.key}-${i}`;
          const common = {
            "data-region": g.key,
            fill: "#eef3f8",
            stroke: "#9fb3c8",
            strokeWidth: 1,
            onPointerDown: readOnly
              ? undefined
              : (e: PointerEvent) => {
                  e.preventDefault();
                  const p = point(e);
                  onAdd({
                    view,
                    region: g.key,
                    side: sideForPoint(g, view, p.x),
                    ...p,
                  });
                },
          };
          return g.shape.kind === "ellipse" ? (
            <ellipse
              key={key}
              {...common}
              cx={g.shape.cx}
              cy={g.shape.cy}
              rx={g.shape.rx}
              ry={g.shape.ry}
            />
          ) : (
            <rect
              key={key}
              {...common}
              x={g.shape.x}
              y={g.shape.y}
              width={g.shape.w}
              height={g.shape.h}
              rx={5}
            />
          );
        })}
        <text x={6} y={14} fontSize={10} fill="#5b6b7b">
          {view === BodyView.Front ? "R" : "L"}
        </text>
        <text x={VIEW_W - 12} y={14} fontSize={10} fill="#5b6b7b">
          {view === BodyView.Front ? "L" : "R"}
        </text>
        {previous
          .filter((f) => f.view === view)
          .map((f, i) => (
            <circle
              key={`prev-${i}`}
              cx={f.x * VIEW_W}
              cy={f.y * VIEW_H}
              r={7}
              fill="none"
              stroke={FindingColors[f.findingType]}
              strokeWidth={1.5}
              strokeDasharray="3 2"
              aria-hidden="true"
            />
          ))}
        {findings.map((f, i) =>
          f.view !== view ? null : (
            <g
              key={i}
              role="button"
              tabIndex={0}
              aria-label={`Finding ${i + 1}: ${describe(f)}`}
              aria-pressed={selected === i}
              transform={`translate(${f.x * VIEW_W} ${f.y * VIEW_H})`}
              className="cursor-pointer outline-none"
              onPointerDown={(e) => {
                e.stopPropagation();
                onSelect(i);
                if (!readOnly) {
                  drag.current = i;
                  svgRef.current?.setPointerCapture(e.pointerId);
                }
              }}
              onKeyDown={(e) => {
                if (e.key === "Enter" || e.key === " ") {
                  e.preventDefault();
                  onSelect(i);
                  document.getElementById(`bc-row-${i}`)?.focus();
                }
              }}
            >
              <circle
                r={selected === i ? 9 : 7.5}
                fill={FindingColors[f.findingType]}
                stroke={selected === i ? "#111" : "#fff"}
                strokeWidth={selected === i ? 2 : 1.5}
              />
              <text
                textAnchor="middle"
                dy="3"
                fontSize={7}
                fontWeight={700}
                fill="#fff"
                aria-hidden="true"
              >
                {FindingMarks[f.findingType]}
              </text>
            </g>
          ),
        )}
      </svg>
      <figcaption className="mt-1 text-center text-text-muted">
        {VIEW_LABELS[view]}
      </figcaption>
    </figure>
  );
}

function FindingsTable({
  findings,
  readOnly,
  selected,
  onSelect,
  onUpdate,
  onRemove,
  onAdd,
}: {
  findings: BodyFinding[];
  readOnly: boolean;
  selected: number | null;
  onSelect: (i: number | null) => void;
  onUpdate: (i: number, patch: Partial<BodyFinding>) => void;
  onRemove: (i: number) => void;
  onAdd: (f: BodyFinding) => void;
}) {
  if (readOnly)
    return findings.length === 0 ? (
      <p className="text-text-muted">No body-chart findings.</p>
    ) : (
      <ol
        className="ml-5 list-decimal text-[#333]"
        aria-label="Body-chart findings"
      >
        {findings.map((f, i) => (
          <li key={i}>{describe(f)}</li>
        ))}
      </ol>
    );
  const cell = "field-input mt-1";
  const label = "block text-text-muted";
  return (
    <div>
      {findings.length === 0 && (
        <p className="text-text-muted">
          No findings yet — tap the drawing or use + Add finding.
        </p>
      )}
      <ol aria-label="Body-chart findings" className="space-y-2">
        {findings.map((f, i) => (
          <li
            key={i}
            id={`bc-row-${i}`}
            tabIndex={-1}
            className={`rounded-md border p-3 ${selected === i ? "border-primary bg-primary/5" : "border-border"}`}
            onFocus={() => onSelect(i)}
          >
            <div className="mb-2 flex items-center justify-between gap-2">
              <span className="font-bold text-[#333]">
                <span
                  aria-hidden="true"
                  className="mr-2 inline-block h-5 w-5 rounded-full text-center text-xs leading-5 text-white"
                  style={{ background: FindingColors[f.findingType] }}
                >
                  {FindingMarks[f.findingType]}
                </span>
                Finding {i + 1}
              </span>
              <button
                type="button"
                className="btn-refresh"
                aria-label={`Remove finding ${i + 1}`}
                onClick={() => onRemove(i)}
              >
                Remove
              </button>
            </div>
            <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
              <label className={label}>
                Finding
                <select
                  aria-label={`Finding ${i + 1} type`}
                  className={cell}
                  value={f.findingType}
                  onChange={(e) =>
                    onUpdate(i, { findingType: Number(e.target.value) })
                  }
                >
                  {Object.entries(FindingTypeLabels).map(([v, l]) => (
                    <option key={v} value={v}>
                      {l}
                    </option>
                  ))}
                </select>
              </label>
              <label className={label}>
                View
                <select
                  aria-label={`Finding ${i + 1} view`}
                  className={cell}
                  value={f.view}
                  onChange={(e) => {
                    const view = Number(e.target.value);
                    const region = regionKeys(view).includes(f.region)
                      ? f.region
                      : regionKeys(view)[0];
                    onUpdate(i, {
                      view,
                      region,
                      ...centerOf(view, region, f.side),
                    });
                  }}
                >
                  <option value={BodyView.Front}>Front</option>
                  <option value={BodyView.Back}>Back</option>
                </select>
              </label>
              <label className={label}>
                Region
                <select
                  aria-label={`Finding ${i + 1} region`}
                  className={cell}
                  value={f.region}
                  onChange={(e) =>
                    onUpdate(i, {
                      region: e.target.value,
                      ...centerOf(f.view, e.target.value, f.side),
                    })
                  }
                >
                  {regionKeys(f.view).map((k) => (
                    <option key={k} value={k}>
                      {RegionLabels[k]}
                    </option>
                  ))}
                </select>
              </label>
              <label className={label}>
                Side
                <select
                  aria-label={`Finding ${i + 1} side`}
                  className={cell}
                  value={f.side}
                  onChange={(e) => {
                    const side = Number(e.target.value);
                    onUpdate(i, { side, ...centerOf(f.view, f.region, side) });
                  }}
                >
                  {Object.entries(BodySideLabels).map(([v, l]) => (
                    <option key={v} value={v}>
                      {l}
                    </option>
                  ))}
                </select>
              </label>
              <label className={label}>
                Severity (0–10)
                <input
                  aria-label={`Finding ${i + 1} severity, 0 to 10`}
                  type="number"
                  min={0}
                  max={10}
                  inputMode="numeric"
                  className={cell}
                  value={f.severity ?? ""}
                  onChange={(e) =>
                    onUpdate(i, {
                      severity:
                        e.target.value === ""
                          ? null
                          : Math.max(0, Math.min(10, Number(e.target.value))),
                    })
                  }
                />
              </label>
              <label className={label}>
                Radiates to
                <input
                  aria-label={`Finding ${i + 1} radiates to`}
                  className={cell}
                  maxLength={200}
                  value={f.radiatesTo ?? ""}
                  onChange={(e) =>
                    onUpdate(i, { radiatesTo: e.target.value || null })
                  }
                />
              </label>
              <label
                className={`${label} col-span-2 md:col-span-1 xl:col-span-2`}
              >
                Annotation
                <input
                  aria-label={`Finding ${i + 1} annotation`}
                  className={cell}
                  maxLength={200}
                  value={f.annotation ?? ""}
                  onChange={(e) =>
                    onUpdate(i, { annotation: e.target.value || null })
                  }
                />
              </label>
              <label className={`${label} col-span-2 xl:col-span-4`}>
                Therapist comments
                <input
                  aria-label={`Finding ${i + 1} comments`}
                  className={cell}
                  maxLength={1000}
                  value={f.comment ?? ""}
                  onChange={(e) =>
                    onUpdate(i, { comment: e.target.value || null })
                  }
                />
              </label>
            </div>
          </li>
        ))}
      </ol>
      <button
        type="button"
        className="btn-refresh mt-2"
        onClick={() =>
          onAdd({
            view: BodyView.Front,
            region: "knee",
            side: BodySide.Right,
            ...centerOf(BodyView.Front, "knee", BodySide.Right),
            findingType: 0,
            severity: null,
            radiatesTo: null,
            annotation: null,
            comment: null,
          })
        }
      >
        + Add finding
      </button>
    </div>
  );
}
