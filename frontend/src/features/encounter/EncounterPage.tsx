import { useEffect, useMemo, useState, type ReactNode } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import { useToast } from "../../components/Toast";
import { ApiError } from "../../lib/apiClient";
import { useAuth } from "../auth/AuthProvider";
import { canSignClinicalNotes } from "../auth/permissions";
import { ChartingPage } from "../charting/ChartingPage";
import {
  fetchCompliance,
  fetchNoteRecord,
  fetchPullForward,
  signChartNote,
} from "../charting/api";
import { GoalsPanel } from "../charting/components/GoalsPanel";
import { InterventionsPanel } from "../charting/components/InterventionsPanel";
import { MeasurementTables } from "../charting/components/MeasurementTables";
import { NoteRecordPanel } from "../charting/components/NoteRecordPanel";
import { OutcomesPanel } from "../charting/components/OutcomesPanel";
import { PainScale } from "../charting/components/PainScale";
import { parseObjective, parseSubjective } from "../charting/presets";
import type { ObjectiveDetails, SubjectiveDetails } from "../charting/types";
import { fetchTemplates } from "../templates/api";
import { missingRequired } from "../templates/rules";
import { TemplateSectionFields } from "../templates/TemplateForm";
import type {
  FieldValue,
  FieldValues,
  TemplateSection,
} from "../templates/types";
import {
  NoteStatus,
  NoteStatusLabels,
  NoteTypeLabels,
} from "../workflow/types";
import { changeNoteTemplate, fetchEncounter, saveEncounter } from "./api";
import {
  AddGoalForm,
  DiagnosesPanel,
  MedicalHistoryPanel,
  PlanOfCarePanel,
} from "./components/ClinicalPanels";
import type { EditConflict, Encounter, SaveEncounterBody } from "./types";

/** Clinical components the workspace can show; sections whose component
 * isn't available yet and that have no fields of their own are left out. */
const AVAILABLE = new Set([
  "painAssessment",
  "measurements",
  "outcomes",
  "interventions",
  "goals",
  "diagnoses",
  "medicalHistory",
  "planOfCare",
]);

const COLUMNS = [
  "subjective",
  "objective",
  "interventions",
  "assessment",
  "plan",
] as const;
type Columns = Record<(typeof COLUMNS)[number], string>;

/** Notes that create a plan of care when signed. */
const CREATES_PLAN = new Set([0, 5, 10, 11]);

const formatDate = (iso: string) => {
  const [y, m, d] = iso.slice(0, 10).split("-");
  return `${m}/${d}/${y}`;
};
const time = (iso: string) =>
  new Date(iso).toLocaleTimeString("en-US", {
    hour: "numeric",
    minute: "2-digit",
  });

/** /chart/:noteId -- the clinical encounter. Notes written with a
 * documentation template open in the template-driven workspace; older notes
 * (no template) keep the original charting screen. */
export function EncounterPage() {
  const { noteId = "" } = useParams();
  const encounter = useQuery({
    queryKey: ["encounter", noteId],
    queryFn: () => fetchEncounter(noteId),
  });
  // Bumped to start the editor afresh from the server (after an edit
  // conflict). Ordinary refreshes keep the editor's unsaved work.
  const [generation, setGeneration] = useState(0);
  if (encounter.isLoading) return <p className="text-text-muted">Loading…</p>;
  if (encounter.isError)
    return <p className="alert-error">{encounter.error.message}</p>;
  if (!encounter.data) return null;
  if (!encounter.data.template) return <ChartingPage />;
  const e = encounter.data;
  return (
    <Workspace
      key={`${e.note.id}:${e.note.templateVersionId}:${e.note.status}:${generation}`}
      encounter={e}
      onReload={() =>
        void encounter.refetch().then(() => setGeneration((g) => g + 1))
      }
    />
  );
}

function valuesMap(list: FieldValue[]): FieldValues {
  return Object.fromEntries(list.map((v) => [v.key, v]));
}

