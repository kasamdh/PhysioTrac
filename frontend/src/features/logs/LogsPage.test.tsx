import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { LogsPage } from "./LogsPage";
import { fetchAuditLogs } from "./api";
import { ToastProvider } from "../../components/Toast";
import type { AuditLogPage } from "./types";

vi.mock("./api", () => ({ fetchAuditLogs: vi.fn(), recordPageView: vi.fn() }));

const page: AuditLogPage = {
  items: [
    {
      id: "e1", at: "2026-10-06T14:05:00Z", userId: "u1", userName: "Alex Rivera", userLogin: "admin",
      action: "appointment.checked_in", category: "schedule", description: "Checked a patient in",
      objectType: "Appointment", objectId: "a1", patientId: "p1", patientName: "Quinn Alvarez", patientMrn: "SM-1",
      ipAddress: "10.0.0.5", metadataJson: "{}",
    },
    {
      id: "e2", at: "2026-10-06T14:00:00Z", userId: "u1", userName: "Alex Rivera", userLogin: "admin",
      action: "page.viewed", category: "pages", description: "Opened Workflow",
      objectType: "Page", objectId: null, patientId: null, patientName: null, patientMrn: null,
      ipAddress: "10.0.0.5", metadataJson: "{\"path\":\"/workflow\"}",
    },
  ],
  total: 2, page: 1, pageSize: 50, from: "2026-10-06", to: "2026-10-06", timezone: "UTC",
  users: [{ id: "u1", name: "Alex Rivera", userName: "admin" }],
};

function renderPage(path = "/admin/logs") {
  vi.mocked(fetchAuditLogs).mockResolvedValue(page);
  render(
    <QueryClientProvider client={new QueryClient()}>
      <ToastProvider>
        <MemoryRouter initialEntries={[path]}>
          <LogsPage />
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

describe("LogsPage", () => {
  beforeEach(() => vi.clearAllMocks());

  it("lists activity with user, category and patient, and opens the details", async () => {
    renderPage();
    const row = (await screen.findByRole("button", { name: "Checked a patient in" })).closest("tr")!;
    expect(within(row).getByText("Alex Rivera")).toBeInTheDocument();
    expect(within(row).getByText("Schedule")).toBeInTheDocument();
    expect(within(row).getByText("Quinn Alvarez")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Opened Workflow" })).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Checked a patient in" }));
    const dialog = screen.getByRole("dialog", { name: "Log Entry" });
    expect(within(dialog).getByText("appointment.checked_in")).toBeInTheDocument();
  });

  it("asks the server for the chosen user, category and date range", async () => {
    renderPage("/admin/logs?from=2026-10-01&to=2026-10-06&user=u1&category=schedule");
    await screen.findByRole("button", { name: "Checked a patient in" });
    expect(fetchAuditLogs).toHaveBeenCalledWith(
      expect.objectContaining({ from: "2026-10-01", to: "2026-10-06", userId: "u1", category: "schedule", page: 1 }),
      expect.anything(),
    );
  });
});
