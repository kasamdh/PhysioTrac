import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ChartingPage } from "./ChartingPage";
import { NotePrintPage } from "./NotePrintPage";
import * as api from "./api";
import { fetchPatientDetail } from "../admin/api";
import { fetchCurrentOrganization } from "../organizations/api";
import { useAuth } from "../auth/AuthProvider";
import { UserRole } from "../auth/types";
import { ToastProvider } from "../../components/Toast";
import { NoteStatus, NoteType } from "../workflow/types";
import type { ChartNote, NoteActions, NoteRecord } from "./types";

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
vi.mock("../organizations/api", () => ({ fetchCurrentOrganization: vi.fn() }));
vi.mock("../auth/AuthProvider", () => ({ useAuth: vi.fn() }));
const navigateSpy = vi.fn();
vi.mock("react-router-dom", async (orig) => ({
  ...(await orig<typeof import("react-router-dom")>()),
  useNavigate: () => navigateSpy,
}));

const signed: ChartNote = {
  id: "n1",
  patientId: "p1",
  therapistId: "u1",
  appointmentId: "a1",
  noteType: NoteType.Daily,
  status: NoteStatus.Signed,
  serviceDate: "2026-10-07",
  subjective: "Knee sore",
  objective: "Flexion 100",
  interventions: "",
  assessment: "Improving",
  plan: "Continue",
  planOfCareStart: null,
  planOfCareEnd: null,
  frequencyPerWeek: null,
  durationWeeks: null,
  signatureName: "Jamie Chen",
  signatureCredentials: "PT, DPT",
  signedAt: "2026-10-07T15:00:00Z",
  cosignRequired: false,
  subjectiveDetailsJson: "{}",
  objectiveMeasurementsJson: "{}",
};
const none: NoteActions = {
  canEdit: false,
  canSign: false,
  canCosign: false,
  canAddAddendum: false,
  canAmend: false,
  canLock: false,
};
const record = (over: Partial<NoteRecord> = {}): NoteRecord => ({
  actions: none,
  authorName: "Jamie Chen",
  cosignedByName: null,
  addenda: [],
  amendmentNoteId: null,
  amendmentStatus: null,
  ...over,
});

