import { useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import { apiRequest } from "../../lib/apiClient";
import { fetchPatientDetail } from "../admin/api";
import { fetchOutcomes, fetchPullForward } from "../charting/api";
import { GoalsPanel } from "../charting/components/GoalsPanel";
import { InterventionsPanel } from "../charting/components/InterventionsPanel";
import { MeasurementTables } from "../charting/components/MeasurementTables";
import { describeChange } from "../charting/presets";
import {
  OUTCOME_MEASURES,
  parseObjective,
  parseSubjective,
} from "../charting/presets";
import type { ChartNote } from "../charting/types";
import { NoteStatus, NoteTypeLabels } from "../workflow/types";

interface ProgressNoteStatus {
  isDue: boolean;
  dueByDayCount: boolean;
  dueByVisitCount: boolean;
  visitsSinceLastProgressNote: number;
  reassessmentDue: string | null;
}

const fetchPatientNotes = (patientId: string) =>
  apiRequest<ChartNote[]>(`/api/v1/notes/patient/${patientId}`);
const fetchProgressStatus = (patientId: string) =>
  apiRequest<ProgressNoteStatus>(
    `/api/v1/notes/patient/${patientId}/progress-note-status`,
  );

const formatDate = (iso: string) => {
  const [y, m, d] = iso.split("-");
  return `${m}/${d}/${y}`;
};
const statusLabel = (s: number) =>
  s === NoteStatus.Draft
    ? "Draft"
    : s === NoteStatus.ReviewRequired
      ? "Awaiting cosign"
      : s === NoteStatus.Locked
        ? "Locked"
        : "Signed";
const isSigned = (s: number) =>
  s === NoteStatus.Signed || s === NoteStatus.Locked;

/** Everything documented for one patient: visit notes with their charting
 * (measurements, interventions, patient response), measurement and pain
 * trends across visits, outcome scores, goals and diagnoses. Read-only
 * overview; each note opens in Clinical Charting. */
export function PatientDocumentationPage() {
  const { patientId = "" } = useParams();
  const patient = useQuery({
    queryKey: ["admin", "patient", patientId],
    queryFn: () => fetchPatientDetail(patientId),
  });
  const notes = useQuery({
    queryKey: ["documentation", "notes", patientId],
    queryFn: () => fetchPatientNotes(patientId),
  });
  const outcomes = useQuery({
    queryKey: ["chart", "outcomes", patientId],
    queryFn: () => fetchOutcomes(patientId),
  });
  const pullForward = useQuery({
    queryKey: ["chart", "pull-forward", patientId],
    queryFn: () => fetchPullForward(patientId),
  });
  const progress = useQuery({
    queryKey: ["documentation", "progress", patientId],
    queryFn: () => fetchProgressStatus(patientId),
  });

  if (patient.isError)
    return <p className="alert-error">{patient.error.message}</p>;
  const p = patient.data;
  const all = notes.data ?? [];
  const signed = all.filter((n) => isSigned(n.status));
  const drafts = all.filter((n) => n.status === NoteStatus.Draft);

  return (
    <div className="space-y-5 pb-10">
      <div>
        <Link to="/patients" className="text-primary hover:underline">
          ‹ Patients
        </Link>
        <h1 className="mt-1 text-2xl font-bold text-[#1565b8]">
          {p ? `${p.fullName} — Documentation` : "Documentation"}
        </h1>
        {p && (
          <p className="text-text-muted">
            {p.medicalRecordNumber} · DOB {formatDate(p.dateOfBirth)} ({p.age})
            {p.phone ? ` · ${p.phone}` : ""}
          </p>
        )}
      </div>

      {(p?.precautions || !!pullForward.data?.activeDiagnoses.length) && (
        <div className="grid gap-3 sm:grid-cols-2">
          {p?.precautions && (
            <p className="rounded-md border border-danger bg-danger-light px-3 py-2 text-danger">
              <strong>Precautions:</strong> {p.precautions}
            </p>
          )}
          {!!pullForward.data?.activeDiagnoses.length && (
            <p className="rounded-md border border-border bg-surface-muted px-3 py-2 text-[#333]">
              <strong>Diagnoses:</strong>{" "}
              {pullForward.data.activeDiagnoses
                .map(
                  (d) =>
                    `${d.code} ${d.description}${d.isPrimary ? " (primary)" : ""}`,
                )
                .join("; ")}
            </p>
          )}
        </div>
      )}

      {/* At-a-glance */}
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Stat label="Signed visit notes" value={String(signed.length)} />
        <Stat
          label="Drafts to finish"
          value={String(drafts.length)}
          tone={drafts.length ? "text-warning" : undefined}
        />
        <Stat
          label="Last documented visit"
          value={signed[0] ? formatDate(signed[0].serviceDate) : "—"}
        />
        <Stat
          label="Progress note"
          value={
            progress.data ? (progress.data.isDue ? "Due now" : "Not due") : "—"
          }
          tone={progress.data?.isDue ? "text-danger" : undefined}
          detail={
            progress.data
              ? `${progress.data.visitsSinceLastProgressNote} visit${progress.data.visitsSinceLastProgressNote === 1 ? "" : "s"} since the last one`
              : undefined
          }
        />
      </div>

      <Section title="Pain & range-of-motion trend">
        <Trends notes={signed} />
      </Section>

      <Section title="Outcome measures">
        {outcomes.data && outcomes.data.length === 0 && (
          <p className="text-text-muted">No outcome measures recorded yet.</p>
        )}
        {outcomes.data && outcomes.data.length > 0 && (
          <div className="list-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Measure</th>
                  <th>Score</th>
                  <th>Change from previous</th>
                </tr>
              </thead>
              <tbody>
                {[...outcomes.data]
                  .sort((a, b) => b.measuredOn.localeCompare(a.measuredOn))
                  .map((s, _i, list) => {
                    const prev = list.find(
                      (x) =>
                        x.measure === s.measure && x.measuredOn < s.measuredOn,
                    );
                    const change = prev
                      ? describeChange(s.measure, prev.score, s.score)
                      : null;
                    return (
                      <tr key={s.id}>
                        <td data-label="Date">{formatDate(s.measuredOn)}</td>
                        <td data-label="Measure">
                          {OUTCOME_MEASURES.find((m) => m.value === s.measure)
                            ?.label ?? s.measure}
                        </td>
                        <td data-label="Score">
                          {s.score}
                          {s.maximumScore !== null ? `/${s.maximumScore}` : ""}
                        </td>
                        <td data-label="Change" className={change?.tone}>
                          {change?.text ?? "—"}
                        </td>
                      </tr>
                    );
                  })}
              </tbody>
            </table>
          </div>
        )}
      </Section>

      <Section title="Goals">
        <GoalsPanel patientId={patientId} readOnly />
      </Section>

      <Section title="Visit notes">
        {notes.isLoading && <p className="text-text-muted">Loading…</p>}
        {notes.data && notes.data.length === 0 && (
          <p className="text-text-muted">No visit notes yet.</p>
        )}
        <ul className="space-y-3">
          {all.map((n) => (
            <NoteItem key={n.id} note={n} />
          ))}
        </ul>
      </Section>
    </div>
  );
}

function Stat({
  label,
  value,
  detail,
  tone,
}: {
  label: string;
  value: string;
  detail?: string;
  tone?: string;
}) {
  return (
    <div className="rounded-lg border border-border bg-white p-3">
      <p className="text-text-muted">{label}</p>
      <p className={`text-2xl font-bold ${tone ?? "text-[#333]"}`}>{value}</p>
      {detail && <p className="text-text-muted">{detail}</p>}
    </div>
  );
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="space-y-3 rounded-lg border border-border bg-white p-4 sm:p-5">
      <h2 className="text-2xl font-bold text-[#1565b8]">{title}</h2>
      {children}
    </section>
  );
}

