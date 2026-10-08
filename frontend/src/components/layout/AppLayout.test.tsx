import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AppLayout } from "./AppLayout";
import { useAuth } from "../../features/auth/AuthProvider";
import { ToastProvider } from "../Toast";
import { UserRole } from "../../features/auth/types";
import type { CurrentUser } from "../../features/auth/types";
import { fetchCurrentOrganization } from "../../features/organizations/api";
import { recordPageView } from "../../features/logs/api";

vi.mock("../../features/auth/AuthProvider", () => ({
  useAuth: vi.fn(),
}));

vi.mock("../../features/logs/api", () => ({ recordPageView: vi.fn().mockResolvedValue(undefined) }));

vi.mock("../../features/organizations/api", () => ({
  fetchCurrentOrganization: vi.fn(),
}));

const mockedUseAuth = vi.mocked(useAuth);
const mockedFetchOrganization = vi.mocked(fetchCurrentOrganization);

function userWithRole(role: UserRole): CurrentUser {
  return {
    id: "1",
    username: "test-user",
    email: null,
    role,
    organizationId: "org-1",
    isPlatformSuperAdmin: false,
    mustChangePassword: false,
  };
}

function renderAppLayout(role: UserRole) {
  mockedUseAuth.mockReturnValue({
    user: userWithRole(role),
    isLoading: false,
    loginError: null,
    isLoggingIn: false,
    signIn: vi.fn(),
    signOut: vi.fn(),
  });
  mockedFetchOrganization.mockResolvedValue({ id: "org-1", name: "Source Motion Physical Therapy", locations: [], timezone: "America/New_York" });

  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <MemoryRouter initialEntries={["/"]}>
          <Routes>
            <Route element={<AppLayout />}>
              <Route path="/" element={<div>Dashboard content</div>} />
            </Route>
          </Routes>
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

describe("AppLayout role-based navigation", () => {
  it("shows every nav item to an Admin", () => {
    renderAppLayout(UserRole.Admin);

    expect(screen.getByRole("link", { name: "Home" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Patients" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Schedule" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Providers" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Billing" })).toBeInTheDocument();
  });

  it("hides Patients/Providers/Billing from a Scheduler, who isn't in RoleSets.Clinical or RoleSets.Billing", () => {
    renderAppLayout(UserRole.Scheduler);

    expect(screen.getByRole("link", { name: "Home" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Schedule" })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Patients" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Providers" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Billing" })).not.toBeInTheDocument();
  });

  it("shows only Home to a Patient-portal login", () => {
    renderAppLayout(UserRole.Patient);

    expect(screen.getByRole("link", { name: "Home" })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Patients" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Schedule" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Providers" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Billing" })).not.toBeInTheDocument();
  });

  it("shows Billing to a Biller, who is in RoleSets.Billing but not RoleSets.Clinical/Scheduling", () => {
    renderAppLayout(UserRole.Biller);

    expect(screen.getByRole("link", { name: "Billing" })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Patients" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Schedule" })).not.toBeInTheDocument();
  });

  it("welcomes the user by name with their last login", () => {
    mockedUseAuth.mockReturnValue({
      user: { ...userWithRole(UserRole.Admin), firstName: "Kasam", lastName: "Dhakal", lastLoginAt: "2026-09-05T18:52:00Z" },
      isLoading: false,
      loginError: null,
      isLoggingIn: false,
      signIn: vi.fn(),
      signOut: vi.fn(),
    });
    render(
      <QueryClientProvider client={new QueryClient()}>
        <ToastProvider>
          <MemoryRouter initialEntries={["/"]}>
            <Routes>
              <Route element={<AppLayout />}>
                <Route path="/" element={<div />} />
              </Route>
            </Routes>
          </MemoryRouter>
        </ToastProvider>
      </QueryClientProvider>,
    );

    expect(screen.getAllByText(/Welcome Kasam Dhakal, last login at/)[0]).toBeInTheDocument();
  });

  it("opens help for the current page from the ? button", async () => {
    renderAppLayout(UserRole.Admin);
    await userEvent.click(screen.getByRole("button", { name: "Help for this page" }));
    expect(screen.getByRole("dialog", { name: "Home Help" })).toBeInTheDocument();
  });

  it("records the screen opened (path only) once", () => {
    vi.mocked(recordPageView).mockClear();
    renderAppLayout(UserRole.Admin);
    expect(recordPageView).toHaveBeenCalledTimes(1);
    expect(recordPageView).toHaveBeenCalledWith("/");
  });
});
