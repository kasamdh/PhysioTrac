import { useEffect, useMemo, useState, type ReactNode } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import { useToast } from "../../components/Toast";
import { fetchPatientDetail } from "../admin/api";
import { useAuth } from "../auth/AuthProvider";
import { canSignClinicalNotes } from "../auth/permissions";
import {
  NoteStatus,
  NoteStatusLabels,
  NoteTypeLabels,
  needsPlanOfCare,
} from "../workflow/types";
import {
  fetchChartNote,
  fetchCompliance,
  fetchNoteRecord,
  fetchPullForward,
  saveChartNote,
  signChartNote,
  type ChartNoteUpdate,
} from "./api";
import { GoalsPanel } from "./components/GoalsPanel";
import { InterventionsPanel } from "./components/InterventionsPanel";
import { MeasurementTables } from "./components/MeasurementTables";
import { NoteRecordPanel } from "./components/NoteRecordPanel";
import { OutcomesPanel } from "./components/OutcomesPanel";
import { PainScale } from "./components/PainScale";
import { parseObjective, parseSubjective } from "./presets";
import type { ChartNote, ObjectiveDetails, SubjectiveDetails } from "./types";

const SECTIONS = [
  ["subjective", "Subjective"],
  ["exam", "Examination"],
  ["outcomes", "Outcome measures"],
  ["interventions", "Interventions"],
  ["progress", "Goals & progress"],
  ["assessment", "Assessment & plan"],
  ["sign", "Sign"],
] as const;

const formatDate = (iso: string) => {
  const [y, m, d] = iso.split("-");
  return `${m}/${d}/${y}`;
};

/** Clinical Charting: the full editor for one visit's ClinicalNote (the same
 * record Workflow and the patient's documentation use -- not a separate note
 * system). Drafts save automatically; a signed note opens read-only. */
export function ChartingPage() {
  const { noteId = "" } = useParams();
  const note = useQuery({
    queryKey: ["chart", "note", noteId],
    queryFn: () => fetchChartNote(noteId),
  });

  if (note.isLoading) return <p className="text-text-muted">Loading…</p>;
  if (note.isError) return <p className="alert-error">{note.error.message}</p>;
  if (!note.data) return null;
  return <ChartEditor key={note.data.id} note={note.data} />;
}

