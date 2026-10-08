import {
  FREQUENCY,
  IRRITABILITY,
  PAIN_QUALITIES,
  PainScaleLabels,
  VERBAL,
  formatRating,
} from "./pain";
import type { PainAssessment } from "./types";

type RatingKey =
  "current" | "best" | "worst" | "beforeTreatment" | "afterTreatment";
const RATINGS: [RatingKey, string][] = [
  ["current", "Current pain"],
  ["best", "Best pain"],
  ["worst", "Worst pain"],
  ["beforeTreatment", "Before treatment"],
  ["afterTreatment", "After treatment"],
];

/** Structured pain assessment: ratings on the chosen scale (with last
 * visit's value beside each), location, quality, behaviour and impact. */
export function PainAssessmentPanel({
  value,
  onChange,
  readOnly,
  previous,
}: {
  value: PainAssessment;
  onChange: (next: PainAssessment) => void;
  readOnly: boolean;
  previous?: PainAssessment | null;
}) {
  const set = (patch: Partial<PainAssessment>) =>
    onChange({ ...value, ...patch });
  const text = (
    k: keyof PainAssessment,
    label: string,
    long = false,
    max = 1000,
  ) => (
    <label className={`block ${long ? "md:col-span-2" : ""}`}>
      <span className="font-bold text-[#333]">{label}</span>
      {long ? (
        <textarea
          className="field-input mt-1 min-h-[4.5rem] resize-y"
          readOnly={readOnly}
          maxLength={max}
          value={(value[k] as string | null) ?? ""}
          onChange={(e) => set({ [k]: e.target.value || null })}
        />
      ) : (
        <input
          className="field-input mt-1"
          readOnly={readOnly}
          maxLength={max}
          value={(value[k] as string | null) ?? ""}
          onChange={(e) => set({ [k]: e.target.value || null })}
        />
      )}
    </label>
  );
  const choice = (
    label: string,
    options: string[],
    current: number | null,
    apply: (v: number | null) => void,
  ) => (
    <div>
      <span className="font-bold text-[#333]" id={`pain-${label}`}>
        {label}
      </span>
      <div
        role="radiogroup"
        aria-labelledby={`pain-${label}`}
        className="mt-1 flex flex-wrap gap-1"
      >
        {options.map((o, i) => (
          <button
            key={o}
            type="button"
            role="radio"
            aria-checked={current === i}
            disabled={readOnly}
            className="seg-btn min-h-11 rounded border border-border px-3"
            onClick={() => apply(current === i ? null : i)}
          >
            {o}
          </button>
        ))}
      </div>
    </div>
  );

  return (
    <div className="space-y-4">
      <label className="block max-w-sm">
        <span className="font-bold text-[#333]">Pain scale</span>
        <select
          className="field-input mt-1"
          disabled={readOnly}
          value={value.scale}
          onChange={(e) =>
            set({
              scale: Number(e.target.value),
              current: null,
              best: null,
              worst: null,
              beforeTreatment: null,
              afterTreatment: null,
            })
          }
        >
          {Object.entries(PainScaleLabels).map(([v, l]) => (
            <option key={v} value={v}>
              {l}
            </option>
          ))}
        </select>
      </label>

      <div className="grid gap-4 lg:grid-cols-2">
        {RATINGS.map(([k, label]) => (
          <Rating
            key={k}
            label={label}
            scale={value.scale}
            value={value[k]}
            readOnly={readOnly}
            previous={
              previous && previous.scale === value.scale
                ? previous[k]
                : undefined
            }
            onChange={(v) => set({ [k]: v })}
          />
        ))}
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        {text("location", "Location", false, 300)}
        {text("duration", "Duration", false, 200)}
        <div className="md:col-span-2">
          <span className="font-bold text-[#333]" id="pain-qualities">
            Quality
          </span>
          <div
            role="group"
            aria-labelledby="pain-qualities"
            className="mt-1 flex flex-wrap gap-1"
          >
            {PAIN_QUALITIES.map((q) => {
              const on = value.qualities.includes(q);
              return (
                <button
                  key={q}
                  type="button"
                  aria-pressed={on}
                  disabled={readOnly}
                  className="seg-btn min-h-11 rounded border border-border px-3"
                  onClick={() =>
                    set({
                      qualities: on
                        ? value.qualities.filter((x) => x !== q)
                        : [...value.qualities, q],
                    })
                  }
                >
                  {q}
                </button>
              );
            })}
          </div>
        </div>
        {choice("Frequency", FREQUENCY, value.frequency, (v) =>
          set({ frequency: v }),
        )}
        {choice("Irritability", IRRITABILITY, value.irritability, (v) =>
          set({ irritability: v }),
        )}
        {text("aggravatingFactors", "Aggravating factors", true)}
        {text("easingFactors", "Easing factors", true)}
        {text("dailyPattern", "24-hour pattern", true)}
        {text("sleepImpact", "Sleep impact", true)}
        {text("functionalImpact", "Functional impact", true, 2000)}
      </div>
    </div>
  );
}

function Rating({
  label,
  scale,
  value,
  previous,
  readOnly,
  onChange,
}: {
  label: string;
  scale: number;
  value: number | null;
  previous: number | null | undefined;
  readOnly: boolean;
  onChange: (v: number | null) => void;
}) {
  const id = `pain-${label.replace(/\s+/g, "-").toLowerCase()}`;
  const options =
    scale === 2
      ? [0, 2, 4, 6, 8, 10]
      : scale === 3
        ? [0, 1, 2, 3]
        : scale === 0
          ? Array.from({ length: 11 }, (_, i) => i)
          : null;
  return (
    <div>
      <p className="mb-1 font-bold text-[#333]" id={id}>
        {label}
        <span className="ml-2 font-normal text-text-muted">
          {formatRating(scale, value)}
          {previous != null
            ? ` · last visit ${formatRating(scale, previous)}`
            : ""}
        </span>
      </p>
      {options ? (
        <div
          role="radiogroup"
          aria-labelledby={id}
          className="flex flex-wrap gap-1"
        >
          {options.map((n) => (
            <button
              key={n}
              type="button"
              role="radio"
              aria-checked={value === n}
              aria-label={scale === 3 ? VERBAL[n] : String(n)}
              disabled={readOnly}
              onClick={() => onChange(value === n ? null : n)}
              className={`h-11 min-w-11 rounded border px-2 ${
                value === n
                  ? "border-primary-deep bg-primary text-white"
                  : "border-border bg-white text-[#333] hover:bg-surface-muted"
              }`}
            >
              {scale === 3 ? VERBAL[n] : n}
            </button>
          ))}
        </div>
      ) : (
        <input
          type="range"
          min={0}
          max={100}
          step={1}
          aria-labelledby={id}
          aria-valuetext={formatRating(scale, value)}
          disabled={readOnly}
          className="w-full max-w-md"
          value={value ?? 0}
          onChange={(e) => onChange(Number(e.target.value))}
        />
      )}
      {!options && value != null && !readOnly && (
        <button
          type="button"
          className="btn-refresh ml-2"
          onClick={() => onChange(null)}
        >
          Clear
        </button>
      )}
    </div>
  );
}
