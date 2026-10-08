import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { PatientReportPrintPage } from "./PatientReportPrintPage";
import * as api from "./api";
import { formatInZone } from "./usePrintOutput";
import { fetchPatientDetail } from "../admin/api";
import { fetchCurrentOrganization } from "../organizations/api";
import { fetchMeasurementHistory, fetchPlansOfCare } from "../encounter/api";
import { fetchPatientGoals } from "../encounter/goals/api";
import { fetchPatientOutcomes, fetchOutcomeDefinitions } from "../encounter/outcomes/api";

vi.mock("./api", async (orig) => ({
  ...(await orig<typeof import("./api")>()),
  recordReportOutput: vi.fn(),
  fetchBodyChartHistory: vi.fn(),
}));
vi.mock("../admin/api", () => ({ fetchPatientDetail: vi.fn() }));
vi.mock("../organizations/api", () => ({ fetchCurrentOrganization: vi.fn() }));
vi.mock("../encounter/api", () => ({
  fetchMeasurementHistory: vi.fn(),
  fetchPlansOfCare: vi.fn(),
}));
vi.mock("../encounter/goals/api", () => ({ fetchPatientGoals: vi.fn() }));
vi.mock("../encounter/outcomes/api", () => ({
  fetchPatientOutcomes: vi.fn(),
  fetchOutcomeDefinitions: vi.fn(),
}));

const goal = {
  id: "g1",
  patientId: "p1",
  functionalLimitation: "Stairs",
  functionalTask: "Climb 12 stairs with one rail",
  term: 0,
  baselineValue: 4,
  targetValue: 12,
  currentValue: 8,
  unit: "stairs",
  measurementMethod: "observation",
  targetDate: "2026-11-15",
  status: 1,
  progressPercent: 50,
  approvedById: null,
  approvedAt: null,
  planOfCareId: "poc1",
  comments: null,
  version: 1,
};

function setup(report: string) {
  vi.mocked(fetchPatientDetail).mockResolvedValue({
    id: "p1",
    fullName: "Quinn Alvarez",
    medicalRecordNumber: "SM-1",
    dateOfBirth: "2001-07-30",
  } as never);
  vi.mocked(fetchCurrentOrganization).mockResolvedValue({
    id: "o",
    name: "Source Motion Physical Therapy",
    locations: [],
    timezone: "America/Chicago",
  });
  vi.mocked(api.recordReportOutput).mockResolvedValue(undefined);
  vi.mocked(fetchPatientGoals).mockResolvedValue([goal]);
  vi.mocked(fetchPlansOfCare).mockResolvedValue([
    {
      id: "poc1",
      status: 1,
      startDate: "2026-10-01",
      endDate: "2026-11-26",
      frequencyPerWeek: 2,
      durationWeeks: 8,
      treatmentDiagnosis: "Right knee pain",
      prognosis: "Good",
      plannedInterventions: "Therapeutic exercise",
      homeProgram: "Quad sets",
      sourceNoteId: "n1",
    },
  ]);
  vi.mocked(fetchMeasurementHistory).mockResolvedValue([
    { noteId: "n1", serviceDate: "2026-10-01", category: 0, item: "Knee", movement: "Flexion", side: 1, mode: "AROM", unit: "deg", numericValue: 95, textValue: null },
    { noteId: "n2", serviceDate: "2026-10-08", category: 0, item: "Knee", movement: "Flexion", side: 1, mode: "AROM", unit: "deg", numericValue: 110, textValue: null },
  ]);
  vi.mocked(api.fetchBodyChartHistory).mockResolvedValue([]);
  vi.mocked(fetchPatientOutcomes).mockResolvedValue([]);
  vi.mocked(fetchOutcomeDefinitions).mockResolvedValue([]);
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <MemoryRouter initialEntries={[`/patients/p1/reports/${report}/print`]}>
        <Routes>
          <Route path="/patients/:patientId/reports/:report/print" element={<PatientReportPrintPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("formatInZone", () => {
  it("shows times in the clinic's zone, with the zone named", () => {
    expect(formatInZone("2026-10-07T15:00:00Z", "America/Chicago")).toBe(
      "10/07/2026, 10:00 AM CDT",
    );
    expect(formatInZone("2026-12-07T15:00:00Z", "America/New_York")).toBe(
      "12/07/2026, 10:00 AM EST",
    );
  });
});

describe("PatientReportPrintPage", () => {
  let print: ReturnType<typeof vi.spyOn>;
  beforeEach(() => {
    print = vi.spyOn(window, "print").mockImplementation(() => {});
  });
  afterEach(() => {
    print.mockRestore();
    vi.clearAllMocks();
  });

  it("prints the plan of care with its goals, audited first", async () => {
    setup("plan-of-care");
    expect(await screen.findByText("Plan of care")).toBeInTheDocument();
    expect(screen.getByText("Quinn Alvarez")).toBeInTheDocument();
    expect(screen.getByText("Right knee pain")).toBeInTheDocument();
    expect(screen.getByText(/Patient will climb 12 stairs/)).toBeInTheDocument();
    await waitFor(() => expect(print).toHaveBeenCalled(), { timeout: 3000 });
    expect(api.recordReportOutput).toHaveBeenCalledWith("p1", "plan-of-care", "print");
  });

  it("compares measurements across visits", async () => {
    setup("measurements");
    expect(await screen.findByText("Range of motion")).toBeInTheDocument();
    expect(screen.getByText("Knee Flexion AROM (R)")).toBeInTheDocument();
    expect(screen.getByText("95°")).toBeInTheDocument();
    expect(screen.getByText("110°")).toBeInTheDocument();
  });

  it("says when there is nothing to report", async () => {
    setup("body-chart");
    expect(await screen.findByText("No signed body chart recorded yet.")).toBeInTheDocument();
  });

  it("rejects an unknown report without auditing or printing", async () => {
    setup("everything");
    expect(await screen.findByText("Unknown report.")).toBeInTheDocument();
    await new Promise((r) => setTimeout(r, 900));
    expect(api.recordReportOutput).not.toHaveBeenCalled();
    expect(print).not.toHaveBeenCalled();
  });
});

describe("PageMargins", () => {
  it("puts clinic, patient, title and page numbers on every page, escaping quotes", async () => {
    const { PageMargins } = await import("./PageMargins");
    const { container } = render(
      <PageMargins clinic='The "Best" PT' patient="Quinn Alvarez" mrn="SM-1" title="Goal progress" />,
    );
    const css = container.querySelector("style")!.textContent!;
    expect(css).toContain('content: "The \\"Best\\" PT · Quinn Alvarez · MRN SM-1"');
    expect(css).toContain('@top-right { content: "Goal progress"');
    expect(css).toContain('"Page " counter(page) " of " counter(pages)');
  });
});
