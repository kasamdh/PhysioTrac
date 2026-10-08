import { InterventionsPanel } from "../charting/components/InterventionsPanel";
import { MeasurementTables } from "../charting/components/MeasurementTables";
import { parseObjective, parseSubjective } from "../charting/presets";
import type { ChartNote, NoteAddendum } from "../charting/types";
import { NoteCharting } from "../encounter/PainSummary";

/** A note's documented content, read-only: narrative, pain, measurements,
 * interventions with patient response, plan of care, and addenda. Shared by
 * the patient's documentation list and the printable note. */
export function NoteBody({
  note,
  addenda = [],
}: {
  note: ChartNote;
  addenda?: NoteAddendum[];
}) {
  const subj = parseSubjective(note.subjectiveDetailsJson);
  const obj = parseObjective(note.objectiveMeasurementsJson);
  const measured = obj.rom.length + obj.mmt.length + obj.specialTests.length;
  const hasVitals =
    !!obj.vitals.bloodPressure || !!obj.vitals.heartRate || !!obj.vitals.spo2;
  return (
    <div className="space-y-4">
      {note.amendsNoteId && note.amendmentReason && (
        <Field label="Reason for amendment" value={note.amendmentReason} />
      )}
      <Field label="Subjective" value={note.subjective} />
      <NoteCharting noteId={note.id} />
      {(subj.painNow !== null || subj.painLocation) && (
        <p className="text-[#333]">
          Pain now {subj.painNow ?? "—"}/10 · best {subj.painBest ?? "—"} ·
          worst {subj.painWorst ?? "—"}
          {subj.painLocation ? ` · ${subj.painLocation}` : ""}
        </p>
      )}
      {(measured > 0 || hasVitals) && (
        <MeasurementTables
          value={obj}
          previous={null}
          readOnly
          onChange={() => {}}
        />
      )}
      <Field label="Objective" value={note.objective} />
      <div>
        <p className="mb-1 font-bold text-[#333]">Interventions</p>
        <InterventionsPanel noteId={note.id} readOnly />
      </div>
      <Field label="Treatment summary" value={note.interventions} />
      <Field label="Assessment" value={note.assessment} />
      <Field label="Plan" value={note.plan} />
      {(note.planOfCareStart || note.frequencyPerWeek) && (
        <div>
          <p className="font-bold text-[#333]">Plan of care</p>
          <p className="text-[#333]">
            {note.planOfCareStart ? formatDate(note.planOfCareStart) : "—"} to{" "}
            {note.planOfCareEnd ? formatDate(note.planOfCareEnd) : "—"}
            {note.frequencyPerWeek ? ` · ${note.frequencyPerWeek}x/week` : ""}
            {note.durationWeeks ? ` for ${note.durationWeeks} weeks` : ""}
          </p>
        </div>
      )}
      {addenda.length > 0 && <AddendaList addenda={addenda} />}
    </div>
  );
}

export function AddendaList({ addenda }: { addenda: NoteAddendum[] }) {
  return (
    <div>
      <p className="mb-1 font-bold text-[#333]">Addenda</p>
      <ol className="space-y-2">
        {addenda.map((a) => (
          <li
            key={a.id}
            className="rounded-md border border-border bg-surface-muted px-3 py-2"
          >
            <p className="text-text-muted">
              {new Date(a.createdAt).toLocaleString("en-US")}
              {a.authorName ? ` · ${a.authorName}` : ""} · {a.reason}
            </p>
            <p className="whitespace-pre-wrap text-[#333]">{a.body}</p>
          </li>
        ))}
      </ol>
    </div>
  );
}

function Field({ label, value }: { label: string; value?: string | null }) {
  if (!value) return null;
  return (
    <div>
      <p className="font-bold text-[#333]">{label}</p>
      <p className="whitespace-pre-wrap text-[#333]">{value}</p>
    </div>
  );
}

const formatDate = (iso: string) => {
  const [y, m, d] = iso.slice(0, 10).split("-");
  return `${m}/${d}/${y}`;
};
