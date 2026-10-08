import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { DocumentationSettingsPage } from "./DocumentationSettingsPage";
import { useAuth } from "../../auth/AuthProvider";
import { UserRole, type CurrentUser } from "../../auth/types";
import { apiRequest } from "../../../lib/apiClient";

vi.mock("../../auth/AuthProvider", () => ({ useAuth: vi.fn() }));
vi.mock("../../../lib/apiClient", async (orig) => ({
  ...(await orig<typeof import("../../../lib/apiClient")>()),
  apiRequest: vi.fn(),
}));

const current = {
  ptaCosignRequired: true,
  progressNoteDueVisitCount: 10,
  progressNoteDueDays: null,
  autoCreatePendingCharges: true,
};

function renderAs(role: UserRole) {
  const user: CurrentUser = {
    id: "1",
    username: "u",
    email: null,
    role,
    organizationId: "org",
    isPlatformSuperAdmin: false,
    mustChangePassword: false,
    accessControlEnabled: true,
  };
  vi.mocked(useAuth).mockReturnValue({
    user,
    isLoading: false,
    loginError: null,
    isLoggingIn: false,
    signIn: vi.fn(),
    signOut: vi.fn(),
  });
  vi.mocked(apiRequest).mockImplementation(async (_path, init) =>
    init?.method === "PUT" ? init.body : current,
  );
  render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <MemoryRouter>
        <DocumentationSettingsPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("DocumentationSettingsPage", () => {
  afterEach(() => vi.clearAllMocks());

  it("lets an administrator change the progress-note and charge rules", async () => {
    renderAs(UserRole.Admin);
    const visits = await screen.findByLabelText(
      "Treatment visits before a progress note is due",
    );
    expect(visits).toHaveValue("10");
    await userEvent.clear(visits);
    await userEvent.type(visits, "12");
    await userEvent.type(
      screen.getByLabelText("Days before a progress note is due"),
      "30",
    );
    await userEvent.click(
      screen.getByRole("checkbox", {
        name: /Create pending charges when a note is signed/,
      }),
    );
    await userEvent.click(screen.getByRole("button", { name: "Save settings" }));

    await waitFor(() =>
      expect(apiRequest).toHaveBeenCalledWith(
        "/api/v1/organizations/documentation-settings",
        {
          method: "PUT",
          body: {
            ptaCosignRequired: true,
            progressNoteDueVisitCount: 12,
            progressNoteDueDays: 30,
            autoCreatePendingCharges: false,
          },
        },
      ),
    );
    expect(await screen.findByRole("status")).toHaveTextContent(
      "Settings saved.",
    );
  });

  it("flags an out-of-range value and won't save it", async () => {
    renderAs(UserRole.Admin);
    const days = await screen.findByLabelText(
      "Days before a progress note is due",
    );
    await userEvent.type(days, "400");
    expect(days).toHaveAttribute("aria-invalid", "true");
    expect(screen.getByRole("button", { name: "Save settings" })).toBeDisabled();
  });

  it("is read-only for a therapist", async () => {
    renderAs(UserRole.Therapist);
    expect(
      await screen.findByText(
        "Only administrators and directors can change these settings.",
      ),
    ).toBeInTheDocument();
    expect(
      screen.getByLabelText("Treatment visits before a progress note is due"),
    ).toBeDisabled();
    expect(
      screen.queryByRole("button", { name: "Save settings" }),
    ).not.toBeInTheDocument();
  });
});