function setup(note: ChartNote, rec: NoteRecord, path = "/chart/n1") {
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
  vi.mocked(api.fetchChartNote).mockResolvedValue(note);
  vi.mocked(api.fetchNoteRecord).mockResolvedValue(rec);
  vi.mocked(api.fetchCompliance).mockResolvedValue([]);
  vi.mocked(api.fetchPullForward).mockResolvedValue({
    activeGoals: [],
    lastObjectiveMeasurementsJson: null,
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
  vi.mocked(fetchCurrentOrganization).mockResolvedValue({
    id: "o",
    name: "Source Motion Physical Therapy",
    locations: [],
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
  render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <ToastProvider>
        <MemoryRouter initialEntries={[path]}>
          <Routes>
            <Route path="/chart/:noteId" element={<ChartingPage />} />
            <Route path="/notes/:noteId/print" element={<NotePrintPage />} />
          </Routes>
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

describe("Signed note record", () => {
  beforeEach(() => vi.clearAllMocks());

  it("adds an addendum with a reason, leaving the note itself alone", async () => {
    setup(signed, record({ actions: { ...none, canAddAddendum: true } }));
    vi.mocked(api.addAddendum).mockResolvedValue({
      id: "ad1",
      noteId: "n1",
      authorId: "u1",
      reason: "Late entry",
      body: "Called patient.",
      createdAt: "2026-10-07T18:00:00Z",
      authorName: "Jamie Chen",
    });
    await userEvent.click(
      await screen.findByRole("button", { name: "+ Add addendum" }),
    );
    const form = screen.getByRole("form", { name: "Add addendum" });
    const save = within(form).getByRole("button", { name: "Save addendum" });
    expect(save).toBeDisabled();
    await userEvent.type(within(form).getByLabelText("Reason"), "Late entry");
    await userEvent.type(
      within(form).getByLabelText("Addendum"),
      "Called patient.",
    );
    await userEvent.click(save);
    await waitFor(() =>
      expect(api.addAddendum).toHaveBeenCalledWith(
        "n1",
        "Late entry",
        "Called patient.",
      ),
    );
  });

  it("starts an amendment with a reason and opens the amendment draft", async () => {
    setup(signed, record({ actions: { ...none, canAmend: true } }));
    vi.mocked(api.amendNote).mockResolvedValue({
      ...signed,
      id: "n2",
      status: NoteStatus.Draft,
      amendsNoteId: "n1",
    });
    await userEvent.click(
      await screen.findByRole("button", { name: "Amend note" }),
    );
    await userEvent.type(
      screen.getByLabelText("Reason for amendment"),
      "Wrong flexion value",
    );
    await userEvent.click(
      screen.getByRole("button", { name: "Start amendment" }),
    );
    await waitFor(() =>
      expect(api.amendNote).toHaveBeenCalledWith("n1", "Wrong flexion value"),
    );
    await waitFor(() => expect(navigateSpy).toHaveBeenCalledWith("/chart/n2"));
  });

  it("only offers what the service allows", async () => {
    setup(signed, record());
    expect(
      await screen.findByRole("link", { name: "Print / PDF" }),
    ).toHaveAttribute("href", "/notes/n1/print");
    for (const name of ["+ Add addendum", "Amend note", "Lock note"]) {
      expect(screen.queryByRole("button", { name })).not.toBeInTheDocument();
    }
  });

  it("locks only after a confirmation", async () => {
    setup(signed, record({ actions: { ...none, canLock: true } }));
    vi.mocked(api.lockNote).mockResolvedValue({
      ...signed,
      status: NoteStatus.Locked,
    });
    await userEvent.click(
      await screen.findByRole("button", { name: "Lock note" }),
    );
    expect(api.lockNote).not.toHaveBeenCalled();
    await userEvent.click(
      screen.getByRole("button", { name: "Yes, lock this note" }),
    );
    await waitFor(() => expect(api.lockNote).toHaveBeenCalledWith("n1"));
  });

  it("cosigns a PTA's note with the password", async () => {
    setup(
      { ...signed, status: NoteStatus.ReviewRequired, cosignRequired: true },
      record({ actions: { ...none, canCosign: true } }),
    );
    vi.mocked(api.cosignChartNote).mockResolvedValue({ ...signed });
    const cosign = await screen.findByRole("button", { name: "Cosign note" });
    expect(cosign).toBeDisabled();
    await userEvent.type(screen.getByLabelText(/Your password/), "Secret!1");
    await userEvent.click(cosign);
    await waitFor(() =>
      expect(api.cosignChartNote).toHaveBeenCalledWith("n1", "Secret!1"),
    );
  });

  it("marks an amended note and links to its amendment", async () => {
    setup(
      { ...signed, status: NoteStatus.Amended },
      record({ amendmentNoteId: "n2", amendmentStatus: NoteStatus.Signed }),
    );
    expect(
      await screen.findByRole("link", { name: "Open the amendment" }),
    ).toHaveAttribute("href", "/chart/n2");
  });

  it("shows an amendment's reason and links back to the original", async () => {
    setup(
      {
        ...signed,
        id: "n2",
        status: NoteStatus.Draft,
        amendsNoteId: "n1",
        amendmentReason: "Wrong flexion value",
      },
      record({ actions: { ...none, canEdit: true, canSign: true } }),
      "/chart/n2",
    );
    expect(
      await screen.findByRole("heading", { name: /Daily note \(amendment\)/ }),
    ).toBeInTheDocument();
    expect(screen.getByText(/Wrong flexion value/)).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: "View the original note" }),
    ).toHaveAttribute("href", "/chart/n1");
  });

  it("prints the note with letterhead, addenda and signature block", async () => {
    const print = vi.spyOn(window, "print").mockImplementation(() => {});
    setup(
      signed,
      record({
        addenda: [
          {
            id: "ad1",
            noteId: "n1",
            authorId: "u1",
            reason: "Late entry",
            body: "Called patient.",
            createdAt: "2026-10-07T18:00:00Z",
            authorName: "Jamie Chen",
          },
        ],
      }),
      "/notes/n1/print",
    );
    expect(
      await screen.findByText("Source Motion Physical Therapy"),
    ).toBeInTheDocument();
    expect(screen.getByText("Quinn Alvarez")).toBeInTheDocument();
    expect(screen.getByText("Knee sore")).toBeInTheDocument();
    expect(screen.getByText("Called patient.")).toBeInTheDocument();
    expect(screen.getByText(/Electronically signed by/)).toHaveTextContent(
      "Jamie Chen, PT, DPT",
    );
    await waitFor(() => expect(print).toHaveBeenCalled(), { timeout: 3000 });
    print.mockRestore();
  });
});
