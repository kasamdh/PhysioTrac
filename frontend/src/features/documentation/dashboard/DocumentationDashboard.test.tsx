import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useAuth } from "../../auth/AuthProvider";
import { UserRole } from "../../auth/types";
import { openAppointmentEncounter } from "../../encounter/api";
import { fetchScheduleSettings } from "../../schedule/api";
import { noteTag } from "../../schedule/status";
import * as api from "./api";
import { DocumentationDashboardPage } from "./DocumentationDashboardPage";

vi.mock("../../auth/AuthProvider", () => ({ useAuth: vi.fn() }));
vi.mock("../../encounter/api", () => ({ openAppointmentEncounter: vi.fn() }));
vi.mock("../../schedule/api", () => ({
  fetchScheduleSettings: vi.fn(),
  searchPatients: vi.fn().mockResolvedValue([]),
}));
vi.mock("./api", async (importOriginal) => ({
  ...(await importOriginal<typeof import("./api")>()),
  fetchDocumentationDashboard: vi.fn(),
}));

const item = (over: Partial<api.DashboardItem>): api.DashboardItem => ({
  patientId: "p1",
  patientName: "Taylor Sample",
  medicalRecordNumber: "SM-1",
  date: "2026-10-08",
  noteId: null,
  noteType: 1,
  noteStatus: null,
  documentationStatus: 0,
  appointmentId: "a1",
  startsAt: "2026-10-08T14:00:00Z",
  appointmentStatus: 0,
  authorName: null,
  providerName: "Jordan Lee, PT",
  daysSinceService: 0,
  overdue: false,
  ...over,
});

const dashboard: api.DocumentationDashboard = {
  today: "2026-10-08",
  from: null,
  to: "2026-10-08",
  counts: {
    todaysVisits: 1,
    notStarted: 1,
    drafts: 0,
    readyToSign: 1,
    awaitingCosign: 0,
    returned: 0,
    overdue: 1,
    progressNotesDue: 1,
    reevaluationsDue: 0,
    expiringPlans: 1,
    recentlySigned: 0,
  },
  todaysSchedule: [item({})],
  notStarted: [
    item({
      appointmentId: "a2",
      date: "2026-10-05",
      overdue: true,
      daysSinceService: 3,
    }),
  ],
  drafts: [],
  readyToSign: [
    item({
      noteId: "n1",
      appointmentId: null,
      documentationStatus: 4,
      authorName: "Jordan Lee",
    }),
  ],
  awaitingCosign: [],
  returned: [],
  overdue: [
    item({
      appointmentId: "a2",
      date: "2026-10-05",
      overdue: true,
      daysSinceService: 3,
    }),
  ],
  progressNotesDue: [
    {
      patientId: "p1",
      patientName: "Taylor Sample",
      medicalRecordNumber: "SM-1",
      noteType: 4,
      dueDate: null,
      overdue: false,
      detail: "9 visits since the last evaluation — due at visit 10",
    },
  ],
  reevaluationsDue: [],
  expiringPlans: [
    {
      patientId: "p1",
      patientName: "Taylor Sample",
      medicalRecordNumber: "SM-1",
      noteType: 11,
      dueDate: "2026-10-18",
      overdue: false,
      detail: "Certification ends in 10 days",
    },
  ],
  recentlySigned: [],
};

