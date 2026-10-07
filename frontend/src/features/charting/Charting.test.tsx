import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ChartingPage } from "./ChartingPage";
import * as api from "./api";
import { fetchPatientDetail } from "../admin/api";
import { useAuth } from "../auth/AuthProvider";
import { UserRole } from "../auth/types";
import { ToastProvider } from "../../components/Toast";
import { NoteStatus, NoteType } from "../workflow/types";
import type { ChartNote } from "./types";

vi.mock("./api", () => ({
  fetchChartNote: vi.fn(),
  saveChartNote: vi.fn(),
  fetchCompliance: vi.fn(),
  fetchPullForward: vi.fn(),
  fetchInterventions: vi.fn(),
  fetchInterventionSummary: vi.fn(),
  addIntervention: vi.fn(),
  updateIntervention: vi.fn(),
  deleteIntervention: vi.fn(),
  fetchGoals: vi.fn(),
  updateGoalProgress: vi.fn(),
  fetchOutcomes: vi.fn(),
  recordOutcome: vi.fn(),
  signChartNote: vi.fn(),
  fetchNoteRecord: vi.fn(),
  fetchNoteVersions: vi.fn(),
  cosignChartNote: vi.fn(),
  addAddendum: vi.fn(),
  amendNote: vi.fn(),
  lockNote: vi.fn(),
}));
vi.mock("../admin/api", () => ({ fetchPatientDetail: vi.fn() }));
vi.mock("../auth/AuthProvider", () => ({ useAuth: vi.fn() }));

const draft: ChartNote = {
  id: "n1",
  patientId: "p1",
  therapistId: "u1",
  appointmentId: "a1",
  noteType: NoteType.Daily,
  status: NoteStatus.Draft,
  serviceDate: "2026-10-07",
  subjective: "",
  objective: "",
  interventions: "",
  assessment: "",
  plan: "",
  planOfCareStart: null,
  planOfCareEnd: null,
  frequencyPerWeek: null,
  durationWeeks: null,
  signatureName: null,
  signedAt: null,
  cosignRequired: false,
  subjectiveDetailsJson: "{}",
  objectiveMeasurementsJson: "{}",
};

