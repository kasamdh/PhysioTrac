import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { WorkflowPage } from "./WorkflowPage";
import { createVisitNote, fetchWorkflowToday } from "./api";
import { openAppointmentEncounter } from "../encounter/api";
import { transitionAppointment } from "../schedule/api";
import { useAuth } from "../auth/AuthProvider";
import { UserRole } from "../auth/types";
import { AppointmentKind, AppointmentStatus } from "../schedule/types";
import { ToastProvider } from "../../components/Toast";
import {
  NoteStatus,
  NoteType,
  type WorkflowAppointment,
  type WorkflowDay,
} from "./types";

vi.mock("./api", () => ({
  fetchWorkflowToday: vi.fn(),
  createVisitNote: vi.fn(),
}));
const navigateSpy = vi.fn();
vi.mock("react-router-dom", async (orig) => ({
  ...(await orig<typeof import("react-router-dom")>()),
  useNavigate: () => navigateSpy,
}));
vi.mock("../schedule/api", () => ({ transitionAppointment: vi.fn() }));
vi.mock("../auth/AuthProvider", () => ({ useAuth: vi.fn() }));
vi.mock("../encounter/api", () => ({ openAppointmentEncounter: vi.fn() }));
vi.mock("../charting/api", () => ({
  fetchNoteQueues: vi
    .fn()
    .mockResolvedValue({ myUnsignedNotes: [], awaitingMyCosign: [] }),
}));

const appt = (over: Partial<WorkflowAppointment>): WorkflowAppointment => ({
  appointmentId: "a1",
  startsAt: "2026-10-06T13:00:00Z",
  endsAt: "2026-10-06T13:30:00Z",
  status: AppointmentStatus.Scheduled,
  kind: AppointmentKind.FollowUp,
  appointmentTypeName: "Follow-up Visit",
  appointmentTypeColor: null,
  patientId: "p1",
  patientName: "Quinn Alvarez",
  medicalRecordNumber: "SM-1",
  providerId: "prov-1",
  providerName: "Jamie Chen",
  locationName: "Raleigh",
  suggestedNoteType: NoteType.Daily,
  noteId: null,
  noteStatus: null,
  noteType: null,
  ...over,
});

const day: WorkflowDay = {
  date: "2026-10-06",
  timezone: "America/New_York",
  providers: [
    {
      id: "prov-1",
      name: "Jamie Chen",
      credentials: "PT",
      discipline: 1,
      userId: "u1",
    },
  ],
  myProviderId: "prov-1",
  selectedProviderId: "prov-1",
  allProviders: false,
  ownDayOnly: false,
  appointments: [
    appt({}),
    appt({
      appointmentId: "a2",
      patientName: "Taylor Brooks",
      status: AppointmentStatus.CheckedIn,
    }),
    appt({
      appointmentId: "a3",
      patientName: "Mateo Cruz",
      status: AppointmentStatus.Completed,
      noteId: "n3",
      noteStatus: NoteStatus.Signed,
    }),
  ],
};

function wrap(ui: React.ReactNode, role: UserRole = UserRole.Therapist) {
  vi.mocked(useAuth).mockReturnValue({
    user: {
      id: "u1",
      username: "therapist",
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
  return render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <ToastProvider>
        <MemoryRouter>{ui}</MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

describe("WorkflowPage", () => {
  beforeEach(() => vi.clearAllMocks());

  it("lists today's appointments with status counts and note status", async () => {
    vi.mocked(fetchWorkflowToday).mockResolvedValue(day);
    wrap(<WorkflowPage />);
    expect(
      await screen.findByRole("button", { name: "Quinn Alvarez" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "All (3)" })).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "To check in (1)" }),
    ).toBeInTheDocument();
    const mateo = screen
      .getByRole("button", { name: "Mateo Cruz" })
      .closest("tr")!;
    expect(within(mateo).getByText("Signed")).toBeInTheDocument();

    await userEvent.click(
      screen.getByRole("button", { name: "Checked in (1)" }),
    );
    expect(
      screen.queryByRole("button", { name: "Quinn Alvarez" }),
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Taylor Brooks" }),
    ).toBeInTheDocument();
  });

  it("checks a patient in with one click", async () => {
    vi.mocked(fetchWorkflowToday).mockResolvedValue(day);
    vi.mocked(transitionAppointment).mockResolvedValue(undefined);
    wrap(<WorkflowPage />);
    const quinn = (
      await screen.findByRole("button", { name: "Quinn Alvarez" })
    ).closest("tr")!;
    await userEvent.click(
      within(quinn).getByRole("button", { name: "Check in" }),
    );
    await waitFor(() =>
      expect(transitionAppointment).toHaveBeenCalledWith("a1", "check-in"),
    );
  });
});

describe("WorkflowPage documenting", () => {
  beforeEach(() => vi.clearAllMocks());

  it("Document opens the visit's encounter (the server starts its note), then shows it", async () => {
    vi.mocked(fetchWorkflowToday).mockResolvedValue(day);
    vi.mocked(openAppointmentEncounter).mockResolvedValue({
      noteId: "new-note",
      created: true,
    });
    wrap(<WorkflowPage />);
    const quinn = (
      await screen.findByRole("button", { name: "Quinn Alvarez" })
    ).closest("tr")!;
    await userEvent.click(
      within(quinn).getByRole("button", { name: "Document" }),
    );

    await waitFor(() =>
      expect(navigateSpy).toHaveBeenCalledWith("/chart/new-note"),
    );
    expect(openAppointmentEncounter).toHaveBeenCalledWith("a1");
    expect(createVisitNote).not.toHaveBeenCalled();
  });

  it("opens an existing note without creating another", async () => {
    vi.mocked(fetchWorkflowToday).mockResolvedValue({
      ...day,
      appointments: [appt({ noteId: "n9", noteStatus: NoteStatus.Draft })],
    });
    wrap(<WorkflowPage />);
    await userEvent.click(
      await screen.findByRole("button", { name: "Continue note" }),
    );

    await waitFor(() => expect(navigateSpy).toHaveBeenCalledWith("/chart/n9"));
    expect(createVisitNote).not.toHaveBeenCalled();
  });
});
