import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { EncounterPage } from "./EncounterPage";
import * as api from "./api";
import * as chartApi from "../charting/api";
import { ApiError } from "../../lib/apiClient";
import { useAuth } from "../auth/AuthProvider";
import { UserRole } from "../auth/types";
import { ToastProvider } from "../../components/Toast";
import { NoteStatus, NoteType } from "../workflow/types";
import { FieldType } from "../templates/types";
import type { Encounter } from "./types";

vi.mock("./api", () => ({
  fetchEncounter: vi.fn(),
  fetchEncounterStatus: vi.fn(),
  saveEncounter: vi.fn(),
  changeNoteTemplate: vi.fn(),
  fetchPlansOfCare: vi.fn().mockResolvedValue([]),
  fetchPatientDiagnoses: vi.fn().mockResolvedValue([]),
  searchDiagnosisCodes: vi.fn().mockResolvedValue([]),
  addPatientDiagnosis: vi.fn(),
  fetchAllergies: vi.fn().mockResolvedValue([]),
  addAllergy: vi.fn(),
  fetchMedications: vi.fn().mockResolvedValue([]),
  addMedication: vi.fn(),
  createGoal: vi.fn(),
  approveGoal: vi.fn(),
}));
vi.mock("../charting/api", () => ({
  fetchChartNote: vi.fn(),
  fetchCompliance: vi.fn(),
  fetchNoteRecord: vi.fn(),
  fetchPullForward: vi.fn(),
  signChartNote: vi.fn(),
  fetchGoals: vi.fn().mockResolvedValue([]),
  updateGoalProgress: vi.fn(),
  fetchInterventions: vi.fn().mockResolvedValue([]),
  fetchInterventionSummary: vi.fn(),
  fetchOutcomes: vi.fn().mockResolvedValue([]),
  fetchNoteVersions: vi.fn(),
}));
vi.mock("../templates/api", () => ({
  fetchTemplates: vi.fn().mockResolvedValue([]),
}));
vi.mock("../charting/ChartingPage", () => ({
  ChartingPage: () => <p>Original charting screen</p>,
}));
vi.mock("../auth/AuthProvider", () => ({ useAuth: vi.fn() }));

const encounter = (over: Partial<Encounter> = {}): Encounter => ({
  note: {
    id: "n1",
    patientId: "p1",
    therapistId: "u1",
    appointmentId: "a1",
    noteType: NoteType.Evaluation,
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
    templateVersionId: "v1",
  },
  templateName: "Initial Evaluation",
  template: {
    id: "v1",
    templateId: "t1",
    versionNumber: 3,
    changeSummary: null,
    createdAt: "2026-10-01T00:00:00Z",
    createdByName: null,
    sections: [
      {
        key: "subjective",
        title: "Subjective examination",
        fields: [
          {
            key: "chiefComplaint",
            label: "Primary complaint",
            fieldType: FieldType.ShortText,
            isRequired: true,
          },
          {
            key: "hpc",
            label: "History of present condition",
            fieldType: FieldType.LongText,
            isRequired: true,
            noteColumn: "subjective",
          },
          {
            key: "redFlags",
            label: "Red flags",
            fieldType: FieldType.Radio,
            isRequired: false,
            options: ["Negative", "Positive"],
          },
        ],
      },
      { key: "pain", title: "Pain", fields: [], component: "painAssessment" },
      {
        key: "bodyChart",
        title: "Body chart",
        fields: [],
        component: "bodyChart",
      },
      {
        key: "tests",
        title: "Special tests",
        fields: [],
        component: "specialTests",
      },
      { key: "goals", title: "Goals", fields: [], component: "goals" },
    ],
  },
  values: [],
  header: {
    patientId: "p1",
    patientName: "Avery Sample",
    dateOfBirth: "1980-04-09",
    age: 46,
    medicalRecordNumber: "SM-9",
    appointmentId: "a1",
    appointmentStartsAt: "2026-10-07T14:00:00Z",
    appointmentEndsAt: "2026-10-07T15:00:00Z",
    visitNumber: 1,
    visitType: "Initial Evaluation",
    authorName: "Jordan Example",
    treatingProviderName: "Jordan Example, PT",
    supervisingProviderName: null,
    referringProviderName: "Dr. Fictional",
    diagnoses: ["M25.561 Pain in right knee"],
    allergies: ["Latex (rash)"],
    precautions: "Avoid deep knee flexion",
    activePlanOfCareId: null,
    planOfCareStart: null,
    planOfCareEnd: null,
  },
  saveVersion: 4,
  lastSavedAt: "2026-10-07T14:05:00Z",
  lastSavedByName: "Jordan Example",
  ...over,
});