function setup(
  note: ChartNote = draft,
  role: UserRole = UserRole.Therapist,
  lastExam: string | null = null,
) {
  vi.mocked(useAuth).mockReturnValue({
    user: {
      id: "u1",
      username: "t",
      email: null,
      role,
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
  vi.mocked(api.fetchChartNote).mockResolvedValue(note);
  vi.mocked(api.saveChartNote).mockResolvedValue(note);
  vi.mocked(api.fetchCompliance).mockResolvedValue([
    {
      code: "missing_objective",
      severity: "high",
      title: "Objective findings are missing",
      detail: "",
      finalizationBlocker: true,
    },
  ]);
  vi.mocked(api.fetchPullForward).mockResolvedValue({
    activeGoals: [],
    lastObjectiveMeasurementsJson: lastExam,
    activeDiagnoses: [],
  });
  vi.mocked(api.fetchInterventions).mockResolvedValue([]);
  vi.mocked(api.fetchInterventionSummary).mockResolvedValue({
    timedMinutes: 0,
    untimedCount: 0,
    estimatedTimedUnits: 0,
    ruleVariant: "Medicare",
  });
  vi.mocked(api.fetchGoals).mockResolvedValue([]);
  vi.mocked(api.fetchOutcomes).mockResolvedValue([]);
  vi.mocked(api.fetchNoteRecord).mockResolvedValue({
    actions: {
      canEdit: true,
      canSign: true,
      canCosign: false,
      canAddAddendum: false,
      canAmend: false,
      canLock: false,
    },
    authorName: "Jamie Chen",
    cosignedByName: null,
    addenda: [],
    amendmentNoteId: null,
    amendmentStatus: null,
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
    precautions: "No lifting over 10 lb",
    assignedTherapistId: null,
    primaryLocationId: null,
    primaryCareProviderId: null,
    referringProviderId: null,
    status: 0,
  });
  render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <ToastProvider>
        <MemoryRouter initialEntries={["/chart/n1"]}>
          <Routes>
            <Route path="/chart/:noteId" element={<ChartingPage />} />
          </Routes>
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

describe("ChartingPage", () => {
  beforeEach(() => vi.clearAllMocks());

  it("shows the patient, precautions and every charting section", async () => {
    setup();
    expect(
      await screen.findByRole("heading", {
        name: /Daily note — Quinn Alvarez/,
      }),
    ).toBeInTheDocument();
    expect(screen.getByText(/No lifting over 10 lb/)).toBeInTheDocument();
    for (const s of [
      "Subjective",
      "Examination",
      "Outcome measures",
      "Interventions & patient response",
      "Goals & progress",
      "Assessment & plan",
      "Sign",
    ]) {
      expect(
        screen.getByRole("heading", { name: s, level: 2 }),
      ).toBeInTheDocument();
    }
  });

  it("autosaves structured findings and narrative to the same note", async () => {
    setup();
    await screen.findByRole("heading", { name: /Quinn Alvarez/ });
    await userEvent.click(
      within(screen.getByRole("group", { name: "Pain now" })).getByRole(
        "button",
        { name: "6" },
      ),
    );
    await userEvent.click(
      screen.getByRole("button", { name: /Add Knee motions/ }),
    );
    await userEvent.type(
      screen.getByLabelText("Knee Flexion AROM degrees"),
      "110",
    );

    await waitFor(() => expect(api.saveChartNote).toHaveBeenCalled(), {
      timeout: 4000,
    });
    const [noteId, body] = vi.mocked(api.saveChartNote).mock.calls.at(-1)!;
    expect(noteId).toBe("n1");
    expect(JSON.parse(body.subjectiveDetailsJson).painNow).toBe(6);
    const rom = JSON.parse(body.objectiveMeasurementsJson).rom;
    expect(
      rom.find((r: { motion: string }) => r.motion === "Flexion"),
    ).toMatchObject({ joint: "Knee", side: "R", arom: "110" });
  });

  it("shows last visit's value beside the same measurement", async () => {
    setup(
      draft,
      UserRole.Therapist,
      JSON.stringify({
        rom: [
          {
            id: "x",
            joint: "Knee",
            motion: "Flexion",
            side: "R",
            arom: "95",
            prom: "100",
          },
        ],
      }),
    );
    await screen.findByRole("heading", { name: /Quinn Alvarez/ });
    await userEvent.click(
      screen.getByRole("button", { name: /Add Knee motions/ }),
    );
    const row = screen
      .getByLabelText("Knee Flexion AROM degrees")
      .closest("tr")!;
    expect(within(row).getByText("95° / 100°")).toBeInTheDocument();
  });

  it("lists what still blocks signing, and needs the attestation and password", async () => {
    setup();
    expect(
      await screen.findByText(/Objective findings are missing/),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Sign note" })).toBeDisabled();
  });

  it("signs with the password once nothing blocks it", async () => {
    setup();
    vi.mocked(api.fetchCompliance).mockResolvedValue([]);
    vi.mocked(api.signChartNote).mockResolvedValue({
      ...draft,
      status: NoteStatus.Signed,
    });
    await screen.findByRole("heading", { name: /Quinn Alvarez/ });
    await act(async () => {}); // let compliance resolve
    await userEvent.click(screen.getByRole("checkbox", { name: /I attest/ }));
    await userEvent.type(
      screen.getByLabelText(/Your password/),
      "Correct!Pass1",
    );
    const sign = screen.getByRole("button", { name: "Sign note" });
    await waitFor(() => expect(sign).toBeEnabled());
    await userEvent.click(sign);
    await waitFor(() =>
      expect(api.signChartNote).toHaveBeenCalledWith("n1", "Correct!Pass1"),
    );
  });

  it("opens a signed note read-only", async () => {
    setup({
      ...draft,
      status: NoteStatus.Signed,
      signatureName: "Jamie Chen",
      objective: "ROM improved",
    });
    expect(await screen.findByText(/Signed by Jamie Chen/)).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Sign note" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: /Add Knee motions/ }),
    ).not.toBeInTheDocument();
  });
});