function sameValue(a: FieldValue | undefined, b: FieldValue | undefined) {
  const n = (v: FieldValue | undefined) =>
    JSON.stringify([
      v?.text ?? null,
      v?.number ?? null,
      v?.date ?? null,
      v?.time ?? null,
      v?.bool ?? null,
      v?.json ?? null,
    ]);
  return n(a) === n(b);
}

function Workspace({
  encounter,
  onReload,
}: {
  encounter: Encounter;
  onReload: () => void;
}) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const { user } = useAuth();
  const note = encounter.note;
  const sections = encounter.template!.sections;
  const allFields = useMemo(
    () => sections.flatMap((s) => s.fields),
    [sections],
  );

  const record = useQuery({
    queryKey: ["chart", "record", note.id],
    queryFn: () => fetchNoteRecord(note.id),
  });
  const compliance = useQuery({
    queryKey: ["chart", "compliance", note.id],
    queryFn: () => fetchCompliance(note.id),
  });
  const pullForward = useQuery({
    queryKey: ["chart", "pull-forward", note.patientId],
    queryFn: () => fetchPullForward(note.patientId),
  });

  const editable =
    note.status === NoteStatus.Draft ||
    note.status === NoteStatus.ReturnedForCorrection;
  const readOnly = !editable || record.data?.actions.canEdit === false;

  // ---- working copy and what the server last confirmed ----------------------
  const initialColumns: Columns = {
    subjective: note.subjective ?? "",
    objective: note.objective ?? "",
    interventions: note.interventions ?? "",
    assessment: note.assessment ?? "",
    plan: note.plan ?? "",
  };
  const [values, setValues] = useState<FieldValues>(() =>
    valuesMap(encounter.values),
  );
  const [columns, setColumns] = useState<Columns>(initialColumns);
  const [subj, setSubj] = useState<SubjectiveDetails>(() =>
    parseSubjective(note.subjectiveDetailsJson),
  );
  const [obj, setObj] = useState<ObjectiveDetails>(() =>
    parseObjective(note.objectiveMeasurementsJson),
  );
  const [saved, setSaved] = useState(() => ({
    values: valuesMap(encounter.values),
    columns: initialColumns,
    subj: JSON.stringify(parseSubjective(note.subjectiveDetailsJson)),
    obj: JSON.stringify(parseObjective(note.objectiveMeasurementsJson)),
  }));
  const [saveVersion, setSaveVersion] = useState(encounter.saveVersion);
  const [savedAt, setSavedAt] = useState<{ at: string; by: string | null }>({
    at: encounter.lastSavedAt,
    by: encounter.lastSavedByName,
  });
  const [conflict, setConflict] = useState<EditConflict | null>(null);
  const [saveErrors, setSaveErrors] = useState<string[]>([]);

  const body = useMemo<SaveEncounterBody | null>(() => {
    const b: SaveEncounterBody = { baseSaveVersion: saveVersion };
    const changed = Object.keys({ ...values, ...saved.values })
      .filter((k) => !sameValue(values[k], saved.values[k]))
      .map((k) => values[k] ?? { key: k });
    if (changed.length) b.values = changed;
    for (const c of COLUMNS)
      if (columns[c] !== saved.columns[c]) b[c] = columns[c];
    if (JSON.stringify(subj) !== saved.subj)
      b.subjectiveDetailsJson = JSON.stringify(subj);
    if (JSON.stringify(obj) !== saved.obj)
      b.objectiveMeasurementsJson = JSON.stringify(obj);
    return Object.keys(b).length > 1 ? b : null;
  }, [values, columns, subj, obj, saved, saveVersion]);
  const dirty = body !== null;

  const save = useMutation({
    mutationFn: (b: SaveEncounterBody) => saveEncounter(note.id, b),
    onSuccess: (result, b) => {
      setSaveVersion(result.saveVersion);
      setSavedAt({ at: result.savedAt, by: result.savedByName });
      setSaveErrors([]);
      setSaved((s) => ({
        values: {
          ...s.values,
          ...Object.fromEntries((b.values ?? []).map((v) => [v.key, v])),
        },
        columns: {
          ...s.columns,
          ...Object.fromEntries(
            COLUMNS.filter((c) => b[c] !== undefined).map((c) => [c, b[c]!]),
          ),
        },
        subj: b.subjectiveDetailsJson ?? s.subj,
        obj: b.objectiveMeasurementsJson ?? s.obj,
      }));
      void queryClient.invalidateQueries({
        queryKey: ["chart", "compliance", note.id],
      });
    },
    onError: (e: Error) => {
      if (e instanceof ApiError && e.status === 409)
        setConflict(e.payload as EditConflict);
      else {
        const errors =
          e instanceof ApiError
            ? (e.payload as { errors?: string[] } | undefined)?.errors
            : undefined;
        setSaveErrors(errors?.length ? errors : [e.message]);
      }
    },
  });

  // Autosave: 1.2 s after the last change, one save at a time, never over a conflict.
  useEffect(() => {
    if (readOnly || !body || conflict || save.isPending || saveErrors.length)
      return;
    const t = window.setTimeout(() => save.mutate(body), 1200);
    return () => window.clearTimeout(t);
  }, [body, readOnly, conflict, save.isPending, saveErrors.length]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (!dirty || readOnly) return;
    const warn = (e: BeforeUnloadEvent) => e.preventDefault();
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [dirty, readOnly]);

  const setValue = (v: FieldValue) => {
    setSaveErrors([]);
    setValues((p) => ({ ...p, [v.key]: v }));
  };
  const setColumn = (c: string, t: string) => {
    setSaveErrors([]);
    setColumns((p) => ({ ...p, [c]: t }));
  };

  const reload = onReload;

  const previousObjective = useMemo(() => {
    const json = pullForward.data?.lastObjectiveMeasurementsJson;
    return readOnly || !json ? null : parseObjective(json);
  }, [pullForward.data, readOnly]);

  const renderComponent = (component: string): ReactNode => {
    switch (component) {
      case "painAssessment":
        return (
          <div className="space-y-3">
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
            <label className="block max-w-xl">
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
          </div>
        );
      case "measurements":
        return (
          <MeasurementTables
            value={obj}
            previous={previousObjective}
            readOnly={readOnly}
            onChange={setObj}
          />
        );
      case "outcomes":
        return (
          <OutcomesPanel
            patientId={note.patientId}
            noteId={note.id}
            serviceDate={note.serviceDate}
            readOnly={readOnly}
          />
        );
      case "interventions":
        return <InterventionsPanel noteId={note.id} readOnly={readOnly} />;
      case "goals":
        return (
          <div className="space-y-3">
            <GoalsPanel patientId={note.patientId} readOnly={readOnly} />
            {!readOnly && <AddGoalForm patientId={note.patientId} />}
          </div>
        );
      case "diagnoses":
        return (
          <DiagnosesPanel patientId={note.patientId} readOnly={readOnly} />
        );
      case "medicalHistory":
        return (
          <MedicalHistoryPanel patientId={note.patientId} readOnly={readOnly} />
        );
      case "planOfCare":
        return (
          <PlanOfCarePanel
            patientId={note.patientId}
            createsPlan={CREATES_PLAN.has(note.noteType)}
          />
        );
      default:
        return null;
    }
  };
  const shown = sections.filter(
    (s) => s.fields.length > 0 || (s.component && AVAILABLE.has(s.component)),
  );
  const missing = missingRequired(allFields, values, columns);

  return (
    <div className="pb-10">
      <EncounterHeaderView
        encounter={encounter}
        readOnly={readOnly}
        status={NoteStatusLabels[note.status] ?? ""}
        saveState={
          readOnly ? null : conflict ? (
            <span className="text-danger">Not saved — changed elsewhere</span>
          ) : save.isPending ? (
            <span className="text-text-muted">Saving…</span>
          ) : saveErrors.length ? (
            <span className="text-danger">
              Not saved — see the problems below
            </span>
          ) : dirty ? (
            <span className="text-text-muted">Unsaved changes…</span>
          ) : (
            <span className="text-text-muted">
              Draft · saved {time(savedAt.at)}
              {savedAt.by ? ` by ${savedAt.by}` : ""}
            </span>
          )
        }
      />

      {conflict && (
        <div
          role="alert"
          className="mb-4 rounded-md border border-danger bg-danger-light px-3 py-2 text-[#333]"
        >
          <p>
            <strong>
              This note was changed
              {conflict.savedByName ? ` by ${conflict.savedByName}` : ""}
            </strong>{" "}
            at {time(conflict.savedAt)}, after you opened it. Your latest
            changes were not saved, so the other edit isn’t overwritten.
          </p>
          <button type="button" className="btn-primary mt-2" onClick={reload}>
            Reload the latest version
          </button>
        </div>
      )}
      {saveErrors.length > 0 && (
        <div role="alert" className="alert-error mb-4">
          <ul className="ml-5 list-disc">
            {saveErrors.map((e) => (
              <li key={e}>{e}</li>
            ))}
          </ul>
        </div>
      )}

      <nav
        aria-label="Note sections"
        className="sticky top-[72px] z-10 -mx-4 mb-4 overflow-x-auto bg-surface px-4 py-2 shadow-sm md:-mx-6 md:px-6"
      >
        <div className="seg-group flex-nowrap">
          {[
            ...shown.map((s) => [s.key, s.title] as const),
            ["sign", "Sign"] as const,
          ].map(([key, title]) => (
            <button
              key={key}
              type="button"
              className="seg-btn"
              onClick={() =>
                document
                  .getElementById(`enc-${key}`)
                  ?.scrollIntoView({ behavior: "smooth", block: "start" })
              }
            >
              {title}
            </button>
          ))}
        </div>
      </nav>

      <div className="space-y-5">
        {shown.map((s: TemplateSection) => (
          <section
            key={s.key}
            id={`enc-${s.key}`}
            aria-labelledby={`enc-${s.key}-title`}
            className="scroll-mt-36 space-y-4 rounded-lg border border-border bg-white p-4 sm:p-5"
          >
            <h2
              id={`enc-${s.key}-title`}
              className="text-2xl font-bold text-[#1565b8]"
            >
              {s.title}
            </h2>
            <TemplateSectionFields
              section={{
                ...s,
                component:
                  s.component && AVAILABLE.has(s.component)
                    ? s.component
                    : null,
              }}
              values={values}
              columns={columns}
              readOnly={readOnly}
              onChange={setValue}
              onColumnChange={setColumn}
              renderComponent={(c) => renderComponent(c)}
            />
          </section>
        ))}

        <section
          id="enc-sign"
          aria-labelledby="enc-sign-title"
          className="scroll-mt-36 space-y-4 rounded-lg border border-border bg-white p-4 sm:p-5"
        >
          <h2 id="enc-sign-title" className="text-2xl font-bold text-[#1565b8]">
            Sign
          </h2>
          {!editable ? (
            <NoteRecordPanel note={note} record={record.data} />
          ) : (
            <SignCard
              noteId={note.id}
              missing={missing}
              blockers={(compliance.data ?? []).filter(
                (f) =>
                  f.finalizationBlocker && f.code !== "missing_required_fields",
              )}
              canSign={
                canSignClinicalNotes(user) &&
                (record.data?.actions.canSign ?? true) &&
                !readOnly
              }
              isAmendment={!!note.amendsNoteId}
              createsPlan={CREATES_PLAN.has(note.noteType)}
              flush={async () => {
                if (body && !conflict) await save.mutateAsync(body);
              }}
              onSigned={(status) => {
                showToast(
                  status === NoteStatus.ReviewRequired
                    ? "Note submitted — waiting for a PT’s cosign."
                    : "Note signed.",
                );
                void queryClient.invalidateQueries({
                  queryKey: ["encounter", note.id],
                });
                void queryClient.invalidateQueries({ queryKey: ["chart"] });
                void queryClient.invalidateQueries({ queryKey: ["workflow"] });
                void queryClient.invalidateQueries({
                  queryKey: ["patient", note.patientId],
                });
              }}
            />
          )}
        </section>
      </div>
    </div>
  );
}

