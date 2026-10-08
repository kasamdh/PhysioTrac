import type { PainAssessment } from "./types";

export const PAIN_QUALITIES = [
  "Aching",
  "Sharp",
  "Dull",
  "Burning",
  "Throbbing",
  "Stabbing",
  "Shooting",
  "Cramping",
  "Tight",
  "Tingling",
];

export const PainScaleLabels: Record<number, string> = {
  0: "Numeric 0–10",
  1: "Visual analog 0–100 mm",
  2: "Faces (Wong-Baker)",
  3: "Verbal",
};
export const VERBAL = ["None", "Mild", "Moderate", "Severe"];
export const FREQUENCY = ["Constant", "Intermittent", "Occasional"];
export const IRRITABILITY = ["Low", "Moderate", "High"];

export const emptyPain = (): PainAssessment => ({
  scale: 0,
  current: null,
  best: null,
  worst: null,
  beforeTreatment: null,
  afterTreatment: null,
  location: null,
  qualities: [],
  frequency: null,
  duration: null,
  irritability: null,
  aggravatingFactors: null,
  easingFactors: null,
  dailyPattern: null,
  sleepImpact: null,
  functionalImpact: null,
});

/** A rating as text on its scale, e.g. "6/10", "42 mm", "Moderate". */
export function formatRating(scale: number, v: number | null | undefined) {
  if (v == null) return "—";
  if (scale === 1) return `${v} mm`;
  if (scale === 3) return VERBAL[v] ?? String(v);
  return `${v}/10`;
}
