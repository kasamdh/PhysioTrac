// Mirrors PhysioTrac.Domain.Enums.MeasurementCategory.
export const Category = {
  RangeOfMotion: 0,
  Strength: 1,
  Sensation: 2,
  Dermatome: 3,
  Myotome: 4,
  Reflex: 5,
  Tone: 6,
  Coordination: 7,
  CranialNerve: 8,
  Gait: 9,
  Balance: 10,
  Functional: 11,
} as const;

export const NEURO_CATEGORIES = [2, 3, 4, 5, 6, 7, 8];
export const NeuroLabels: Record<number, string> = {
  2: "Sensation",
  3: "Dermatome",
  4: "Myotome",
  5: "Reflex",
  6: "Tone",
  7: "Coordination",
  8: "Cranial nerve",
};

// Matches ObjectiveMeasurementDto.
export interface Measurement {
  category: number;
  item: string;
  movement?: string | null;
  side?: number | null;
  mode?: string | null;
  numericValue?: number | null;
  textValue?: string | null;
  unit?: string | null;
  bodyRegion?: string | null;
  endFeel?: string | null;
  painful?: boolean | null;
  compensation?: string | null;
  assistiveDevice?: string | null;
  assistanceLevel?: string | null;
  surface?: string | null;
  condition?: string | null;
  comment?: string | null;
  id?: string | null;
}

export interface MeasurementPoint {
  serviceDate: string;
  numericValue: number | null;
  textValue: string | null;
}

// Matches MeasurementHistoryDto.
export interface MeasurementHistory {
  category: number;
  item: string;
  movement: string | null;
  side: number | null;
  mode: string | null;
  unit: string | null;
  baseline: MeasurementPoint;
  previous: MeasurementPoint | null;
}

/** Same identity as the server's MeasurementRules.Key. */
export function measurementKey(m: {
  category: number;
  item: string;
  movement?: string | null;
  side?: number | null;
  mode?: string | null;
  unit?: string | null;
}) {
  const t = (s?: string | null) => (s ?? "").trim().toLowerCase();
  return [
    m.category,
    t(m.item),
    t(m.movement),
    m.side ?? "",
    t(m.mode),
    t(m.unit),
  ].join("|");
}

export const MMT_GRADES = [
  "0",
  "1",
  "2-",
  "2",
  "2+",
  "3-",
  "3",
  "3+",
  "4-",
  "4",
  "4+",
  "5",
];
/** MMT grades as numbers so a change can be shown (4- = 3.67 ... 5 = 5). */
export function mmtScore(grade: string | null | undefined): number | null {
  if (!grade) return null;
  const base = Number.parseInt(grade, 10);
  if (Number.isNaN(base)) return null;
  return (
    base + (grade.endsWith("+") ? 1 / 3 : grade.endsWith("-") ? -1 / 3 : 0)
  );
}

export const ASSISTANCE_LEVELS = [
  "Independent",
  "Modified independent",
  "Supervision",
  "Contact guard",
  "Minimal assist",
  "Moderate assist",
  "Maximal assist",
  "Dependent",
];
export const END_FEELS = [
  "Normal",
  "Firm",
  "Hard",
  "Soft",
  "Springy block",
  "Empty",
  "Spasm",
];
export const FUNCTIONAL_ACTIVITIES = [
  "Bed mobility",
  "Transfers",
  "Sit-to-stand",
  "Squat",
  "Step-up",
  "Step-down",
  "Stairs",
  "Lifting",
  "Reaching",
  "Walking",
  "Running",
  "Work activities",
  "Sports activities",
];
export const GAIT_ITEMS = [
  "Distance",
  "Speed",
  "Endurance",
  "Deviations",
  "Safety",
];
export const BALANCE_TESTS = [
  "Static sitting",
  "Dynamic sitting",
  "Static standing",
  "Dynamic standing",
  "Feet together",
  "Tandem stance",
  "Single-leg stance",
];
export const NEURO_RESULTS: Record<number, string[]> = {
  2: ["Intact", "Impaired", "Absent", "Hyperesthesia"],
  3: ["Intact", "Impaired", "Absent"],
  4: ["Normal", "Weak", "Absent"],
  5: ["0", "1+", "2+", "3+", "4+"],
  6: ["MAS 0", "MAS 1", "MAS 1+", "MAS 2", "MAS 3", "MAS 4"],
  7: ["Normal", "Impaired"],
  8: ["Intact", "Impaired"],
};

/** A value as text with its unit ("100 deg", "4-", "0.9 m/s"). */
export function formatValue(
  v: { numericValue?: number | null; textValue?: string | null },
  unit?: string | null,
) {
  if (v.numericValue != null)
    return `${v.numericValue}${unit ? ` ${unit}` : ""}`;
  return v.textValue ?? "—";
}

const fmtDate = (iso: string) => {
  const [y, m, d] = iso.split("-");
  return `${m}/${d}/${y.slice(2)}`;
};

/** "Baseline 85 deg (09/01/26) · Previous 100 deg (09/15/26) · +15 since baseline". */
export function comparison(
  history: MeasurementHistory | undefined,
  current: { numericValue?: number | null; textValue?: string | null },
  unit?: string | null,
  mode?: string | null,
): string | null {
  if (!history) return null;
  const parts = [
    `Baseline ${formatValue(history.baseline, unit)} (${fmtDate(history.baseline.serviceDate)})`,
  ];
  // The previous visit is worth naming only when it isn't the baseline visit itself.
  if (
    history.previous &&
    history.previous.serviceDate !== history.baseline.serviceDate
  )
    parts.push(
      `Previous ${formatValue(history.previous, unit)} (${fmtDate(history.previous.serviceDate)})`,
    );
  const mmt = (mode ?? "").toUpperCase() === "MMT";
  const now = mmt ? mmtScore(current.textValue) : current.numericValue;
  const base = mmt
    ? mmtScore(history.baseline.textValue)
    : history.baseline.numericValue;
  if (now != null && base != null) {
    const delta = Math.round((now - base) * 100) / 100;
    parts.push(
      delta === 0
        ? "no change since baseline"
        : `${delta > 0 ? "+" : ""}${mmt ? `${Math.round(delta * 3)}/3 grade` : delta}${!mmt && unit ? ` ${unit}` : ""} since baseline`,
    );
  }
  return parts.join(" · ");
}

export const OutcomeLabels: Record<number, string> = {
  0: "Not tested",
  1: "Positive",
  2: "Negative",
};
