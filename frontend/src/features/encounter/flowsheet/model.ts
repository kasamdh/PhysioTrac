// Mirrors PhysioTrac.Domain.Enums.InterventionCategory.
export const InterventionCategories: Record<number, string> = {
  0: "Therapeutic exercise",
  1: "Manual therapy",
  2: "Therapeutic activity",
  3: "Neuromuscular re-education",
  4: "Gait training",
  5: "Self-care / home management",
  6: "Patient education",
  7: "Other",
  8: "Canalith repositioning",
  9: "Modalities",
  10: "Dry needling",
  11: "Home exercise program",
};

// Mirrors InterventionStatus.
export const StatusLabels: Record<number, string> = {
  0: "Completed",
  1: "Modified",
  2: "Held",
  3: "Discontinued",
};

// Matches FlowsheetEntryDto.
export interface FlowsheetEntry {
  description: string;
  category: number | null;
  cptCode: string | null;
  isTimed: boolean;
  startTime: string | null; // "HH:mm" or "HH:mm:ss"
  endTime: string | null;
  minutes: number;
  units: number | null;
  sets: number | null;
  repetitions: number | null;
  resistance: string | null;
  duration: string | null;
  distance: string | null;
  position: string | null;
  equipment: string | null;
  assistanceLevel: string | null;
  cueing: string | null;
  modification: string | null;
  patientResponse: string | null;
  painBefore: number | null;
  painAfter: number | null;
  status: number;
  comment: string | null;
  bodyRegion: string | null;
  libraryItemId: string | null;
  carriedForwardFromNoteId: string | null;
  carryForwardReviewed: boolean;
  id?: string | null;
}

export interface FlowsheetWarning {
  code: string;
  message: string;
  entry?: number | null;
}

export interface FlowsheetSummary {
  timedMinutes: number;
  untimedServices: number;
  estimatedTimedUnits: number;
  enteredTimedUnits: number | null;
  ruleVariant: string;
  warnings: FlowsheetWarning[];
}

export interface PreviousFlowsheet {
  noteId: string;
  serviceDate: string;
  entries: FlowsheetEntry[];
}

// Matches InterventionLibraryItemDto.
export interface LibraryItem {
  id: string;
  code: string;
  name: string;
  category: number;
  cptCode: string | null;
  isTimed: boolean;
  bodyRegion: string | null;
  description: string | null;
  defaultSets: number | null;
  defaultRepetitions: number | null;
  defaultResistance: string | null;
  defaultDuration: string | null;
  defaultEquipment: string | null;
  defaultPosition: string | null;
  isActive: boolean;
  isSystem: boolean;
  isFavorite: boolean;
}

export interface GroupItem {
  name: string;
  category: number;
  isTimed: boolean;
  cptCode: string | null;
  sets: number | null;
  repetitions: number | null;
  resistance: string | null;
  duration: string | null;
  equipment: string | null;
  position: string | null;
  libraryItemId: string | null;
}

export interface InterventionGroup {
  id: string;
  name: string;
  description: string | null;
  isShared: boolean;
  isMine: boolean;
  isFavorite: boolean;
  items: GroupItem[];
}

export const blankEntry = (
  over: Partial<FlowsheetEntry> = {},
): FlowsheetEntry => ({
  description: "",
  category: null,
  cptCode: null,
  isTimed: true,
  startTime: null,
  endTime: null,
  minutes: 0,
  units: null,
  sets: null,
  repetitions: null,
  resistance: null,
  duration: null,
  distance: null,
  position: null,
  equipment: null,
  assistanceLevel: null,
  cueing: null,
  modification: null,
  patientResponse: null,
  painBefore: null,
  painAfter: null,
  status: 0,
  comment: null,
  bodyRegion: null,
  libraryItemId: null,
  carriedForwardFromNoteId: null,
  carryForwardReviewed: false,
  ...over,
});

export const fromLibrary = (i: LibraryItem): FlowsheetEntry =>
  blankEntry({
    description: i.name,
    category: i.category,
    cptCode: i.cptCode,
    isTimed: i.isTimed,
    bodyRegion: i.bodyRegion,
    sets: i.defaultSets,
    repetitions: i.defaultRepetitions,
    resistance: i.defaultResistance,
    duration: i.defaultDuration,
    equipment: i.defaultEquipment,
    position: i.defaultPosition,
    libraryItemId: i.id,
  });

export const fromGroupItem = (i: GroupItem): FlowsheetEntry =>
  blankEntry({
    description: i.name,
    category: i.category,
    cptCode: i.cptCode,
    isTimed: i.isTimed,
    sets: i.sets,
    repetitions: i.repetitions,
    resistance: i.resistance,
    duration: i.duration,
    equipment: i.equipment,
    position: i.position,
    libraryItemId: i.libraryItemId,
  });