function renderPage() {
  render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <MemoryRouter initialEntries={["/documentation"]}>
        <Routes>
          <Route
            path="/documentation"
            element={<DocumentationDashboardPage />}
          />
          <Route path="/chart/:id" element={<p>Chart page</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("Documentation dashboard", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useAuth).mockReturnValue({
      user: { id: "u1", role: UserRole.Therapist },
    } as ReturnType<typeof useAuth>);
    vi.mocked(fetchScheduleSettings).mockResolvedValue({
      providers: [
        {
          id: "prov1",
          name: "Jordan Lee",
          credentials: "PT",
          isActive: true,
          userId: "u1",
        },
        {
          id: "prov2",
          name: "Casey Other",
          credentials: null,
          isActive: true,
          userId: "u2",
        },
      ],
    } as never);
    vi.mocked(api.fetchDocumentationDashboard).mockResolvedValue(dashboard);
  });

  it("starts on the clinician's own patients and filters", async () => {
    renderPage();
    await waitFor(() =>
      expect(api.fetchDocumentationDashboard).toHaveBeenCalledWith(
        expect.objectContaining({ providerId: "prov1" }),
      ),
    );
    await userEvent.selectOptions(screen.getByLabelText("Provider"), "");
    await waitFor(() =>
      expect(api.fetchDocumentationDashboard).toHaveBeenLastCalledWith(
        expect.objectContaining({ providerId: "" }),
      ),
    );
    await userEvent.selectOptions(screen.getByLabelText("Status"), "4");
    await userEvent.selectOptions(screen.getByLabelText("Note type"), "4");
    await waitFor(() =>
      expect(api.fetchDocumentationDashboard).toHaveBeenLastCalledWith(
        expect.objectContaining({ status: "4", noteType: "4" }),
      ),
    );
    await userEvent.click(
      screen.getByRole("button", { name: "Clear filters" }),
    );
    await waitFor(() =>
      expect(api.fetchDocumentationDashboard).toHaveBeenLastCalledWith(
        expect.objectContaining({
          providerId: "prov1",
          status: "",
          noteType: "",
        }),
      ),
    );
  });

  it("summarizes every list and flags overdue work", async () => {
    renderPage();
    const summary = await screen.findByRole("navigation", { name: "Summary" });
    const overdue = within(summary).getByRole("link", { name: /1\s*Overdue/ });
    expect(overdue).toHaveAttribute("href", "#dash-overdue");
    expect(overdue.className).toContain("border-danger");
    expect(
      within(summary).getByRole("link", { name: /1\s*Ready to sign/ }),
    ).toBeInTheDocument();

    const ready = screen.getByRole("region", { name: /Ready to sign/ });
    expect(
      within(ready).getByRole("link", { name: "Open note" }),
    ).toHaveAttribute("href", "/chart/n1");
    const overdueList = screen.getByRole("region", { name: /^Overdue/ });
    expect(overdueList).toHaveTextContent("Overdue · 3 days");
    expect(
      screen.getByRole("region", { name: /Progress notes due/ }),
    ).toHaveTextContent("9 visits since the last evaluation — due at visit 10");
    expect(
      screen.getByRole("region", { name: /Re-evaluations due/ }),
    ).toHaveTextContent("No re-evaluations are due.");
  });

  it("starts the note for an undocumented visit", async () => {
    vi.mocked(openAppointmentEncounter).mockResolvedValue({
      noteId: "n9",
      created: true,
    });
    renderPage();
    const notStarted = await screen.findByRole("region", {
      name: /Notes not started/,
    });
    await userEvent.click(
      within(notStarted).getByRole("button", { name: "Start note" }),
    );
    expect(await screen.findByText("Chart page")).toBeInTheDocument();
    expect(openAppointmentEncounter).toHaveBeenCalledWith("a2", false);
  });
});

describe("Calendar note tag", () => {
  const now = Date.parse("2026-10-08T15:00:00Z");
  it("shows a visit's documentation status once there's something to say", () => {
    expect(
      noteTag(
        { documentationStatus: 0, startsAt: "2026-10-08T16:00:00Z" },
        now,
      ),
    ).toBeNull();
    expect(
      noteTag(
        { documentationStatus: 0, startsAt: "2026-10-08T14:00:00Z" },
        now,
      ),
    ).toBe("No note");
    expect(
      noteTag(
        { documentationStatus: 1, startsAt: "2026-10-09T14:00:00Z" },
        now,
      ),
    ).toBe("Note: draft");
    expect(
      noteTag(
        { documentationStatus: 6, startsAt: "2026-10-08T14:00:00Z" },
        now,
      ),
    ).toBe("Note: cosign");
    expect(
      noteTag(
        { documentationStatus: 7, startsAt: "2026-10-08T14:00:00Z" },
        now,
      ),
    ).toBe("Signed");
    expect(
      noteTag(
        { documentationStatus: null, startsAt: "2026-10-08T14:00:00Z" },
        now,
      ),
    ).toBeNull();
  });
});
