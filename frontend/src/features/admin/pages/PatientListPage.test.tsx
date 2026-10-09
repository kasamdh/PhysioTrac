import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { PatientListPage } from "./PatientListPage";
import { useAuth } from "../../auth/AuthProvider";
import { fetchLocations, fetchPatientDirectory } from "../api";
import { UserRole } from "../../auth/types";
import { ToastProvider } from "../../../components/Toast";

vi.mock("../../auth/AuthProvider", () => ({ useAuth: vi.fn() }));
vi.mock("../api", () => ({
  fetchLocations: vi.fn(),
  fetchPatientDirectory: vi.fn(),
  deletePatient: vi.fn(),
  restorePatient: vi.fn(),
}));

function renderPage(manage: boolean) {
  vi.mocked(useAuth).mockReturnValue({
    user: { id: "1", username: "admin", email: null, role: UserRole.Admin, organizationId: "o", isPlatformSuperAdmin: false, mustChangePassword: false },
    isLoading: false,
    loginError: null,
    isLoggingIn: false,
    signIn: vi.fn(),
    signOut: vi.fn(),
  });
  vi.mocked(fetchLocations).mockResolvedValue([]);
  vi.mocked(fetchPatientDirectory).mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 25 });
  render(
    <QueryClientProvider client={new QueryClient()}>
      <ToastProvider>
        <MemoryRouter>
          <PatientListPage manage={manage} />
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

describe("PatientListPage", () => {
  it("is list-only on the Patients page: no Add patient, no Deleted filter", () => {
    renderPage(false);
    expect(screen.queryByRole("button", { name: "+ Add patient" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Deleted" })).not.toBeInTheDocument();
  });

  it("offers Add patient and the Deleted filter in Administration", () => {
    renderPage(true);
    expect(screen.getByRole("button", { name: "+ Add patient" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Deleted" })).toBeInTheDocument();
  });
});