function EncounterHeaderView({
  encounter,
  readOnly,
  status,
  saveState,
}: {
  encounter: Encounter;
  readOnly: boolean;
  status: string;
  saveState: ReactNode;
}) {
  const h = encounter.header;
  const note = encounter.note;
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const canSwitch = !readOnly && encounter.values.length === 0;
  const templates = useQuery({
    queryKey: ["templates", { noteType: note.noteType }],
    queryFn: () => fetchTemplates({ noteType: note.noteType }),
    enabled: canSwitch,
  });
  const switchTemplate = useMutation({
    mutationFn: (templateId: string) => changeNoteTemplate(note.id, templateId),
    onSuccess: () =>
      void queryClient.invalidateQueries({ queryKey: ["encounter", note.id] }),
    onError: (e: Error) => showToast(e.message),
  });
  const currentTemplateId = encounter.template?.templateId;

  const fact = (label: string, value: ReactNode) =>
    value ? (
      <div>
        <dt className="text-text-muted">{label}</dt>
        <dd className="font-bold text-[#333]">{value}</dd>
      </div>
    ) : null;

  return (
    <div className="mb-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <Link to="/workflow" className="text-primary hover:underline">
            ‹ Workflow
          </Link>
          <span className="mx-2 text-text-muted">|</span>
          <Link
            to={`/patients/${h.patientId}/documentation`}
            className="text-primary hover:underline"
          >
            {h.patientName}’s documentation
          </Link>
          <h1 className="mt-1 text-2xl font-bold text-[#1565b8]">
            {NoteTypeLabels[note.noteType] ?? "Visit note"}
            {note.amendsNoteId ? " (amendment)" : ""} — {h.patientName}
          </h1>
        </div>
        <div className="flex flex-col items-end gap-1">
          <span className="rounded-full bg-surface-muted px-3 py-1 text-[#333]">
            {status}
          </span>
          {saveState}
        </div>
      </div>

      <dl className="mt-3 grid grid-cols-2 gap-x-6 gap-y-2 rounded-lg border border-border bg-white p-3 md:grid-cols-4 xl:grid-cols-6">
        {fact("DOB", `${formatDate(h.dateOfBirth)} (${h.age})`)}
        {fact("MRN", h.medicalRecordNumber)}
        {fact(
          "Appointment",
          h.appointmentStartsAt
            ? `${new Date(h.appointmentStartsAt).toLocaleDateString("en-US")} ${time(h.appointmentStartsAt)}`
            : `Service ${formatDate(note.serviceDate)}`,
        )}
        {fact("Visit", `#${h.visitNumber} · ${h.visitType}`)}
        {fact("Therapist", h.treatingProviderName ?? h.authorName)}
        {fact("Referred by", h.referringProviderName)}
        {fact(
          "Plan of care",
          h.planOfCareStart && h.planOfCareEnd
            ? `${formatDate(h.planOfCareStart)} – ${formatDate(h.planOfCareEnd)}`
            : "None active",
        )}
        {fact(
          "Template",
          `${encounter.templateName ?? ""} v${encounter.template?.versionNumber ?? ""}`,
        )}
      </dl>

      <div className="mt-2 grid gap-2 md:grid-cols-3">
        <p className="rounded-md border border-border bg-surface-muted px-3 py-2 text-[#333]">
          <strong>Diagnoses:</strong>{" "}
          {h.diagnoses.length ? h.diagnoses.join("; ") : "none on file"}
        </p>
        <p
          className={`rounded-md border px-3 py-2 ${h.allergies.length ? "border-danger bg-danger-light text-danger" : "border-border bg-surface-muted text-[#333]"}`}
        >
          <strong>Allergies:</strong>{" "}
          {h.allergies.length ? h.allergies.join("; ") : "none recorded"}
        </p>
        <p
          className={`rounded-md border px-3 py-2 ${h.precautions ? "border-danger bg-danger-light text-danger" : "border-border bg-surface-muted text-[#333]"}`}
        >
          <strong>Precautions:</strong> {h.precautions ?? "none recorded"}
        </p>
      </div>

      {canSwitch && (templates.data?.length ?? 0) > 1 && (
        <label className="mt-3 flex max-w-xl flex-wrap items-center gap-2">
          <span className="font-bold text-[#333]">Template</span>
          <select
            className="field-input max-w-sm"
            value={currentTemplateId ?? ""}
            disabled={switchTemplate.isPending}
            onChange={(e) => switchTemplate.mutate(e.target.value)}
          >
            {templates.data!.map((t) => (
              <option key={t.id} value={t.id}>
                {t.isFavorite ? "★ " : ""}
                {t.name}
              </option>
            ))}
          </select>
          <span className="text-text-muted">
            — can be changed until fields are filled in
          </span>
        </label>
      )}
    </div>
  );
}

