import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AppLayout } from "./AppLayout";
import { useAuth } from "../../features/auth/AuthProvider";
import { ToastProvider } from "../Toast";
import { UserRole } from "../../features/auth/types";
import type { CurrentUser } from "../../features/auth/types";
import { fetchCurrentOrganization } from "../../features/organizations/api";

vi.mock("../../features/auth/AuthProvider", () => ({
  useAuth: vi.fn(),
}));

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
  mockedFetchOrganization.mockResolvedValue({ id: "org-1", name: "Source Motion Physical Therapy", locations: [] });

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

    expect(screen.getByRole("link", { name: "Dashboard" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Patients" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Schedule" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Providers" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Billing" })).toBeInTheDocument();
  });

  it("hides Patients/Providers/Billing from a Scheduler, who isn't in RoleSets.Clinical or RoleSets.Billing", () => {
    renderAppLayout(UserRole.Scheduler);

    expect(screen.getByRole("link", { name: "Dashboard" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Schedule" })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Patients" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Providers" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Billing" })).not.toBeInTheDocument();
  });

  it("shows only Dashboard to a Patient-portal login", () => {
    renderAppLayout(UserRole.Patient);

    expect(screen.getByRole("link", { name: "Dashboard" })).toBeInTheDocument();
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
});
