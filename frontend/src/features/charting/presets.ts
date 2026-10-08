import type { ObjectiveDetails, SubjectiveDetails } from "./types";

export const emptySubjective: SubjectiveDetails = {
  painNow: null,
  painBest: null,
  painWorst: null,
  painLocation: "",
  hepCompliance: "",
};

const emptyVitals = { bloodPressure: "", heartRate: "", spo2: "" };

/** Parse stored findings, filling missing parts with defaults (older notes,
 * or notes written elsewhere, may hold a subset or just "{}"). */
export function parseSubjective(
  json: string | null | undefined,
): SubjectiveDetails {
  try {
    return {
      ...emptySubjective,
      ...(JSON.parse(json || "{}") as Partial<SubjectiveDetails>),
    };
  } catch {
    return { ...emptySubjective };
  }
}

export function parseObjective(
  json: string | null | undefined,
): ObjectiveDetails {
  try {
    const o = JSON.parse(json || "{}") as Partial<ObjectiveDetails>;
    return {
      rom: Array.isArray(o.rom) ? o.rom : [],
      mmt: Array.isArray(o.mmt) ? o.mmt : [],
      specialTests: Array.isArray(o.specialTests) ? o.specialTests : [],
      vitals: { ...emptyVitals, ...(o.vitals ?? {}) },
    };
  } catch {
    return { rom: [], mmt: [], specialTests: [], vitals: { ...emptyVitals } };
  }
}

/** Common motions per region with typical normal range in degrees (AAOS
 * reference values), shown as a guide beside each measurement. */
export const ROM_PRESETS: Record<string, { motion: string; normal: number }[]> =
  {
    Cervical: [
      { motion: "Flexion", normal: 45 },
      { motion: "Extension", normal: 45 },
      { motion: "Rotation", normal: 80 },
      { motion: "Side bend", normal: 45 },
    ],
    Shoulder: [
      { motion: "Flexion", normal: 180 },
      { motion: "Abduction", normal: 180 },
      { motion: "External rotation", normal: 90 },
      { motion: "Internal rotation", normal: 70 },
      { motion: "Extension", normal: 60 },
    ],
    Elbow: [
      { motion: "Flexion", normal: 150 },
      { motion: "Extension", normal: 0 },
    ],
    Wrist: [
      { motion: "Flexion", normal: 80 },
      { motion: "Extension", normal: 70 },
    ],
    Lumbar: [
      { motion: "Flexion", normal: 60 },
      { motion: "Extension", normal: 25 },
      { motion: "Side bend", normal: 25 },
    ],
    Hip: [
      { motion: "Flexion", normal: 120 },
      { motion: "Extension", normal: 30 },
      { motion: "Abduction", normal: 45 },
      { motion: "External rotation", normal: 45 },
      { motion: "Internal rotation", normal: 45 },
    ],
    Knee: [
      { motion: "Flexion", normal: 135 },
      { motion: "Extension", normal: 0 },
    ],
    Ankle: [
      { motion: "Dorsiflexion", normal: 20 },
      { motion: "Plantarflexion", normal: 50 },
      { motion: "Inversion", normal: 35 },
      { motion: "Eversion", normal: 15 },
    ],
  };

export const normalFor = (joint: string, motion: string) =>
  ROM_PRESETS[joint]?.find((m) => m.motion === motion)?.normal;

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

export const COMMON_SPECIAL_TESTS = [
  "Straight leg raise",
  "Slump",
  "FABER",
  "FADIR",
  "Lachman",
  "Anterior drawer",
  "McMurray",
  "Hawkins-Kennedy",
  "Neer",
  "Empty can",
  "Spurling",
  "Thomas",
];

/** Mirrors PhysioTrac.Domain.Enums.OutcomeMeasure, with each tool's usual
 * maximum and which direction is better (for "change since last time"). */
export const OUTCOME_MEASURES: {
  value: number;
  label: string;
  max: number | null;
  higherIsBetter: boolean;
  unit: string;
}[] = [
  {
    value: 0,
    label: "LEFS (Lower Extremity Functional Scale)",
    max: 80,
    higherIsBetter: true,
    unit: "points",
  },
  {
    value: 1,
    label: "ODI (Oswestry Disability Index)",
    max: 100,
    higherIsBetter: false,
    unit: "%",
  },
  {
    value: 2,
    label: "NDI (Neck Disability Index)",
    max: 50,
    higherIsBetter: false,
    unit: "points",
  },
  {
    value: 3,
    label: "QuickDASH",
    max: 100,
    higherIsBetter: false,
    unit: "points",
  },
  {
    value: 4,
    label: "TUG (Timed Up and Go)",
    max: null,
    higherIsBetter: false,
    unit: "seconds",
  },
  {
    value: 5,
    label: "Berg Balance Scale",
    max: 56,
    higherIsBetter: true,
    unit: "points",
  },
  {
    value: 6,
    label: "PSFS (Patient-Specific Functional Scale)",
    max: 10,
    higherIsBetter: true,
    unit: "points",
  },
  {
    value: 7,
    label: "5xSTS (Five Times Sit-to-Stand)",
    max: null,
    higherIsBetter: false,
    unit: "seconds",
  },
  {
    value: 8,
    label: "ABC (Activities-specific Balance Confidence)",
    max: 100,
    higherIsBetter: true,
    unit: "%",
  },
  {
    value: 9,
    label: "FGA (Functional Gait Assessment)",
    max: 30,
    higherIsBetter: true,
    unit: "points",
  },
];

export const newId = () =>
  typeof crypto !== "undefined" && "randomUUID" in crypto
    ? crypto.randomUUID()
    : `${Date.now()}-${Math.random()}`;

/** Change between two scores in the measure's own terms: "improved" means
 * the direction that is better for that tool (lower ODI, higher LEFS...). */
export function describeChange(
  measure: number,
  previous: number,
  current: number,
) {
  const info = OUTCOME_MEASURES.find((x) => x.value === measure);
  const delta = current - previous;
  if (delta === 0) return { text: "no change", tone: "text-text-muted" };
  const improved = info?.higherIsBetter ? delta > 0 : delta < 0;
  return {
    text: `${delta > 0 ? "+" : ""}${Number(delta.toFixed(1))} (${improved ? "improved" : "worse"})`,
    tone: improved ? "text-success" : "text-danger",
  };
}