function SignCard({
  noteId,
  missing,
  blockers,
  canSign,
  isAmendment,
  createsPlan,
  flush,
  onSigned,
}: {
  noteId: string;
  missing: string[];
  blockers: { code: string; title: string; detail: string }[];
  canSign: boolean;
  isAmendment: boolean;
  createsPlan: boolean;
  flush: () => Promise<void>;
  onSigned: (status: number) => void;
}) {
  const [attested, setAttested] = useState(false);
  const [password, setPassword] = useState("");
  const sign = useMutation({
    mutationFn: async () => {
      await flush(); // never sign stale content
      return signChartNote(noteId, password);
    },
    onSuccess: (n) => onSigned(n.status),
    onError: () => setPassword(""),
  });
  if (!canSign)
    return (
      <p className="text-text-muted">
        Your work saves automatically as a draft; a therapist or assistant signs
        the note.
      </p>
    );
  const ready = missing.length === 0 && blockers.length === 0;
  return (
    <div className="space-y-3">
      {ready ? (
        <p className="text-success">All required documentation is complete.</p>
      ) : (
        <div className="rounded-md border border-warning bg-warning-light px-3 py-2">
          <p className="font-bold text-[#333]">Before signing:</p>
          <ul className="ml-5 list-disc text-[#333]">
            {missing.length > 0 && (
              <li>Complete the required fields: {missing.join(", ")}</li>
            )}
            {blockers.map((b) => (
              <li key={b.code}>
                {b.title} — <span className="text-text-muted">{b.detail}</span>
              </li>
            ))}
          </ul>
        </div>
      )}
      {sign.isError && <p className="alert-error">{sign.error.message}</p>}
      <label className="flex items-start gap-3">
        <input
          type="checkbox"
          className="mt-1 h-5 w-5 shrink-0"
          checked={attested}
          onChange={(e) => setAttested(e.target.checked)}
        />
        <span>
          I attest that I provided or supervised this treatment and that this
          note is accurate and complete.
        </span>
      </label>
      <label className="block max-w-md" htmlFor="enc-sign-password">
        <span className="font-bold text-[#333]">Your password</span>
        <span className="ml-2 text-text-muted">— re-enter it to sign</span>
        <input
          id="enc-sign-password"
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
        disabled={!ready || !attested || !password || sign.isPending}
        onClick={() => sign.mutate()}
      >
        {sign.isPending ? "Signing…" : "Sign note"}
      </button>
      <p className="text-text-muted">
        {isAmendment
          ? "Signing this amendment replaces the original note in the record; the original stays viewable, marked Amended."
          : createsPlan
            ? "Signing locks the note and creates the patient’s plan of care from it."
            : "Signing locks the note and completes the visit. Corrections after signing are made with an addendum or an amendment."}
      </p>
    </div>
  );
}