const toMinutes = (t: string | null) => {
  if (!t) return null;
  const [h, m] = t.split(":").map(Number);
  return Number.isNaN(h) || Number.isNaN(m) ? null : h * 60 + m;
};

/** Units for total timed minutes (EightMinuteRuleCalculator). */
export function unitsFor(minutes: number, variant: string) {
  if (minutes < 8) return 0;
  return variant === "RoundedFifteenMinute"
    ? Math.round(minutes / 15)
    : 1 + Math.floor((minutes - 8) / 15);
}

/** Client mirror of FlowsheetRules.Analyze, for live totals and warnings
 * while typing (the server's summary is authoritative). */
export function analyze(
  entries: FlowsheetEntry[],
  variant: string,
): FlowsheetSummary {
  const performed = entries
    .map((e, i) => ({ e, i }))
    .filter((x) => x.e.status === 0 || x.e.status === 1);
  const timed = performed.filter((x) => x.e.isTimed);
  const timedMinutes = timed.reduce((s, x) => s + (x.e.minutes || 0), 0);
  const estimated = unitsFor(timedMinutes, variant);
  const entered = timed.some((x) => x.e.units != null)
    ? timed.reduce((s, x) => s + (x.e.units ?? 0), 0)
    : null;
  const warnings: FlowsheetWarning[] = [];
  for (const { e, i } of performed) {
    const n = i + 1;
    if (!e.patientResponse?.trim())
      warnings.push({
        code: "missing_response",
        message: `Entry ${n} (${e.description}): add the patient's response.`,
        entry: n,
      });
    if (e.isTimed && !e.minutes)
      warnings.push({
        code: "no_minutes",
        message: `Entry ${n} (${e.description}): a timed service has no minutes.`,
        entry: n,
      });
    const s = toMinutes(e.startTime);
    const end = toMinutes(e.endTime);
    if (s != null && end != null) {
      if (end <= s)
        warnings.push({
          code: "end_before_start",
          message: `Entry ${n} (${e.description}): the end time is not after the start time.`,
          entry: n,
        });
      else if (Math.abs(end - s - (e.minutes || 0)) > 1)
        warnings.push({
          code: "minutes_mismatch",
          message: `Entry ${n} (${e.description}): ${e.minutes || 0} min recorded but ${end - s} min between start and end.`,
          entry: n,
        });
    }
    if (!e.isTimed && (e.units ?? 0) > 1)
      warnings.push({
        code: "untimed_units",
        message: `Entry ${n} (${e.description}): an untimed service is usually 1 unit.`,
        entry: n,
      });
  }
  const withTimes = timed.filter((x) => {
    const s = toMinutes(x.e.startTime);
    const end = toMinutes(x.e.endTime);
    return s != null && end != null && end > s;
  });
  for (let a = 0; a < withTimes.length; a++)
    for (let b = a + 1; b < withTimes.length; b++) {
      const x = withTimes[a];
      const y = withTimes[b];
      if (
        toMinutes(x.e.startTime)! < toMinutes(y.e.endTime)! &&
        toMinutes(y.e.startTime)! < toMinutes(x.e.endTime)!
      )
        warnings.push({
          code: "overlap",
          message: `Entries ${x.i + 1} and ${y.i + 1} overlap in time (${x.e.description} / ${y.e.description}).`,
          entry: y.i + 1,
        });
    }
  if (entered != null && entered !== estimated)
    warnings.push({
      code: "units_mismatch",
      message: `${entered} timed units entered, but ${timedMinutes} timed minutes give ${estimated} under the ${variant} rule.`,
    });
  return {
    timedMinutes,
    untimedServices: performed.filter((x) => !x.e.isTimed).length,
    estimatedTimedUnits: estimated,
    enteredTimedUnits: entered,
    ruleVariant: variant,
    warnings,
  };
}

/** Minutes between two "HH:mm" times, or null. */
export function minutesBetween(start: string | null, end: string | null) {
  const s = toMinutes(start);
  const e = toMinutes(end);
  return s != null && e != null && e > s ? e - s : null;
}

/** One line about an entry's dose, for comparison and read-only display. */
export function doseText(e: FlowsheetEntry) {
  return [
    e.sets != null && e.repetitions != null
      ? `${e.sets}x${e.repetitions}`
      : e.sets != null
        ? `${e.sets} sets`
        : e.repetitions != null
          ? `${e.repetitions} reps`
          : null,
    e.resistance,
    e.duration,
    e.distance,
    e.minutes ? `${e.minutes} min` : null,
    e.assistanceLevel,
  ]
    .filter(Boolean)
    .join(", ");
}
