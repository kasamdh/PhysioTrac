import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { PatientDocumentationPage } from "./PatientDocumentationPage";
import { apiRequest } from "../../lib/apiClient";
import { fetchPatientDetail } from "../admin/api";
import * as chartApi from "../charting/api";
import { NoteStatus, NoteType } from "../workflow/types";
import type { ChartNote } from "../charting/types";
import { ToastProvider } from "../../components/Toast";
import { useAuth } from "../auth/AuthProvider";
import { UserRole } from "../auth/types";

vi.mock("../auth/AuthProvider", () => ({ useAuth: vi.fn() }));
vi.mock("../encounter/api", () => ({
  createPatientNote: vi.fn(),
  fetchEncounter: vi.fn().mockResolvedValue({ pain: null, bodyChart: [] }),
  fetchPainHistory: vi.fn().mockResolvedValue([]),
  fetchMeasurementHistory: vi.fn().mockResolvedValue([]),
}));

vi.mock("../encounter/outcomes/api", () => ({
  fetchOutcomeDefinitions: vi.fn().mockResolvedValue([]),
  fetchPatientOutcomes: vi.fn().mockResolvedValue([]),
  recordOutcomeScore: vi.fn(),
  deleteOutcomeScore: vi.fn(),
}));
vi.mock("../encounter/goals/api", () => ({
  goalsKey: (id: string) => ["chart", "goals", id],
  fetchPatientGoals: vi.fn().mockResolvedValue([]),
  fetchGoalHistory: vi.fn().mockResolvedValue([]),
  createPatientGoal: vi.fn(),
  editGoal: vi.fn(),
  approvePatientGoal: vi.fn(),
}));
vi.mock("../../lib/apiClient", () => ({ apiRequest: vi.fn() }));
vi.mock("../admin/api", () => ({ fetchPatientDetail: vi.fn() }));
vi.mock("../charting/api", () => ({
  fetchOutcomes: vi.fn(),
  fetchPullForward: vi.fn(),
  fetchGoals: vi.fn(),
  updateGoalProgress: vi.fn(),
  fetchInterventions: vi.fn(),
  fetchInterventionSummary: vi.fn(),
  addIntervention: vi.fn(),
  updateIntervention: vi.fn(),
  deleteIntervention: vi.fn(),
  fetchNoteRecord: vi.fn(),
}));

const note = (
  id: string,
  date: string,
  painNow: number,
  arom: string,
): ChartNote => ({
  id,
  patientId: "p1",
  therapistId: "u1",
  appointmentId: null,
  noteType: NoteType.Daily,
  status: NoteStatus.Signed,
  serviceDate: date,
  subjective: "Feels better",
  objective: "Gait steady",
  interventions: null,
  assessment: "Improving",
  plan: "Continue",
  planOfCareStart: null,
  planOfCareEnd: null,
  frequencyPerWeek: null,
  durationWeeks: null,
  signatureName: "Jamie Chen",
  signedAt: `${date}T15:00:00Z`,
  cosignRequired: false,
  subjectiveDetailsJson: JSON.stringify({ painNow }),
  objectiveMeasurementsJson: JSON.stringify({
    rom: [
      {
        id: `r${id}`,
        joint: "Knee",
        motion: "Flexion",
        side: "R",
        arom,
        prom: "",
      },
    ],
  }),
});

