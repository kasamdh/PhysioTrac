// Mirrors OutcomeScoringMethod.
export const Scoring = {
  Sum: 0,
  PercentOfAnswered: 1,
  ProratedSum: 2,
  QuickDash: 3,
  Mean: 4,
  TimedMean: 5,
} as const;

// Matches OutcomeMeasureDefinitionDto (GET /outcomes/measures).
export interface OutcomeDefinition {
  measure: number;
  code: string;
  abbreviation: string;
  name: string;
  domain: string;
  description: string;
  scoring: number;
  items: { key: string; label: string }[];
  itemMin: number;
  itemMax: number;
  itemStep: number;
  options: { value: number; label: string }[];
  minAnswered: number;
  itemsNamedByPatient: boolean;
  scoreMin: number;
  scoreMax: number | null;
  unit: string;
  higherIsBetter: boolean;
  decimals: number;
  meaningfulChange: number | null;
  meaningfulChangeNote: string | null;
  bands: { min: number; label: string }[];
  reference: string | null;
}

export interface ItemResponse {
  key: string;
  value: number | null;
  label?: string | null;
}

// Matches OutcomeScoreDto.
export interface OutcomeScore {
  id: string;
  patientId: string;
  noteId: string | null;
  measure: number;
  measuredOn: string;
  score: number;
  maximumScore: number | null;
  interpretation: string | null;
  itemResponses: ItemResponse[] | null;
  notes: string | null;
  isLocked: boolean;
}

export interface ScoreResult {
  score: number | null;
  interpretation: string;
  errors: string[];
}

const round = (n: number, decimals: number) => {
  const f = 10 ** decimals;
  // Away from zero at the midpoint, like the server's MidpointRounding.AwayFromZero.
  return (Math.sign(n) * Math.round(Math.abs(n) * f + Number.EPSILON)) / f;
};

/** Client mirror of OutcomeMeasureCatalog.Score, for the live score while
 * entering responses (the server's score is the one saved). */
export function scoreResponses(
  d: OutcomeDefinition,
  responses: ItemResponse[],
): ScoreResult {
  const errors: string[] = [];
  const answered: number[] = [];
  for (const item of d.items) {
    const r = responses.find((x) => x.key === item.key);
    if (r?.value == null || Number.isNaN(r.value)) continue;
    const v = r.value;
    if (v < d.itemMin || v > d.itemMax)
      errors.push(`${item.label}: enter ${d.itemMin}–${d.itemMax}.`);
    else if (d.itemStep > 0 && !isMultiple(v, d.itemStep))
      errors.push(`${item.label}: use steps of ${d.itemStep}.`);
    else if (d.itemsNamedByPatient && !r.label?.trim())
      errors.push(`${item.label}: name the activity.`);
    answered.push(v);
  }
  if (answered.length < d.minAnswered)
    errors.push(
      d.minAnswered === d.items.length
        ? `Answer all ${d.items.length} items (${answered.length} answered).`
        : `Answer at least ${d.minAnswered} of ${d.items.length} items (${answered.length} answered).`,
    );
  if (errors.length) return { score: null, interpretation: "", errors };

  const sum = answered.reduce((s, v) => s + v, 0);
  const n = answered.length;
  const raw =
    d.scoring === Scoring.Sum
      ? sum
      : d.scoring === Scoring.PercentOfAnswered
        ? (sum / (d.itemMax * n)) * 100
        : d.scoring === Scoring.ProratedSum
          ? (sum * d.items.length) / n
          : d.scoring === Scoring.QuickDash
            ? (sum / n - 1) * 25
            : sum / n;
  const score = round(raw, d.decimals);
  return { score, interpretation: interpret(d, score), errors: [] };
}

const isMultiple = (v: number, step: number) => {
  const q = v / step;
  return Math.abs(q - Math.round(q)) < 1e-9;
};

/** Mirror of OutcomeMeasureCatalog.Interpret. */
export function interpret(d: OutcomeDefinition, score: number) {
  const band = [...d.bands]
    .sort((a, b) => a.min - b.min)
    .filter((b) => score >= b.min)
    .pop();
  if (band) return band.label;
  if (d.scoreMax !== null && d.scoreMax > d.scoreMin) {
    const pct = Math.round(
      ((score - d.scoreMin) / (d.scoreMax - d.scoreMin)) * 100,
    );
    return d.higherIsBetter
      ? `${pct}% of maximum function (higher is better)`
      : `${pct}% of maximum disability (lower is better)`;
  }
  return d.higherIsBetter ? "Higher is better" : "Lower is better";
}

/** Range check for a total entered without item responses. */
export function totalError(d: OutcomeDefinition, score: number | null) {
  if (score === null || Number.isNaN(score)) return "Enter the total.";
  if (d.scoreMax === null)
    return score > 0 ? null : "The time must be more than 0.";
  return score < d.scoreMin || score > d.scoreMax
    ? `Enter ${d.scoreMin}–${d.scoreMax}.`
    : null;
}

export interface ChangeVerdict {
  delta: number;
  text: string;
  tone: string;
}

/** Describes a change between two scores of a measure: direction (which
 * way is better depends on the measure) and whether it reaches the
 * measure's meaningful-change value. */
export function describeChange(
  d: OutcomeDefinition,
  from: number,
  to: number,
): ChangeVerdict {
  const delta = round(to - from, Math.max(d.decimals, 1));
  const sign = delta > 0 ? "+" : "";
  const amount = `${sign}${delta} ${d.unit === "%" ? "points" : d.unit}`;
  if (delta === 0) return { delta, text: "no change", tone: "text-text-muted" };
  const better = d.higherIsBetter ? delta > 0 : delta < 0;
  const meaningful =
    d.meaningfulChange !== null && Math.abs(delta) >= d.meaningfulChange;
  if (better)
    return {
      delta,
      text: `${amount} — ${meaningful ? "meaningful improvement" : "improved"}`,
      tone: "text-success",
    };
  return {
    delta,
    text: `${amount} — ${meaningful ? "meaningful decline" : "worse"}`,
    tone: "text-danger",
  };
}

export interface Comparison {
  history: OutcomeScore[]; // oldest first
  baseline: OutcomeScore;
  previous: OutcomeScore | null;
  current: OutcomeScore;
  fromBaseline: ChangeVerdict | null;
  fromPrevious: ChangeVerdict | null;
}

/** Baseline (first), previous and current (latest) scores of one measure,
 * with the change from baseline and from the previous score. */
export function compare(
  d: OutcomeDefinition,
  scores: OutcomeScore[],
): Comparison | null {
  const history = scores
    .filter((s) => s.measure === d.measure)
    .sort((a, b) => a.measuredOn.localeCompare(b.measuredOn));
  if (!history.length) return null;
  const baseline = history[0];
  const current = history[history.length - 1];
  const previous = history.length > 1 ? history[history.length - 2] : null;
  return {
    history,
    baseline,
    previous,
    current,
    fromBaseline:
      history.length > 1
        ? describeChange(d, baseline.score, current.score)
        : null,
    fromPrevious: previous
      ? describeChange(d, previous.score, current.score)
      : null,
  };
}

export const formatScore = (d: OutcomeDefinition | undefined, n: number) =>
  d?.scoreMax != null
    ? `${n}/${d.scoreMax}${d.unit === "%" ? "%" : ""}`
    : `${n} ${d?.unit === "seconds" ? "s" : (d?.unit ?? "")}`.trim();

export const formatDate = (iso: string) => {
  const [y, m, d] = iso.split("-");
  return `${m}/${d}/${y}`;
};