/** Pain (now) and each measured motion across the most recent signed visits. */
function Trends({ notes }: { notes: ChartNote[] }) {
  const visits = [...notes].slice(0, 6).reverse(); // oldest -> newest, last 6
  if (visits.length === 0)
    return (
      <p className="text-text-muted">
        Trends appear once visit notes are signed.
      </p>
    );
  const rows = new Map<string, Map<string, string>>();
  const pain = new Map<string, string>();
  for (const n of visits) {
    const s = parseSubjective(n.subjectiveDetailsJson);
    if (s.painNow !== null) pain.set(n.id, `${s.painNow}/10`);
    for (const r of parseObjective(n.objectiveMeasurementsJson).rom) {
      if (!r.arom && !r.prom) continue;
      const label = `${r.joint} ${r.motion}${r.side ? ` (${r.side})` : ""}`;
      if (!rows.has(label)) rows.set(label, new Map());
      rows.get(label)!.set(n.id, r.arom ? `${r.arom}°` : `P ${r.prom}°`);
    }
  }
  if (pain.size === 0 && rows.size === 0)
    return (
      <p className="text-text-muted">
        No pain ratings or ROM measurements charted yet.
      </p>
    );
  return (
    <div className="list-wrap">
      <table className="data-table">
        <thead>
          <tr>
            <th>Measure</th>
            {visits.map((v) => (
              <th key={v.id}>{formatDate(v.serviceDate)}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {pain.size > 0 && (
            <tr>
              <td data-label="Measure">Pain now</td>
              {visits.map((v) => (
                <td key={v.id} data-label={formatDate(v.serviceDate)}>
                  {pain.get(v.id) ?? "—"}
                </td>
              ))}
            </tr>
          )}
          {[...rows.entries()].map(([label, byVisit]) => (
            <tr key={label}>
              <td data-label="Measure">{label} AROM</td>
              {visits.map((v) => (
                <td key={v.id} data-label={formatDate(v.serviceDate)}>
                  {byVisit.get(v.id) ?? "—"}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** One visit note: summary line, expandable to its full charting. */
function NoteItem({ note }: { note: ChartNote }) {
  const [open, setOpen] = useState(false);
  const subj = parseSubjective(note.subjectiveDetailsJson);
  const obj = parseObjective(note.objectiveMeasurementsJson);
  const measured = obj.rom.length + obj.mmt.length + obj.specialTests.length;
  return (
    <li className="rounded-md border border-border">
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2 p-3">
        <button
          type="button"
          className="table-link min-h-11 text-left font-bold"
          aria-expanded={open}
          onClick={() => setOpen((o) => !o)}
        >
          {open ? "▾" : "▸"} {formatDate(note.serviceDate)} —{" "}
          {NoteTypeLabels[note.noteType] ?? "Note"}
        </button>
        <span
          className={
            note.status === NoteStatus.Draft ? "text-warning" : "text-[#333]"
          }
        >
          {statusLabel(note.status)}
        </span>
        {note.signatureName && (
          <span className="text-text-muted">by {note.signatureName}</span>
        )}
        <span className="text-text-muted">
          {subj.painNow !== null ? `Pain ${subj.painNow}/10 · ` : ""}
          {measured} measurement{measured === 1 ? "" : "s"}
        </span>
        <Link to={`/chart/${note.id}`} className="btn-refresh ml-auto">
          {note.status === NoteStatus.Draft ? "Continue charting" : "Open note"}
        </Link>
      </div>
      {open && (
        <div className="space-y-4 border-t border-border p-3">
          <Field label="Subjective" value={note.subjective} />
          {(subj.painNow !== null || subj.painLocation) && (
            <p className="text-[#333]">
              Pain now {subj.painNow ?? "—"}/10 · best {subj.painBest ?? "—"} ·
              worst {subj.painWorst ?? "—"}
              {subj.painLocation ? ` · ${subj.painLocation}` : ""}
            </p>
          )}
          {measured > 0 && (
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
          <Field label="Assessment" value={note.assessment} />
          <Field label="Plan" value={note.plan} />
        </div>
      )}
    </li>
  );
}

function Field({ label, value }: { label: string; value: string | null }) {
  if (!value) return null;
  return (
    <div>
      <p className="font-bold text-[#333]">{label}</p>
      <p className="whitespace-pre-wrap text-[#333]">{value}</p>
    </div>
  );
}