function setup() {
  vi.mocked(useAuth).mockReturnValue({
    user: {
      id: "u1",
      username: "t",
      email: null,
      role: UserRole.Therapist,
      organizationId: "o",
      isPlatformSuperAdmin: false,
      mustChangePassword: false,
    },
    isLoading: false,
    loginError: null,
    isLoggingIn: false,
    signIn: vi.fn(),
    signOut: vi.fn(),
  });
  vi.mocked(fetchPatientDetail).mockResolvedValue({
    id: "p1",
    medicalRecordNumber: "SM-1",
    firstName: "Quinn",
    lastName: "Alvarez",
    fullName: "Quinn Alvarez",
    dateOfBirth: "2001-07-30",
    age: 25,
    phone: null,
    email: null,
    address: null,
    emergencyContact: null,
    preferredLanguage: null,
    diagnoses: null,
    precautions: null,
    assignedTherapistId: null,
    primaryLocationId: null,
    primaryCareProviderId: null,
    referringProviderId: null,
    status: 0,
  });
  vi.mocked(apiRequest).mockImplementation(async (path: string) => {
    if (path.endsWith("/progress-note-status"))
      return {
        isDue: true,
        dueByDayCount: false,
        dueByVisitCount: true,
        visitsSinceLastProgressNote: 10,
        reassessmentDue: null,
      };
    return [
      note("n2", "2026-10-07", 3, "120"),
      note("n1", "2026-10-01", 6, "100"),
    ];
  });
  vi.mocked(chartApi.fetchOutcomes).mockResolvedValue([]);
  vi.mocked(chartApi.fetchPullForward).mockResolvedValue({
    activeGoals: [],
    lastObjectiveMeasurementsJson: null,
    activeDiagnoses: [],
  });
  vi.mocked(chartApi.fetchGoals).mockResolvedValue([]);
  vi.mocked(chartApi.fetchInterventions).mockResolvedValue([
    {
      id: "i1",
      noteId: "n2",
      description: "Bridges 3x10",
      bodyRegion: "Hip",
      category: 0,
      minutes: 12,
      units: null,
      isTimed: true,
      order: 0,
      patientResponse: "Tolerated well",
    },
  ]);
  vi.mocked(chartApi.fetchNoteRecord).mockResolvedValue({
    actions: {
      canEdit: false,
      canSign: false,
      canCosign: false,
      canAddAddendum: true,
      canAmend: true,
      canLock: false,
    },
    authorName: "Jamie Chen",
    cosignedByName: null,
    amendmentNoteId: null,
    amendmentStatus: null,
    addenda: [
      {
        id: "ad1",
        noteId: "n2",
        authorId: "u1",
        reason: "Late entry",
        body: "Called patient re HEP.",
        createdAt: "2026-10-07T18:00:00Z",
        authorName: "Jamie Chen",
      },
    ],
  });
  vi.mocked(chartApi.fetchInterventionSummary).mockResolvedValue({
    timedMinutes: 12,
    untimedCount: 0,
    estimatedTimedUnits: 1,
    ruleVariant: "Medicare",
  });
  render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <ToastProvider>
        <MemoryRouter initialEntries={["/patients/p1/documentation"]}>
          <Routes>
            <Route
              path="/patients/:patientId/documentation"
              element={<PatientDocumentationPage />}
            />
          </Routes>
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

describe("PatientDocumentationPage", () => {
  it("summarizes the patient's documentation and trends across visits", async () => {
    setup();
    expect(
      await screen.findByRole("heading", {
        name: "Quinn Alvarez — Documentation",
      }),
    ).toBeInTheDocument();
    expect(await screen.findByText("Due now")).toBeInTheDocument();
    expect(
      screen.getByText("10 visits since the last one"),
    ).toBeInTheDocument();

    const trend = screen.getByText("Knee Flexion (R) AROM").closest("tr")!;
    expect(within(trend).getByText("100°")).toBeInTheDocument();
    expect(within(trend).getByText("120°")).toBeInTheDocument();
    expect(screen.getByText("Pain now").closest("tr")).toHaveTextContent(
      "6/10",
    );
  });

  it("expands a visit note to its full charting, interventions and patient response included", async () => {
    setup();
    await userEvent.click(
      await screen.findByRole("button", { name: /10\/07\/2026 — Daily note/ }),
    );
    expect(await screen.findByText("Bridges 3x10")).toBeInTheDocument();
    expect(screen.getByText("Tolerated well")).toBeInTheDocument();
    expect(screen.getByText("Gait steady")).toBeInTheDocument();
    expect(
      await screen.findByText("Called patient re HEP."),
    ).toBeInTheDocument();
    expect(
      screen.getAllByRole("link", { name: "Open note" })[0],
    ).toHaveAttribute("href", "/chart/n2");
  });
});