function ChartEditor({ note }: { note: ChartNote }) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const { user } = useAuth();
  const readOnly = note.status !== NoteStatus.Draft;
  const poc = needsPlanOfCare(note.noteType);

  const patient = useQuery({
    queryKey: ["admin", "patient", note.patientId],
    queryFn: () => fetchPatientDetail(note.patientId),
  });
  const pullForward = useQuery({
    queryKey: ["chart", "pull-forward", note.patientId],
    queryFn: () => fetchPullForward(note.patientId),
  });
  const compliance = useQuery({
    queryKey: ["chart", "compliance", note.id],
    queryFn: () => fetchCompliance(note.id),
  });
  const record = useQuery({
    queryKey: ["chart", "record", note.id],
    queryFn: () => fetchNoteRecord(note.id),
  });

  const [narrative, setNarrative] = useState({
    subjective: note.subjective ?? "",
    objective: note.objective ?? "",
    interventions: note.interventions ?? "",
    assessment: note.assessment ?? "",
    plan: note.plan ?? "",
  });
  const [subj, setSubj] = useState<SubjectiveDetails>(() =>
    parseSubjective(note.subjectiveDetailsJson),
  );
  const [obj, setObj] = useState<ObjectiveDetails>(() =>
    parseObjective(note.objectiveMeasurementsJson),
  );
  const [pocFields, setPocFields] = useState({
    planOfCareStart: note.planOfCareStart ?? (poc ? note.serviceDate : ""),
    planOfCareEnd: note.planOfCareEnd ?? "",
    frequencyPerWeek: note.frequencyPerWeek?.toString() ?? "",
    durationWeeks: note.durationWeeks?.toString() ?? "",
  });

  // Last visit's measurements (the patient's most recent SIGNED note), for
  // the comparison column while charting a draft. Not shown on a signed
  // note, where "most recent signed" may be this visit or a later one.
  const previousObjective = useMemo(() => {
    const json = pullForward.data?.lastObjectiveMeasurementsJson;
    if (readOnly || !json) return null;
    return parseObjective(json);
  }, [pullForward.data, readOnly]);

  const payload: ChartNoteUpdate = {
    ...narrative,
    planOfCareStart: pocFields.planOfCareStart || null,
    planOfCareEnd: pocFields.planOfCareEnd || null,
    frequencyPerWeek: pocFields.frequencyPerWeek
      ? Number(pocFields.frequencyPerWeek)
      : null,
    durationWeeks: pocFields.durationWeeks
      ? Number(pocFields.durationWeeks)
      : null,
    reassessmentDue: null,
    subjectiveDetailsJson: JSON.stringify(subj),
    objectiveMeasurementsJson: JSON.stringify(obj),
  };
  const serialized = JSON.stringify(payload);

  // ---- autosave -------------------------------------------------------------
  const [lastSaved, setLastSaved] = useState(serialized);
  const [savedAt, setSavedAt] = useState<Date | null>(null);
  const save = useMutation({
    mutationFn: (body: ChartNoteUpdate) => saveChartNote(note.id, body),
    onSuccess: (_d, body) => {
      setLastSaved(JSON.stringify(body));
      setSavedAt(new Date());
      void queryClient.invalidateQueries({
        queryKey: ["chart", "compliance", note.id],
      });
    },
  });
  const dirty = serialized !== lastSaved;
  useEffect(() => {
    if (readOnly || !dirty) return;
    const t = window.setTimeout(
      () => save.mutate(JSON.parse(serialized) as ChartNoteUpdate),
      1200,
    );
    return () => window.clearTimeout(t);
  }, [serialized, readOnly, dirty]); // eslint-disable-line react-hooks/exhaustive-deps

  // Warn before leaving with unsaved typing.
  useEffect(() => {
    if (!dirty || readOnly) return;
    const warn = (e: BeforeUnloadEvent) => e.preventDefault();
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [dirty, readOnly]);

  // ---- signing ------------------------------------------------------------
  const maySign =
    canSignClinicalNotes(user) && (record.data?.actions.canSign ?? true);
  const [attested, setAttested] = useState(false);
  const [password, setPassword] = useState("");
  const blockers = (compliance.data ?? []).filter((f) => f.finalizationBlocker);
  const sign = useMutation({
    mutationFn: async () => {
      if (dirty) await saveChartNote(note.id, payload); // never sign stale content
      return signChartNote(note.id, password);
    },
    onSuccess: (signed) => {
      showToast(
        signed.status === NoteStatus.ReviewRequired
          ? "Note signed — waiting for a PT’s cosign."
          : "Note signed. The visit is completed.",
      );
      void queryClient.invalidateQueries({ queryKey: ["chart"] });
      void queryClient.invalidateQueries({ queryKey: ["workflow"] });
    },
    onError: () => setPassword(""),
  });
  const canSign =
    maySign &&
    !readOnly &&
    blockers.length === 0 &&
    attested &&
    password.length > 0 &&
    !sign.isPending;

  const setN =
    (k: keyof typeof narrative) => (e: { target: { value: string } }) =>
      setNarrative((n) => ({ ...n, [k]: e.target.value }));
  const area = (
    k: keyof typeof narrative,
    label: string,
    placeholder: string,
  ) => (
    <label className="block">
      <span className="font-bold text-[#333]">{label}</span>
      <textarea
        className="field-input mt-1 min-h-[6rem] resize-y"
        readOnly={readOnly}
        placeholder={readOnly ? "" : placeholder}
        value={narrative[k]}
        onChange={setN(k)}
      />
    </label>
  );

  const p = patient.data;
  return (
    <div className="pb-10">
      {/* Encounter header */}
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <div>
          <Link to="/workflow" className="text-primary hover:underline">
            ‹ Workflow
          </Link>
          {p && (
            <>
              <span className="mx-2 text-text-muted">|</span>
              <Link
                to={`/patients/${note.patientId}/documentation`}
                className="text-primary hover:underline"
              >
                {p.fullName}’s documentation
              </Link>
            </>
          )}
          <h1 className="mt-1 text-2xl font-bold text-[#1565b8]">
            {NoteTypeLabels[note.noteType] ?? "Visit note"}
            {note.amendsNoteId ? " (amendment)" : ""} — {p?.fullName ?? "…"}
          </h1>
          <p className="text-text-muted">
            {p &&
              `${p.medicalRecordNumber} · DOB ${formatDate(p.dateOfBirth)} (${p.age}) · `}
            Visit {formatDate(note.serviceDate)}
          </p>
        </div>
        <SaveState
          readOnly={readOnly}
          note={note}
          saving={save.isPending}
          error={save.error?.message}
          dirty={dirty}
          savedAt={savedAt}
        />
      </div>

      {note.amendsNoteId && (
        <p className="mb-4 rounded-md border border-warning bg-warning-light px-3 py-2 text-[#333]">
          <strong>Amendment</strong> of the signed note for this visit
          {note.amendmentReason ? ` — ${note.amendmentReason}` : ""}.{" "}
          <Link
            to={`/chart/${note.amendsNoteId}`}
            className="font-bold text-primary hover:underline"
          >
            View the original note
          </Link>
        </p>
      )}
      {note.status === NoteStatus.Amended && record.data?.amendmentNoteId && (
        <p className="mb-4 rounded-md border border-warning bg-warning-light px-3 py-2 text-[#333]">
          This note has been <strong>amended</strong>; it is kept unchanged for
          the record.{" "}
          <Link
            to={`/chart/${record.data.amendmentNoteId}`}
            className="font-bold text-primary hover:underline"
          >
            Open the amendment
          </Link>
        </p>
      )}

      {(!!p?.precautions || !!pullForward.data?.activeDiagnoses.length) && (
        <div className="mb-4 grid gap-3 sm:grid-cols-2">
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

      {/* Section jump bar */}
      <nav
        aria-label="Note sections"
        className="sticky top-[72px] z-10 -mx-4 mb-4 overflow-x-auto bg-surface px-4 py-2 shadow-sm md:-mx-6 md:px-6"
      >
        <div className="seg-group flex-nowrap">
          {SECTIONS.map(([id, label]) => (
            <button
              key={id}
              type="button"
              className="seg-btn"
              onClick={() =>
                document
                  .getElementById(`chart-${id}`)
                  ?.scrollIntoView({ behavior: "smooth", block: "start" })
              }
            >
              {label}
            </button>
          ))}
        </div>
      </nav>

      <div className="space-y-5">
        <Card id="subjective" title="Subjective">
          <div className="grid gap-4 lg:grid-cols-3">
            <PainScale
              label="Pain now"
              value={subj.painNow}
              readOnly={readOnly}
              onChange={(v) => setSubj({ ...subj, painNow: v })}
            />
            <PainScale
              label="Pain at best"
              value={subj.painBest}
              readOnly={readOnly}
              onChange={(v) => setSubj({ ...subj, painBest: v })}
            />
            <PainScale
              label="Pain at worst"
              value={subj.painWorst}
              readOnly={readOnly}
              onChange={(v) => setSubj({ ...subj, painWorst: v })}
            />
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <label className="block">
              <span className="font-bold text-[#333]">Pain location</span>
              <input
                className="field-input mt-1"
                readOnly={readOnly}
                value={subj.painLocation}
                onChange={(e) =>
                  setSubj({ ...subj, painLocation: e.target.value })
                }
              />
            </label>
            <div>
              <span className="font-bold text-[#333]">
                Home exercise program
              </span>
              <div
                role="group"
                aria-label="Home exercise program compliance"
                className="seg-group mt-1"
              >
                {(["yes", "partial", "no"] as const).map((v) => (
                  <button
                    key={v}
                    type="button"
                    disabled={readOnly}
                    className="seg-btn"
                    aria-pressed={subj.hepCompliance === v}
                    onClick={() => setSubj({ ...subj, hepCompliance: v })}
                  >
                    {v === "yes"
                      ? "Doing it"
                      : v === "partial"
                        ? "Partly"
                        : "Not doing it"}
                  </button>
                ))}
              </div>
            </div>
          </div>
          {area(
            "subjective",
            "Patient report",
            "Symptoms since last visit, function, response to treatment, goals",
          )}
        </Card>

        <Card id="exam" title="Examination">
          <MeasurementTables
            value={obj}
            previous={previousObjective}
            readOnly={readOnly}
            onChange={setObj}
          />
          {area(
            "objective",
            "Objective findings (required to sign)",
            "Observation, palpation, gait, posture, other measurable findings",
          )}
        </Card>

        <Card id="outcomes" title="Outcome measures">
          <OutcomesPanel
            patientId={note.patientId}
            noteId={note.id}
            serviceDate={note.serviceDate}
            readOnly={readOnly}
          />
        </Card>

        <Card id="interventions" title="Interventions & patient response">
          <InterventionsPanel noteId={note.id} readOnly={readOnly} />
          {area(
            "interventions",
            "Treatment summary (optional)",
            "Anything not captured in the rows above",
          )}
        </Card>

        <Card id="progress" title="Goals & progress">
          <GoalsPanel patientId={note.patientId} readOnly={readOnly} />
        </Card>

        <Card id="assessment" title="Assessment & plan">
          {area(
            "assessment",
            "Assessment",
            "Clinical reasoning, progress toward goals, response to treatment",
          )}
          {area(
            "plan",
            "Plan (required to sign)",
            "Next visit, progression, home program, follow-up",
          )}
          {poc && (
            <div>
              <span className="font-bold text-[#333]">
                Plan of care (required to sign)
              </span>
              <div className="mt-1 grid grid-cols-2 gap-3 lg:grid-cols-4">
                {(
                  [
                    ["planOfCareStart", "Start", "date"],
                    ["planOfCareEnd", "End", "date"],
                    ["frequencyPerWeek", "Times / week", "number"],
                    ["durationWeeks", "Weeks", "number"],
                  ] as const
                ).map(([k, label, type]) => (
                  <label key={k} className="text-text-muted">
                    {label}
                    <input
                      type={type}
                      min={type === "number" ? 1 : undefined}
                      className="field-input mt-1"
                      readOnly={readOnly}
                      value={pocFields[k]}
                      onChange={(e) =>
                        setPocFields({ ...pocFields, [k]: e.target.value })
                      }
                    />
                  </label>
                ))}
              </div>
            </div>
          )}
        </Card>

        <Card id="sign" title="Sign">
          {readOnly ? (
            <NoteRecordPanel note={note} record={record.data} />
          ) : !maySign ? (
            <p className="text-text-muted">
              Your work saves automatically as a draft; a therapist or assistant
              signs the note.
            </p>
          ) : (
            <div className="space-y-3">
              {blockers.length > 0 ? (
                <div className="rounded-md border border-warning bg-warning-light px-3 py-2">
                  <p className="font-bold text-[#333]">Before signing:</p>
                  <ul className="ml-5 list-disc text-[#333]">
                    {blockers.map((b) => (
                      <li key={b.code}>
                        {b.title} —{" "}
                        <span className="text-text-muted">{b.detail}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              ) : (
                <p className="text-success">
                  All required documentation is complete.
                </p>
              )}
              {sign.isError && (
                <p className="alert-error">{sign.error.message}</p>
              )}
              <label className="flex items-start gap-3">
                <input
                  type="checkbox"
                  className="mt-1 h-5 w-5 shrink-0"
                  checked={attested}
                  onChange={(e) => setAttested(e.target.checked)}
                />
                <span>
                  I attest that I provided or supervised this treatment and that
                  this note is accurate and complete.
                </span>
              </label>
              <label className="block max-w-md" htmlFor="chart-sign-password">
                <span className="font-bold text-[#333]">Your password</span>
                <span className="ml-2 text-text-muted">
                  — re-enter it to sign
                </span>
                <input
                  id="chart-sign-password"
                  type="password"
                  autoComplete="current-password"
                  className="field-input mt-1"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                />
              </label>
              <button
                type="button"
                className="btn-primary"
                disabled={!canSign}
                onClick={() => sign.mutate()}
              >
                {sign.isPending ? "Signing…" : "Sign note"}
              </button>
              <p className="text-text-muted">
                {note.amendsNoteId
                  ? "Signing this amendment replaces the original note in the record; the original stays viewable, marked Amended."
                  : "Signing locks the note and completes the visit. Corrections after signing are made with an addendum or an amendment."}
              </p>
            </div>
          )}
        </Card>
      </div>
    </div>
  );
}

function Card({
  id,
  title,
  children,
}: {
  id: string;
  title: string;
  children: ReactNode;
}) {
  return (
    <section
      id={`chart-${id}`}
      aria-labelledby={`chart-${id}-title`}
      className="scroll-mt-36 space-y-4 rounded-lg border border-border bg-white p-4 sm:p-5"
    >
      <h2
        id={`chart-${id}-title`}
        className="text-2xl font-bold text-[#1565b8]"
      >
        {title}
      </h2>
      {children}
    </section>
  );
}

function SaveState({
  readOnly,
  note,
  saving,
  error,
  dirty,
  savedAt,
}: {
  readOnly: boolean;
  note: ChartNote;
  saving: boolean;
  error?: string;
  dirty: boolean;
  savedAt: Date | null;
}) {
  if (readOnly)
    return (
      <span className="rounded-full bg-success-light px-3 py-1 text-success">
        {NoteStatusLabels[note.status] ?? "Signed"}
      </span>
    );
  if (error)
    return (
      <span role="alert" className="text-danger">
        Not saved: {error}
      </span>
    );
  if (saving) return <span className="text-text-muted">Saving…</span>;
  if (dirty) return <span className="text-text-muted">Unsaved changes…</span>;
  return (
    <span className="text-text-muted">
      Draft
      {savedAt
        ? ` · saved ${savedAt.toLocaleTimeString("en-US", { hour: "numeric", minute: "2-digit" })}`
        : ""}
    </span>
  );
}
