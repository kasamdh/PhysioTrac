import { useEffect, useMemo, useState, type ReactNode } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import { useToast } from "../../components/Toast";
import { ApiError } from "../../lib/apiClient";
import { useAuth } from "../auth/AuthProvider";
import { RoleSets, canAccess, canSignClinicalNotes } from "../auth/permissions";
import { ChartingPage } from "../charting/ChartingPage";
import {
  fetchCompliance,
  fetchNoteRecord,
  signChartNote,
} from "../charting/api";
import { NoteRecordPanel } from "../charting/components/NoteRecordPanel";
import { VoidNoteForm } from "../charting/components/LifecycleActions";
import { parseObjective, parseSubjective } from "../charting/presets";
import type { ObjectiveDetails, SubjectiveDetails } from "../charting/types";
import { fetchTemplates } from "../templates/api";
import { missingRequired, valueAsText } from "../templates/rules";
import { TemplateSectionFields } from "../templates/TemplateForm";
import { FieldType } from "../templates/types";
import type {
  FieldValue,
  FieldValues,
  TemplateSection,
} from "../templates/types";
import {
  DocumentationStatus,
  DocumentationStatusLabels,
  NoteStatus,
  NoteStatusLabels,
  NoteTypeLabels,
} from "../workflow/types";
import {
  changeNoteTemplate,
  fetchEncounter,
  fetchEncounterStatus,
  saveEncounter,
} from "./api";
import {
  DiagnosesPanel,
  MedicalHistoryPanel,
  PlanOfCarePanel,
} from "./components/ClinicalPanels";
import type {
  BodyFinding,
  EditConflict,
  Encounter,
  PainAssessment,
  SaveEncounterBody,
} from "./types";
import { BodyChart } from "./bodychart/BodyChart";
import { PainAssessmentPanel } from "./PainAssessmentPanel";
import { emptyPain } from "./pain";
import { MeasurementsPanel } from "./measurements/MeasurementsPanel";
import { SpecialTestsPanel } from "./measurements/SpecialTestsPanel";
import { fetchSpecialTests } from "./measurements/api";
import { FlowsheetPanel } from "./flowsheet/FlowsheetPanel";
import { EpisodePanel } from "./episode/EpisodePanel";
import { SUMMARY_NOTE_TYPES } from "./episode/api";
import { GoalTracker } from "./goals/GoalTracker";
import type { GoalProgressRow } from "./goals/model";
import { OutcomesWorkspace } from "./outcomes/OutcomesWorkspace";
import { AiDraftAssist } from "./ai/AiDraftAssist";
import type { FlowsheetEntry } from "./flowsheet/model";
import type { Measurement } from "./measurements/model";
import type { SpecialTest } from "./measurements/types";

/** Clinical components the workspace can show; sections whose component
 * isn't available yet and that have no fields of their own are left out. */
