import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AdminHomePage } from "./AdminHomePage";
import { useAuth } from "../../auth/AuthProvider";
import { UserRole, type CurrentUser } from "../../auth/types";

vi.mock("../../auth/AuthProvider", () => ({ useAuth: vi.fn() }));
const mockedUseAuth = vi.mocked(useAuth);

function renderAs(role: UserRole, accessControlEnabled = true) {
  const user: CurrentUser = {
    id: "1",
    username: "u",
    email: null,
    role,
    organizationId: "org",
    isPlatformSuperAdmin: false,
    mustChangePassword: false,
    accessControlEnabled,
  };
  mockedUseAuth.mockReturnValue({
    user,
    isLoading: false,
    loginError: null,
    isLoggingIn: false,
    signIn: vi.fn(),
    signOut: vi.fn(),
  });
  render(
    <MemoryRouter>
      <AdminHomePage />
    </MemoryRouter>,
  );
}

describe("AdminHomePage", () => {
  it("shows every command to an Admin", () => {
    renderAs(UserRole.Admin);
    for (const name of ["Change Password", "Messages", "Users", "Locations", "Patient List", "Logs"]) {
      expect(screen.getByRole("link", { name })).toBeInTheDocument();
    }
  });

  it("hides Users and Locations from a Therapist while access control is on", () => {
    renderAs(UserRole.Therapist);
    expect(screen.getByRole("link", { name: "Patient List" })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Users" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Locations" })).not.toBeInTheDocument();
  });

  it("shows a Scheduler everything while access control is switched off", () => {
    renderAs(UserRole.Scheduler, false);
    expect(screen.getByRole("link", { name: "Users" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Patient List" })).toBeInTheDocument();
  });

  it("shows Logs to Compliance but not to a Therapist", () => {
    renderAs(UserRole.Compliance);
    expect(screen.getByRole("link", { name: "Logs" })).toBeInTheDocument();
  });

  it("hides Logs from a Therapist while access control is on", () => {
    renderAs(UserRole.Therapist);
    expect(screen.queryByRole("link", { name: "Logs" })).not.toBeInTheDocument();
  });
});
