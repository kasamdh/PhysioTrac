// Mirrors GoalStatus (values are stored, so later states were appended).
export const GoalStatus = {
  Draft: 0,
  InProgress: 1,
  Met: 2,
  Discontinued: 3,
  NotStarted: 4,
  PartiallyMet: 5,
} as const;

export const GoalStatusLabels: Record<number, string> = {
  0: "Draft (needs approval)",
  1: "In progress",
  2: "Met",
  3: "Discontinued",
  4: "Not started",
  5: "Partially met",
};

/** Statuses a clinician can record, in the order offered. */
export const RECORDABLE = [
  GoalStatus.NotStarted,
  GoalStatus.InProgress,
  GoalStatus.Met,
  GoalStatus.PartiallyMet,
  GoalStatus.Discontinued,
];

export const isOpen = (status: number) =>
  status === GoalStatus.Draft ||
  status === GoalStatus.NotStarted ||
  status === GoalStatus.InProgress;

// Matches FunctionalGoalDto.
export interface Goal {
  id: string;
  patientId: string;
  functionalLimitation: string;
  functionalTask: string;
  term: number; // 0 short-term, 1 long-term
  baselineValue: number;
  targetValue: number;
  currentValue: number | null;
  unit: string;
  measurementMethod: string;
  targetDate: string;
  status: number;
  progressPercent: number | null;
  approvedById: string | null;
  approvedAt: string | null;
  planOfCareId: string | null;
  comments: string | null;
  version: number;
}

// Matches NoteGoalProgressDto.
export interface GoalProgressRow {
  goalId: string;
  currentValue: number | null;
  status: number;
  comment: string | null;
  includeInNarrative: boolean;
  // The goal as documented (filled in by the server on save).
  goalVersion?: number;
  term?: number;
  functionalTask?: string | null;
  functionalLimitation?: string | null;
  baselineValue?: number;
  targetValue?: number;
  unit?: string | null;
  measurementMethod?: string | null;
  targetDate?: string | null;
  previousValue?: number | null;
  progressPercent?: number | null;
  id?: string | null;
}

// Matches GoalHistoryDto (Kind: 0 created, 1 approved, 2 edited, 3 progress).
export interface GoalHistoryEntry {
  id: string;
  kind: number;
  goalVersion: number;
  noteId: string | null;
  noteServiceDate: string | null;
  recordedByName: string | null;
  recordedAt: string;
  status: number;
  currentValue: number | null;
  progressPercent: number | null;
  comment: string | null;
  snapshot: {
    term: number;
    functionalTask: string;
    functionalLimitation: string;
    baselineValue: number;
    targetValue: number;
    unit: string;
    measurementMethod: string;
    targetDate: string;
    comments: string | null;
  };
}

export const HistoryKindLabels: Record<number, string> = {
  0: "Created",
  1: "Approved",
  2: "Edited",
  3: "Progress",
};

/** Mirror of FunctionalGoalMath.Progress: 0-100 from baseline to target. */
export function progressPercent(
  baseline: number,
  target: number,
  current: number | null | undefined,
) {
  if (current == null || target === baseline) return null;
  const p = ((current - baseline) / (target - baseline)) * 100;
  return Math.max(0, Math.min(100, Math.round(p)));
}

export const formatDate = (iso: string | null | undefined) => {
  if (!iso) return "—";
  const [y, m, d] = iso.slice(0, 10).split("-");
  return `${m}/${d}/${y}`;
};

const termShort = (term: number | undefined) => (term === 1 ? "LTG" : "STG");

/** The goal as one functional, measurable, time-bound sentence. */
export function statement(g: {
  term?: number;
  functionalTask?: string | null;
  baselineValue?: number;
  targetValue?: number;
  unit?: string | null;
  measurementMethod?: string | null;
  targetDate?: string | null;
}) {
  const task = (g.functionalTask ?? "").trim().replace(/\.$/, "");
  const lower = task.charAt(0).toLowerCase() + task.slice(1);
  const measured = g.measurementMethod?.trim()
    ? `, measured by ${g.measurementMethod.trim()}`
    : "";
  return `Patient will ${lower} (baseline ${g.baselineValue} → target ${g.targetValue} ${g.unit ?? ""}${measured}) by ${formatDate(g.targetDate)}.`.replace(
    /\s+\)/,
    ")",
  );
}

/** One line of goal progress for a note's narrative. */
export function narrativeLine(row: GoalProgressRow, index: number) {
  const pct = progressPercent(
    row.baselineValue ?? 0,
    row.targetValue ?? 0,
    row.currentValue,
  );
  const parts = [
    `${termShort(row.term)} ${index}: ${(row.functionalTask ?? "").replace(/\.$/, "")}`,
    `baseline ${row.baselineValue} ${row.unit ?? ""}`.trim(),
    row.previousValue != null ? `previous ${row.previousValue}` : null,
    row.currentValue != null ? `today ${row.currentValue}` : null,
    `target ${row.targetValue} by ${formatDate(row.targetDate)}`,
    pct != null ? `${pct}% toward target` : null,
    GoalStatusLabels[row.status]?.toLowerCase(),
  ].filter(Boolean);
  return `${parts.join("; ")}.${row.comment ? ` ${row.comment.trim()}` : ""}`;
}

/** The selected goals' progress, ready to insert into a note field. */
export function narrative(rows: GoalProgressRow[]) {
  return rows
    .filter((r) => r.includeInNarrative)
    .map((r, i) => narrativeLine(r, i + 1))
    .join("\n");
}

const VAGUE =
  /\b(improve[sd]?|increase[sd]?|decrease[sd]?|reduce[sd]?|better|enhance[sd]?|maximi[sz]e)\b/i;

/** Mirror of GoalRules.WordingAdvice — advice only, never blocking. */
export function wordingAdvice(
  task: string,
  method: string,
  targetDate: string,
  today: string,
) {
  const advice: string[] = [];
  const t = task.trim();
  if (!t) return advice;
  const hasNumber = /\d/.test(t);
  if (VAGUE.test(t) && !hasNumber)
    advice.push(
      'Say what the patient will be able to do and how much (e.g. "climb 12 stairs with one rail"), not only what will improve.',
    );
  if (!hasNumber)
    advice.push(
      "Include a measurable amount in the goal (distance, time, repetitions, score, assistance level).",
    );
  if (!method.trim()) advice.push("Say how the goal will be measured.");
  if (targetDate && targetDate < today)
    advice.push(
      "The target date has passed — update it or record the goal's status.",
    );
  return advice;
}
