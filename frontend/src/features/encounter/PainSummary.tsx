import { useQuery } from "@tanstack/react-query";
import { fetchEncounter } from "./api";
import { BodyChart } from "./bodychart/BodyChart";
import { MeasurementsPanel } from "./measurements/MeasurementsPanel";
import { SpecialTestsPanel } from "./measurements/SpecialTestsPanel";
import { FlowsheetPanel } from "./flowsheet/FlowsheetPanel";
import { GoalProgressSummary } from "./goals/GoalTracker";
import { formatScore } from "./outcomes/model";
import { useOutcomeDefinitions } from "./outcomes/queries";
import { FREQUENCY, IRRITABILITY, PainScaleLabels, formatRating } from "./pain";
import type { OutcomeScore } from "./outcomes/model";
import type { PainAssessment } from "./types";

/** A signed (or any) note's pain assessment as read-only text. */
export function PainSummary({ pain }: { pain: PainAssessment }) {
  const rows: [string, string | null][] = [
    ["Current", formatRating(pain.scale, pain.current)],
    ["Best", formatRating(pain.scale, pain.best)],
    ["Worst", formatRating(pain.scale, pain.worst)],
    ["Before treatment", formatRating(pain.scale, pain.beforeTreatment)],
    ["After treatment", formatRating(pain.scale, pain.afterTreatment)],
    ["Location", pain.location],
    ["Quality", pain.qualities.length ? pain.qualities.join(", ") : null],
    ["Frequency", pain.frequency != null ? FREQUENCY[pain.frequency] : null],
    ["Duration", pain.duration],
    [
      "Irritability",
      pain.irritability != null ? IRRITABILITY[pain.irritability] : null,
    ],
    ["Aggravating factors", pain.aggravatingFactors],
    ["Easing factors", pain.easingFactors],
    ["24-hour pattern", pain.dailyPattern],
    ["Sleep impact", pain.sleepImpact],
    ["Functional impact", pain.functionalImpact],
  ];
  return (
    <div className="break-inside-avoid">
      <p className="font-bold text-[#333]">
        Pain assessment ({PainScaleLabels[pain.scale]})
      </p>
      <dl className="mt-1 grid grid-cols-2 gap-x-6 gap-y-1 text-[#333] md:grid-cols-3">
        {rows
          .filter(([, v]) => v && v !== "—")
          .map(([k, v]) => (
            <div key={k}>
              <dt className="text-text-muted">{k}</dt>
              <dd className="whitespace-pre-wrap">{v}</dd>
            </div>
          ))}
      </dl>
    </div>
  );
}

/** The pain assessment and body chart recorded on a note (read-only), for
 * the patient's documentation and the printed note. */
export function NoteCharting({ noteId }: { noteId: string }) {
  const encounter = useQuery({
    queryKey: ["encounter", noteId],
    queryFn: () => fetchEncounter(noteId),
  });
  const e = encounter.data;
  if (
    !e ||
    (!e.pain &&
      !e.bodyChart?.length &&
      !e.measurements?.length &&
      !e.flowsheet?.length &&
      !e.goalProgress?.length &&
      !e.outcomes?.length &&
      !e.specialTests?.length)
  )
    return null;
  return (
    <div className="space-y-3">
      {e.pain && <PainSummary pain={e.pain} />}
      {!!e.bodyChart?.length && (
        <div className="break-inside-avoid">
          <p className="font-bold text-[#333]">Body chart</p>
          <BodyChart findings={e.bodyChart} readOnly />
        </div>
      )}
      {!!e.measurements?.length && (
        <div>
          <p className="font-bold text-[#333]">Objective measurements</p>
          <MeasurementsPanel
            value={e.measurements}
            onChange={() => {}}
            readOnly
            history={e.measurementHistory ?? []}
          />
        </div>
      )}
      {!!e.flowsheet?.length && (
        <div>
          <p className="font-bold text-[#333]">Interventions</p>
          <FlowsheetPanel
            value={e.flowsheet}
            onChange={() => {}}
            readOnly
            previous={null}
            ruleVariant={e.flowsheetSummary?.ruleVariant ?? "Medicare"}
            canShareGroups={false}
          />
        </div>
      )}
      {!!e.goalProgress?.length && (
        <div>
          <p className="font-bold text-[#333]">Goal progress</p>
          <GoalProgressSummary rows={e.goalProgress} />
        </div>
      )}
      {!!e.outcomes?.length && <NoteOutcomes scores={e.outcomes} />}
      {!!e.specialTests?.length && (
        <div className="break-inside-avoid">
          <p className="font-bold text-[#333]">Special tests</p>
          <SpecialTestsPanel
            value={e.specialTests}
            onChange={() => {}}
            readOnly
            history={e.specialTestHistory ?? []}
          />
        </div>
      )}
    </div>
  );
}

/** The outcome scores recorded on a note, with their interpretation. */
function NoteOutcomes({ scores }: { scores: OutcomeScore[] }) {
  const definitions = useOutcomeDefinitions();
  const def = (m: number) => definitions.data?.find((d) => d.measure === m);
  return (
    <div className="break-inside-avoid">
      <p className="font-bold text-[#333]">Outcome measures</p>
      <ul className="space-y-1">
        {scores.map((s) => (
          <li key={s.id} className="text-[#333]">
            <strong>{def(s.measure)?.abbreviation ?? "Measure"}</strong>{" "}
            {formatScore(def(s.measure), s.score)}
            {s.interpretation ? ` — ${s.interpretation}` : ""}
            {s.notes ? ` (${s.notes})` : ""}
          </li>
        ))}
      </ul>
    </div>
  );
}