function setup(e: Encounter = encounter()) {
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
  vi.mocked(api.fetchEncounter).mockResolvedValue(e);
  vi.mocked(api.fetchEncounterStatus).mockResolvedValue({
    saveVersion: e.saveVersion,
    savedAt: e.lastSavedAt,
    savedByName: null,
    savedById: null,
    status: e.note.status,
  });
  vi.mocked(chartApi.fetchCompliance).mockResolvedValue([]);
  vi.mocked(chartApi.fetchNoteRecord).mockResolvedValue({
    actions: {
      canEdit: true,
      canSign: true,
      canCosign: false,
      canAddAddendum: false,
      canAmend: false,
      canLock: false,
    },
    authorName: "Jordan Example",
    cosignedByName: null,
    addenda: [],
    amendmentNoteId: null,
    amendmentStatus: null,
  });
  vi.mocked(chartApi.fetchPullForward).mockResolvedValue({
    activeGoals: [],
    lastObjectiveMeasurementsJson: null,
    activeDiagnoses: [],
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
            <Route path="/chart/:noteId" element={<EncounterPage />} />
          </Routes>
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

describe("Encounter workspace", () => {
  beforeEach(() => vi.clearAllMocks());

  it("shows the encounter header and the template's sections", async () => {
    setup();
    expect(
      await screen.findByRole("heading", { name: "Evaluation — Avery Sample" }),
    ).toBeInTheDocument();
    expect(screen.getByText("#1 · Initial Evaluation")).toBeInTheDocument();
    expect(screen.getByText(/Latex \(rash\)/)).toBeInTheDocument();
    expect(screen.getByText(/Avoid deep knee flexion/)).toBeInTheDocument();
    expect(screen.getByText("Initial Evaluation v3")).toBeInTheDocument();
    expect(
      screen.getByRole("heading", { name: /Subjective examination/ }),
    ).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: /^Goals/ })).toBeInTheDocument();
    // A section whose only content is a component not available yet is left out.
    expect(
      screen.queryByRole("heading", { name: /Special tests/ }),
    ).not.toBeInTheDocument();
  });

  it("autosaves only what changed, on the version it was loaded at, then the next version", async () => {
    vi.mocked(api.saveEncounter)
      .mockResolvedValueOnce({
        saveVersion: 5,
        savedAt: "2026-10-07T14:10:00Z",
        savedByName: "Jordan Example",
      })
      .mockResolvedValueOnce({
        saveVersion: 6,
        savedAt: "2026-10-07T14:11:00Z",
        savedByName: "Jordan Example",
      });
    setup();
    await userEvent.type(
      await screen.findByLabelText(/Primary complaint/),
      "Knee pain",
    );
    await userEvent.type(
      screen.getByLabelText(/History of present condition/),
      "Fictional fall",
    );

    await waitFor(() => expect(api.saveEncounter).toHaveBeenCalledTimes(1), {
      timeout: 4000,
    });
    const [, first] = vi.mocked(api.saveEncounter).mock.calls[0];
    expect(first.baseSaveVersion).toBe(4);
    expect(first.values).toEqual([
      { key: "chiefComplaint", text: "Knee pain" },
    ]);
    expect(first.subjective).toBe("Fictional fall");
    expect(await screen.findByText(/saved 2:10 PM|saved/)).toBeInTheDocument();

    await userEvent.click(
      within(screen.getByRole("radiogroup", { name: "Red flags" })).getByRole(
        "radio",
        { name: "Negative" },
      ),
    );
    await waitFor(() => expect(api.saveEncounter).toHaveBeenCalledTimes(2), {
      timeout: 4000,
    });
    const [, second] = vi.mocked(api.saveEncounter).mock.calls[1];
    expect(second).toEqual({
      baseSaveVersion: 5,
      values: [{ key: "redFlags", text: "Negative" }],
    });
  });

  it("stops and explains when someone else saved first, and reloads on request", async () => {
    vi.mocked(api.saveEncounter).mockRejectedValue(
      new ApiError("changed", 409, "EDIT_CONFLICT", {
        detail: "changed",
        code: "EDIT_CONFLICT",
        saveVersion: 5,
        savedAt: "2026-10-07T14:09:00Z",
        savedByName: "Sam Lee",
      }),
    );
    setup();
    await userEvent.type(
      await screen.findByLabelText(/Primary complaint/),
      "x",
    );
    expect(
      await screen.findByText(
        /This note was changed by Sam Lee/,
        {},
        { timeout: 4000 },
      ),
    ).toBeInTheDocument();
    await act(async () => {
      await new Promise((r) => setTimeout(r, 1500));
    });
    expect(api.saveEncounter).toHaveBeenCalledTimes(1); // no retry over the other edit
    await userEvent.click(
      screen.getByRole("button", { name: "Reload the latest version" }),
    );
    await waitFor(() => expect(api.fetchEncounter).toHaveBeenCalledTimes(2));
  });

  it("shows the server's reasons when a value is refused", async () => {
    vi.mocked(api.saveEncounter).mockRejectedValue(
      new ApiError("bad", 422, undefined, {
        errors: ["Primary complaint: 500 characters at most."],
      }),
    );
    setup();
    await userEvent.type(
      await screen.findByLabelText(/Primary complaint/),
      "x",
    );
    expect(
      await screen.findByText(
        "Primary complaint: 500 characters at most.",
        {},
        { timeout: 4000 },
      ),
    ).toBeInTheDocument();
  });

  it("keeps Sign disabled until the required fields are complete, then saves before signing", async () => {
    vi.mocked(api.saveEncounter).mockResolvedValue({
      saveVersion: 5,
      savedAt: "2026-10-07T14:10:00Z",
      savedByName: null,
    });
    vi.mocked(chartApi.signChartNote).mockResolvedValue({
      ...encounter().note,
      status: NoteStatus.Signed,
    });
    setup();
    expect(
      await screen.findByText(
        /Complete the required fields: Primary complaint, History of present condition/,
      ),
    ).toBeInTheDocument();

    await userEvent.type(
      screen.getByLabelText(/Primary complaint/),
      "Knee pain",
    );
    await userEvent.type(
      screen.getByLabelText(/History of present condition/),
      "Fictional fall",
    );
    await userEvent.click(screen.getByRole("checkbox", { name: /I attest/ }));
    await userEvent.type(screen.getByLabelText(/Your password/), "Secret!1");
    const sign = screen.getByRole("button", { name: "Sign note" });
    await waitFor(() => expect(sign).toBeEnabled());
    await userEvent.click(sign);
    await waitFor(() =>
      expect(chartApi.signChartNote).toHaveBeenCalledWith("n1", "Secret!1"),
    );
    expect(api.saveEncounter).toHaveBeenCalled();
    expect(
      screen.getByText(/creates the patient’s plan of care/),
    ).toBeInTheDocument();
  });

  it("opens a signed note read-only", async () => {
    setup(
      encounter({
        note: {
          ...encounter().note,
          status: NoteStatus.Signed,
          signatureName: "Jordan Example",
          signedAt: "2026-10-07T15:00:00Z",
        },
        values: [{ key: "chiefComplaint", text: "Knee pain" }],
      }),
    );
    expect(await screen.findByLabelText(/Primary complaint/)).toHaveAttribute(
      "readonly",
    );
    expect(
      screen.queryByRole("button", { name: "Sign note" }),
    ).not.toBeInTheDocument();
    expect(
      await screen.findByText(/Signed by Jordan Example/),
    ).toBeInTheDocument();
  });

  it("opens a note written before templates on the original charting screen", async () => {
    setup(encounter({ template: null, templateName: null }));
    expect(
      await screen.findByText("Original charting screen"),
    ).toBeInTheDocument();
  });

  it("collapses sections, shows what each still needs, and saves on Ctrl+S", async () => {
    vi.mocked(api.saveEncounter).mockResolvedValue({
      saveVersion: 5,
      savedAt: "2026-10-07T14:10:00Z",
      savedByName: null,
    });
    setup();
    const toggle = await screen.findByRole("button", {
      name: "Subjective examination 2 required left",
      expanded: true,
    });
    expect(toggle).toHaveAttribute("aria-expanded", "true");
    await userEvent.click(toggle);
    expect(toggle).toHaveAttribute("aria-expanded", "false");
    expect(screen.getByLabelText(/Primary complaint/)).not.toBeVisible();
    await userEvent.click(toggle);

    await userEvent.type(
      screen.getByLabelText(/Primary complaint/),
      "Knee pain",
    );
    await userEvent.type(
      screen.getByLabelText(/History of present condition/),
      "Fall",
    );
    expect(
      screen.getByRole("button", {
        name: "Subjective examination Complete",
        expanded: true,
      }),
    ).toBeInTheDocument();
    await userEvent.keyboard("{Control>}s{/Control}");
    expect(api.saveEncounter).toHaveBeenCalledTimes(1); // immediately, not after the autosave pause
  });

  it("warns when someone else saves the note while it is open", async () => {
    setup();
    // The note's latest save (7) is newer than the one this editor loaded (4).
    vi.mocked(api.fetchEncounterStatus).mockResolvedValue({
      saveVersion: 7,
      savedAt: "2026-10-07T14:20:00Z",
      savedByName: "Sam Lee",
      savedById: "u2",
      status: 0,
    });
    const banner = await screen.findByRole("status", {}, { timeout: 5000 });
    expect(banner).toHaveTextContent(/Sam Lee saved this note/);
    expect(
      within(banner).getByRole("button", { name: "Reload the latest version" }),
    ).toBeInTheDocument();
  });

  it("autosaves the structured pain assessment with the encounter", async () => {
    vi.mocked(api.saveEncounter).mockResolvedValue({
      saveVersion: 5,
      savedAt: "2026-10-07T14:10:00Z",
      savedByName: null,
    });
    setup();
    const current = await screen.findByRole("radiogroup", {
      name: /Current pain/,
    });
    await userEvent.click(within(current).getByRole("radio", { name: "5" }));
    await waitFor(() => expect(api.saveEncounter).toHaveBeenCalled(), {
      timeout: 4000,
    });
    const [, body] = vi.mocked(api.saveEncounter).mock.calls[0];
    expect(body.pain).toMatchObject({ scale: 0, current: 5 });
    expect(body.bodyChart).toBeUndefined(); // unchanged parts aren't sent
  });
});