const AVAILABLE = new Set([
  "painAssessment",
  "bodyChart",
  "specialTests",
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
  // Older pain fields (kept unchanged; the pain assessment replaces them).
  const [subj] = useState<SubjectiveDetails>(() =>
    parseSubjective(note.subjectiveDetailsJson),
  );
  const [obj] = useState<ObjectiveDetails>(() =>
    parseObjective(note.objectiveMeasurementsJson),
  );
  const [pain, setPain] = useState<PainAssessment>(
    () => encounter.pain ?? emptyPain(),
  );
  const [chart, setChart] = useState<BodyFinding[]>(
    () => encounter.bodyChart ?? [],
  );
  const [measurements, setMeasurements] = useState<Measurement[]>(
    () => encounter.measurements ?? [],
  );
  const [specialTests, setSpecialTests] = useState<SpecialTest[]>(
    () => encounter.specialTests ?? [],
  );
  const [flowsheet, setFlowsheet] = useState<FlowsheetEntry[]>(
    () => encounter.flowsheet ?? [],
  );
  const [goalProgress, setGoalProgress] = useState<GoalProgressRow[]>(
    () => encounter.goalProgress ?? [],
  );
  const [saved, setSaved] = useState(() => ({
    goalProgress: JSON.stringify(encounter.goalProgress ?? []),
    flowsheet: JSON.stringify(encounter.flowsheet ?? []),
    measurements: JSON.stringify(encounter.measurements ?? []),
    specialTests: JSON.stringify(
      stripDefinitions(encounter.specialTests ?? []),
    ),
    pain: JSON.stringify(encounter.pain ?? emptyPain()),
    chart: JSON.stringify(encounter.bodyChart ?? []),
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
    if (JSON.stringify(pain) !== saved.pain) b.pain = pain;
    if (JSON.stringify(chart) !== saved.chart) b.bodyChart = chart;
    if (JSON.stringify(flowsheet) !== saved.flowsheet) b.flowsheet = flowsheet;
    if (JSON.stringify(goalProgress) !== saved.goalProgress)
      b.goalProgress = goalProgress;
    if (JSON.stringify(measurements) !== saved.measurements)
      b.measurements = measurements;
    const tests = stripDefinitions(specialTests);
    if (JSON.stringify(tests) !== saved.specialTests) b.specialTests = tests;
    return Object.keys(b).length > 1 ? b : null;
  }, [
    values,
    columns,
    subj,
    obj,
    pain,
    chart,
    measurements,
    specialTests,
    flowsheet,
    goalProgress,
    saved,
    saveVersion,
  ]);
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
        pain: b.pain ? JSON.stringify(b.pain) : s.pain,
        chart: b.bodyChart ? JSON.stringify(b.bodyChart) : s.chart,
        flowsheet: b.flowsheet ? JSON.stringify(b.flowsheet) : s.flowsheet,
        goalProgress: b.goalProgress
          ? JSON.stringify(b.goalProgress)
          : s.goalProgress,
        measurements: b.measurements
          ? JSON.stringify(b.measurements)
          : s.measurements,
        specialTests: b.specialTests
          ? JSON.stringify(b.specialTests)
          : s.specialTests,
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

  // Ctrl/Cmd+S saves now instead of waiting for the autosave pause.
  useEffect(() => {
    if (readOnly) return;
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "s") {
        e.preventDefault();
        if (body && !conflict && !save.isPending) save.mutate(body);
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [body, conflict, readOnly, save]);

  // Someone else saving while this editor is open is caught by the
  // server on our next save (409); this check warns before typing more.
  const status = useQuery({
    queryKey: ["encounter-status", note.id],
    queryFn: () => fetchEncounterStatus(note.id),
    refetchInterval: 20_000,
    enabled: !readOnly && !conflict,
  });
  const otherSave =
    !conflict &&
    !save.isPending &&
    status.data &&
    status.data.saveVersion > saveVersion
      ? status.data
      : null;

  const storageKey = `encounter-collapsed:${encounter.template!.templateId}`;
  const [collapsed, setCollapsed] = useState<Set<string>>(() => {
    try {
      return new Set<string>(
        JSON.parse(localStorage.getItem(storageKey) ?? "[]"),
      );
    } catch {
      return new Set<string>();
    }
  });
  const setCollapsedAndRemember = (next: Set<string>) => {
    setCollapsed(next);
    try {
      localStorage.setItem(storageKey, JSON.stringify([...next]));
    } catch {
      /* per-device convenience only */
    }
  };
  const toggleSection = (key: string) => {
    const next = new Set(collapsed);
    if (next.has(key)) next.delete(key);
    else next.add(key);
    setCollapsedAndRemember(next);
  };
  const goTo = (key: string) => {
    if (collapsed.has(key)) toggleSection(key);
    window.setTimeout(() => {
      document
        .getElementById(`enc-${key}`)
        ?.scrollIntoView({ behavior: "smooth", block: "start" });
      document
        .getElementById(`enc-${key}-toggle`)
        ?.focus({ preventScroll: true });
    }, 0);
  };

  const setValue = (v: FieldValue) => {
    setSaveErrors([]);
    setValues((p) => ({ ...p, [v.key]: v }));
  };
  const setColumn = (c: string, t: string) => {
    setSaveErrors([]);
    setColumns((p) => ({ ...p, [c]: t }));
  };

  const reload = onReload;

  // Library entries for tests already on the note (warnings, result kind).
  const library = useQuery({
    queryKey: ["special-tests", "all"],
    queryFn: () => fetchSpecialTests({ includeInactive: true }),
    enabled: specialTests.some((t) => !!t.definitionId),
    staleTime: 5 * 60_000,
  });
  const definitions = useMemo(
    () => new Map((library.data ?? []).map((d) => [d.id, d])),
    [library.data],
  );

  // Where "Insert goal progress" writes: the note's goal-progress field
  // (daily note, progress report, discharge), else its assessment.
  const narrativeField = (() => {
    const fields = sections.flatMap((x) => x.fields);
    return (
      ["progressTowardGoals", "functionalImprovement", "functionalOutcome"]
        .map((k) => fields.find((f) => f.key === k))
        .find(Boolean) ?? fields.find((f) => f.noteColumn === "assessment")
    );
  })();
  const goalNarrativeTarget = narrativeField
    ? {
        label: narrativeField.label,
        insert: (text: string) => {
          const join = (old: string) =>
            old.trim() ? `${old.trimEnd()}\n${text}` : text;
          if (narrativeField.noteColumn)
            setColumn(
              narrativeField.noteColumn,
              join(columns[narrativeField.noteColumn as keyof Columns] ?? ""),
            );
          else
            setValue({
              ...values[narrativeField.key],
              key: narrativeField.key,
              text: join(values[narrativeField.key]?.text ?? ""),
            });
        },
      }
    : null;

  const renderComponent = (component: string): ReactNode => {
    switch (component) {
      case "painAssessment":
        return (
          <PainAssessmentPanel
            value={pain}
            onChange={(next) => {
              setSaveErrors([]);
              setPain(next);
            }}
            readOnly={readOnly}
            previous={encounter.previous?.pain}
          />
        );
      case "bodyChart":
        return (
          <BodyChart
            findings={chart}
            onChange={(next) => {
              setSaveErrors([]);
              setChart(next);
            }}
            readOnly={readOnly}
            previous={encounter.previous?.bodyChart}
            previousDate={
              encounter.previous
                ? formatDate(encounter.previous.serviceDate)
                : undefined
            }
          />
        );
      case "measurements":
        return (
          <MeasurementsPanel
            value={measurements}
            onChange={(next) => {
              setSaveErrors([]);
              setMeasurements(next);
            }}
            readOnly={readOnly}
            history={encounter.measurementHistory ?? []}
          />
        );
      case "specialTests":
        return (
          <SpecialTestsPanel
            value={specialTests}
            onChange={(next) => {
              setSaveErrors([]);
              setSpecialTests(next);
            }}
            readOnly={readOnly}
            history={encounter.specialTestHistory ?? []}
            definitions={definitions}
          />
        );
      case "outcomes":
        return (
          <OutcomesWorkspace
            patientId={note.patientId}
            noteId={note.id}
            serviceDate={note.serviceDate}
            readOnly={readOnly}
          />
        );
      case "interventions":
        return (
          <FlowsheetPanel
            value={flowsheet}
            onChange={(next) => {
              setSaveErrors([]);
              setFlowsheet(next);
            }}
            readOnly={readOnly}
            previous={encounter.previousFlowsheet}
            ruleVariant={encounter.flowsheetSummary?.ruleVariant ?? "Medicare"}
            canShareGroups={canAccess(
              user,
              RoleSets.OrganizationAdministration,
            )}
          />
        );
      case "goals":
        return (
          <GoalTracker
            patientId={note.patientId}
            value={goalProgress}
            onChange={(next) => {
              setSaveErrors([]);
              setGoalProgress(next);
            }}
            readOnly={readOnly}
            canApprove={canSignClinicalNotes(user)}
            insertTarget={goalNarrativeTarget}
          />
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
  const progressOf = (sec: TemplateSection): SectionProgress => {
    const required = sec.fields.filter(
      (f) => f.isRequired && f.fieldType !== FieldType.Signature,
    );
    const left = missingRequired(sec.fields, values, columns).length;
    if (left > 0) return { state: "missing", left };
    if (required.length > 0) return { state: "done", left: 0 };
    const filled = sec.fields.some((f) =>
      f.noteColumn
        ? !!columns[f.noteColumn as keyof Columns]?.trim()
        : valueAsText(values[f.key]) != null,
    );
    return { state: filled ? "filled" : "empty", left: 0 };
  };

  return (
    <div className="pb-10">
      <EncounterHeaderView
        encounter={encounter}
        readOnly={readOnly}
        status={
          editable &&
          missing.length === 0 &&
          !(compliance.data ?? []).some((f) => f.finalizationBlocker)
            ? DocumentationStatusLabels[DocumentationStatus.ReadyToSign]
            : note.documentationStatus != null
              ? (DocumentationStatusLabels[note.documentationStatus] ?? "")
              : (NoteStatusLabels[note.status] ?? "")
        }
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
      {otherSave && (
        <div
          role="status"
          className="mb-4 rounded-md border border-warning bg-warning-light px-3 py-2 text-[#333]"
        >
          <p>
            <strong>
              {otherSave.savedByName ?? "Someone else"} saved this note
            </strong>{" "}
            at {time(otherSave.savedAt)} while you have it open. Reload to see
            their changes before you continue — otherwise your next change won’t
            be saved.
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

      {note.status === NoteStatus.ReturnedForCorrection &&
        note.returnReason && (
          <div
            role="alert"
            className="mb-4 rounded-md border border-warning bg-warning-light px-3 py-2 text-[#333]"
          >
            <p>
              <strong>Returned for correction:</strong> {note.returnReason}
            </p>
            <p className="text-text-muted">
              Correct the note, then sign it again to resubmit it.
            </p>
          </div>
        )}
      {SUMMARY_NOTE_TYPES.has(note.noteType) && (
        <EpisodePanel
          noteId={note.id}
          readOnly={readOnly}
          saveVersion={saveVersion}
          busy={dirty || save.isPending}
          prefilledAt={note.prefilledAt}
          prefillReviewedAt={note.prefillReviewedAt}
          onFilled={reload}
        />
      )}

      <nav
        aria-label="Note sections"
        className="sticky top-[72px] z-10 -mx-4 mb-4 overflow-x-auto bg-surface px-4 py-2 shadow-sm md:-mx-6 md:px-6"
      >
        <div className="flex items-center gap-2">
          <div className="seg-group flex-nowrap">
            {shown.map((sec) => {
              const pr = progressOf(sec);
              return (
                <button
                  key={sec.key}
                  type="button"
                  className="seg-btn"
                  onClick={() => goTo(sec.key)}
                >
                  {sec.title}
                  <ProgressMark progress={pr} compact />
                </button>
              );
            })}
            <button
              type="button"
              className="seg-btn"
              onClick={() => goTo("sign")}
            >
              Sign
            </button>
          </div>
          <button
            type="button"
            className="btn-refresh shrink-0"
            onClick={() =>
              setCollapsedAndRemember(
                collapsed.size === shown.length
                  ? new Set()
                  : new Set(shown.map((x) => x.key)),
              )
            }
          >
            {collapsed.size === shown.length ? "Expand all" : "Collapse all"}
          </button>
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
              <button
                id={`enc-${s.key}-toggle`}
                type="button"
                className="flex w-full flex-wrap items-center gap-x-3 text-left"
                aria-expanded={!collapsed.has(s.key)}
                aria-controls={`enc-${s.key}-body`}
                onClick={() => toggleSection(s.key)}
              >
                <span aria-hidden="true" className="text-base text-text-muted">
                  {collapsed.has(s.key) ? "▸" : "▾"}
                </span>
                {s.title}
                <ProgressMark progress={progressOf(s)} />
              </button>
            </h2>
            <div id={`enc-${s.key}-body`} hidden={collapsed.has(s.key)}>
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
                renderFieldExtra={(f) =>
                  !readOnly &&
                  (f.noteColumn === "assessment" || f.noteColumn === "plan") ? (
                    <AiDraftAssist
                      noteId={note.id}
                      section={f.noteColumn}
                      currentText={columns[f.noteColumn]}
                      onInsert={(t) => setColumn(f.noteColumn!, t)}
                    />
                  ) : null
                }
              />
            </div>
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
            <>
              <SignCard
                noteId={note.id}
                missing={missing}
                blockers={(compliance.data ?? []).filter(
                  (f) =>
                    f.finalizationBlocker &&
                    f.code !== "missing_required_fields",
                )}
                canSign={
                  canSignClinicalNotes(user) &&
                  (record.data?.actions.canSign ?? true) &&
                  !readOnly
                }
                isAmendment={!!note.amendsNoteId}
                createsPlan={CREATES_PLAN.has(note.noteType)}
                flush={async () =>
                  body && !conflict
                    ? (await save.mutateAsync(body)).saveVersion
                    : saveVersion
                }
                submitsForCosign={!!record.data?.actions.signSubmitsForCosign}
                onSigned={(status) => {
                  showToast(
                    status === NoteStatus.ReviewRequired
                      ? "Note submitted for a supervising PT’s review."
                      : "Note signed.",
                  );
                  void queryClient.invalidateQueries({
                    queryKey: ["encounter", note.id],
                  });
                  void queryClient.invalidateQueries({ queryKey: ["chart"] });
                  void queryClient.invalidateQueries({
                    queryKey: ["workflow"],
                  });
                  void queryClient.invalidateQueries({
                    queryKey: ["patient", note.patientId],
                  });
                }}
              />
              {record.data && (
                <VoidNoteForm noteId={note.id} actions={record.data.actions} />
              )}
            </>
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
  submitsForCosign,
  onSigned,
}: {
  noteId: string;
  missing: string[];
  blockers: { code: string; title: string; detail: string }[];
  canSign: boolean;
  isAmendment: boolean;
  createsPlan: boolean;
  /** Saves pending changes; resolves to the saved version being signed. */
  flush: () => Promise<number>;
  submitsForCosign: boolean;
  onSigned: (status: number) => void;
}) {
  const [attested, setAttested] = useState(false);
  const [password, setPassword] = useState("");
  const sign = useMutation({
    mutationFn: async () => {
      const version = await flush(); // never sign stale content
      return signChartNote(noteId, password, version);
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
        {sign.isPending
          ? "Signing…"
          : submitsForCosign
            ? "Sign and submit for PT review"
            : "Sign note"}
      </button>
      <p className="text-text-muted">
        {submitsForCosign
          ? "Your signature submits the note to a supervising PT, who cosigns it or returns it to you for correction. You can’t edit it while it is with the PT."
          : isAmendment
            ? "Signing this amendment replaces the original note in the record; the original stays viewable, marked Amended."
            : createsPlan
              ? "Signing locks the note and creates the patient’s plan of care from it."
              : "Signing locks the note and completes the visit. Corrections after signing are made with an addendum or an amendment."}
      </p>
    </div>
  );
}

interface SectionProgress {
  state: "done" | "missing" | "filled" | "empty";
  left: number;
}

/** A section's completion: required fields all done, how many are left,
 * or (no required fields) whether anything was entered. */
function ProgressMark({
  progress,
  compact = false,
}: {
  progress: SectionProgress;
  compact?: boolean;
}) {
  // Plain spaces between the parts keep the button's spoken name readable
  // ("Subjective 2 required left"), not run together.
  if (progress.state === "done")
    return (
      <>
        {" "}
        <span className="ml-2 text-base font-normal text-success">
          <span aria-hidden="true">✓</span>{" "}
          <span className={compact ? "sr-only" : ""}>
            {compact ? "complete" : "Complete"}
          </span>
        </span>
      </>
    );
  if (progress.state === "missing")
    return (
      <>
        {" "}
        <span className="ml-2 rounded-full bg-warning-light px-2 text-base font-normal text-[#7a4a00]">
          {progress.left}{" "}
          <span className={compact ? "sr-only" : ""}>required left</span>
        </span>
      </>
    );
  if (progress.state === "filled" && !compact)
    return (
      <>
        {" "}
        <span className="ml-2 text-base font-normal text-text-muted">
          Entered
        </span>
      </>
    );
  return null;
}

/** Special tests as sent to the server (without the client-only library entry). */
function stripDefinitions(tests: SpecialTest[]): SpecialTest[] {
  return tests.map((t) => {
    const copy = { ...t };
    delete copy.definition;
    return copy;
  });
}
