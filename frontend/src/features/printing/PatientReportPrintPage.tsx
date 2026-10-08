import type { ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { useParams } from "react-router-dom";
import { fetchPatientDetail } from "../admin/api";
import { fetchCurrentOrganization } from "../organizations/api";
import { fetchMeasurementHistory, fetchPlansOfCare } from "../encounter/api";
import { PLAN_STATUS } from "../encounter/components/ClinicalPanels";
import { describe } from "../encounter/bodychart/regions";
import { fetchPatientGoals } from "../encounter/goals/api";
import { GoalStatusLabels, statement } from "../encounter/goals/model";
import { fetchPatientOutcomes } from "../encounter/outcomes/api";
import { formatScore } from "../encounter/outcomes/model";
import { useOutcomeDefinitions } from "../encounter/outcomes/queries";
import { NoteTypeLabels } from "../workflow/types";
import {
  PatientReports,
  fetchBodyChartHistory,
  recordReportOutput,
  type PatientReport,
} from "./api";
import { PageMargins } from "./PageMargins";
import { PrintActions } from "./PrintActions";
import { formatInZone, usePrintOutput } from "./usePrintOutput";

const formatDate = (iso: string | null | undefined) => {
  if (!iso) return "—";
  const [y, m, d] = iso.slice(0, 10).split("-");
  return `${m}/${d}/${y}`;
};

const isReport = (r: string): r is PatientReport => r in PatientReports;

/** A patient-level report (plan of care, body charts, measurements, goals,
 * outcomes) as a printable document with letterhead and identifiers. Built
 * from signed documentation; each print or export is audited first. */
export function PatientReportPrintPage() {
  const { patientId = "", report = "" } = useParams();
  const patient = useQuery({
    queryKey: ["admin", "patient", patientId],
    queryFn: () => fetchPatientDetail(patientId),
  });
  const org = useQuery({
    queryKey: ["organizations", "current"],
    queryFn: fetchCurrentOrganization,
  });
  const known = isReport(report);
  const body = useQuery({
    queryKey: ["patient-report", patientId, report],
    queryFn: () => loadReport(patientId, report as PatientReport),
    enabled: known,
  });
  const definitions = useOutcomeDefinitions();
  const ready =
    known &&
    !!patient.data &&
    !!org.data &&
    !!body.data &&
    (report !== "outcomes" || !definitions.isLoading);
  const printing = usePrintOutput(ready, (kind) =>
    recordReportOutput(patientId, report as PatientReport, kind),
  );

  if (!known) return <p className="alert-error m-4">Unknown report.</p>;
  const error = patient.error ?? org.error ?? body.error;
  if (error) return <p className="alert-error m-4">{error.message}</p>;
  if (!ready)
    return <p className="m-4 text-text-muted">Preparing the report…</p>;

  const p = patient.data!;
  const data = body.data!;
  const tz = org.data!.timezone;
  return (
    <div className="mx-auto max-w-4xl bg-white p-4 text-[#222] sm:p-8 print:max-w-none print:p-0">
      <PrintActions {...printing} />
      <PageMargins
        clinic={org.data!.name}
        patient={p.fullName}
        mrn={p.medicalRecordNumber}
        title={PatientReports[report]}
      />

      <header className="border-b-2 border-[#1565b8] pb-3">
        <p className="text-2xl font-bold text-[#1565b8]">{org.data!.name}</p>
        <p className="text-xl font-bold">{PatientReports[report]}</p>
      </header>

      <dl className="my-4 grid grid-cols-2 gap-x-6 gap-y-1 sm:grid-cols-4">
        <Item label="Patient" value={p.fullName} />
        <Item label="MRN" value={p.medicalRecordNumber} />
        <Item label="Date of birth" value={formatDate(p.dateOfBirth)} />
        <Item label="Report date" value={formatInZone(new Date().toISOString(), tz)} />
      </dl>

      {data.kind === "plan-of-care" && <PlanOfCareReport data={data} />}
      {data.kind === "body-chart" && <BodyChartReport data={data} />}
      {data.kind === "measurements" && <MeasurementReport data={data} />}
      {data.kind === "goals" && <GoalReport data={data} />}
      {data.kind === "outcomes" && (
        <OutcomeReport data={data} definitions={definitions.data ?? []} />
      )}

      <footer className="mt-6 border-t border-[#999] pt-3 text-text-muted">
        Compiled from the patient's documentation. Confidential patient
        information.
      </footer>
    </div>
  );
}

async function loadReport(patientId: string, report: PatientReport) {
  switch (report) {
    case "plan-of-care": {
      const [plans, goals] = await Promise.all([
        fetchPlansOfCare(patientId),
        fetchPatientGoals(patientId),
      ]);
      return { kind: report, plans, goals } as const;
    }
    case "body-chart":
      return {
        kind: report,
        charts: await fetchBodyChartHistory(patientId),
      } as const;
    case "measurements":
      return {
        kind: report,
        rows: await fetchMeasurementHistory(patientId),
      } as const;
    case "goals":
      return { kind: report, goals: await fetchPatientGoals(patientId) } as const;
    case "outcomes":
      return {
        kind: report,
        scores: await fetchPatientOutcomes(patientId),
      } as const;
  }
}
type ReportData = Awaited<ReturnType<typeof loadReport>>;
type Of<K extends PatientReport> = Extract<ReportData, { kind: K }>;

function PlanOfCareReport({ data }: { data: Of<"plan-of-care"> }) {
  if (data.plans.length === 0) return <Empty what="plan of care" />;
  return (
    <div className="space-y-5">
      {data.plans.map((plan) => {
        const goals = data.goals.filter(
          (g) => g.planOfCareId === plan.id,
        );
        return (
          <section key={plan.id} className="break-inside-avoid">
            <h2 className="text-lg font-bold">
              {formatDate(plan.startDate)} – {formatDate(plan.endDate)} (
              {PLAN_STATUS[plan.status] ?? "Plan"})
            </h2>
            <dl className="mt-1 space-y-1">
              <Line label="Frequency / duration">
                {plan.frequencyPerWeek ?? "—"}× per week for{" "}
                {plan.durationWeeks ?? "—"} weeks
              </Line>
              <Line label="Treatment diagnosis">{plan.treatmentDiagnosis}</Line>
              <Line label="Prognosis">{plan.prognosis}</Line>
              <Line label="Planned interventions">
                {plan.plannedInterventions}
              </Line>
              <Line label="Home program">{plan.homeProgram}</Line>
            </dl>
            {goals.length > 0 && (
              <>
                <h3 className="mt-2 font-bold">Goals</h3>
                <ul className="list-disc pl-6">
                  {goals.map((g) => (
                    <li key={g.id}>
                      {statement(g)} — {GoalStatusLabels[g.status]}
                    </li>
                  ))}
                </ul>
              </>
            )}
          </section>
        );
      })}
    </div>
  );
}

function BodyChartReport({ data }: { data: Of<"body-chart"> }) {
  if (data.charts.length === 0) return <Empty what="signed body chart" />;
  return (
    <div className="space-y-4">
      {data.charts.map((c) => (
        <section key={c.noteId} className="break-inside-avoid">
          <h2 className="text-lg font-bold">
            {formatDate(c.serviceDate)} — {NoteTypeLabels[c.noteType] ?? "Note"}
          </h2>
          <ul className="list-disc pl-6">
            {c.findings.map((f, i) => (
              <li key={f.id ?? i}>{describe(f)}</li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}

const CATEGORY_NAMES = [
  "Range of motion",
  "Strength",
  "Sensation",
  "Dermatome",
  "Myotome",
  "Reflex",
  "Tone",
  "Coordination",
  "Cranial nerve",
  "Gait",
  "Balance",
  "Functional",
];
const SIDE = ["L", "R", "Both", "Mid"];

/** Each measured item across the most recent dates (oldest → newest). */
function MeasurementReport({ data }: { data: Of<"measurements"> }) {
  if (data.rows.length === 0) return <Empty what="signed measurement" />;
  const visits = [
    ...new Map(data.rows.map((r) => [r.noteId, r.serviceDate])).entries(),
  ]
    .sort((a, b) => a[1].localeCompare(b[1]))
    .slice(-6);
  const byCategory = new Map<number, Map<string, Map<string, string>>>();
  for (const r of data.rows) {
    const label = [
      r.item,
      r.movement,
      r.mode,
      r.side != null ? `(${SIDE[r.side]})` : null,
    ]
      .filter(Boolean)
      .join(" ");
    const value =
      r.numericValue != null
        ? `${r.numericValue}${r.unit === "deg" || r.unit === "°" ? "°" : r.unit ? ` ${r.unit}` : ""}`
        : (r.textValue ?? "");
    if (!byCategory.has(r.category)) byCategory.set(r.category, new Map());
    const rows = byCategory.get(r.category)!;
    if (!rows.has(label)) rows.set(label, new Map());
    rows.get(label)!.set(r.noteId, value);
  }
  return (
    <div className="space-y-4">
      {[...byCategory.entries()]
        .sort((a, b) => a[0] - b[0])
        .map(([category, rows]) => (
          <section key={category} className="break-inside-avoid">
            <h2 className="text-lg font-bold">
              {CATEGORY_NAMES[category] ?? "Other"}
            </h2>
            <table className="print-table">
              <thead>
                <tr>
                  <th>Measure</th>
                  {visits.map(([id, date]) => (
                    <th key={id}>{formatDate(date)}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {[...rows.entries()].map(([label, values]) => (
                  <tr key={label}>
                    <td>{label}</td>
                    {visits.map(([id]) => (
                      <td key={id}>{values.get(id) ?? "—"}</td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </section>
        ))}
    </div>
  );
}

function GoalReport({ data }: { data: Of<"goals"> }) {
  if (data.goals.length === 0) return <Empty what="goal" />;
  return (
    <table className="print-table">
      <thead>
        <tr>
          <th>Goal</th>
          <th>Baseline</th>
          <th>Current</th>
          <th>Target</th>
          <th>Progress</th>
          <th>Status</th>
        </tr>
      </thead>
      <tbody>
        {data.goals.map((g) => (
          <tr key={g.id}>
            <td>
              {g.term === 1 ? "LTG" : "STG"}: {statement(g)}
            </td>
            <td>
              {g.baselineValue} {g.unit}
            </td>
            <td>{g.currentValue != null ? `${g.currentValue} ${g.unit}` : "—"}</td>
            <td>
              {g.targetValue} {g.unit}
            </td>
            <td>{g.progressPercent != null ? `${g.progressPercent}%` : "—"}</td>
            <td>{GoalStatusLabels[g.status]}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function OutcomeReport({
  data,
  definitions,
}: {
  data: Of<"outcomes">;
  definitions: { measure: number; name: string; abbreviation: string }[];
}) {
  if (data.scores.length === 0) return <Empty what="outcome score" />;
  const measures = [...new Set(data.scores.map((s) => s.measure))];
  return (
    <div className="space-y-4">
      {measures.map((m) => {
        const d = definitions.find((x) => x.measure === m);
        const scores = data.scores
          .filter((s) => s.measure === m)
          .sort((a, b) => a.measuredOn.localeCompare(b.measuredOn));
        return (
          <section key={m} className="break-inside-avoid">
            <h2 className="text-lg font-bold">
              {d ? `${d.name} (${d.abbreviation})` : "Outcome measure"}
            </h2>
            <table className="print-table">
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Score</th>
                  <th>Change from first</th>
                  <th>Interpretation</th>
                </tr>
              </thead>
              <tbody>
                {scores.map((s) => {
                  const delta =
                    Math.round((s.score - scores[0].score) * 100) / 100;
                  return (
                    <tr key={s.id}>
                      <td>{formatDate(s.measuredOn)}</td>
                      <td>
                        {formatScore(
                          d as Parameters<typeof formatScore>[0],
                          s.score,
                        )}
                      </td>
                      <td>
                        {s === scores[0]
                          ? "Baseline"
                          : `${delta > 0 ? "+" : ""}${delta}`}
                      </td>
                      <td>{s.interpretation ?? "—"}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </section>
        );
      })}
    </div>
  );
}

function Empty({ what }: { what: string }) {
  return <p className="text-text-muted">No {what} recorded yet.</p>;
}

function Line({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <dt className="inline font-bold">{label}: </dt>
      <dd className="inline">{children || "—"}</dd>
    </div>
  );
}

function Item({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-text-muted">{label}</dt>
      <dd className="font-bold">{value}</dd>
    </div>
  );
}
