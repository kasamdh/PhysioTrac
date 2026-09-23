import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { ProtectedRoute } from "./ProtectedRoute";
import { useAuth } from "../features/auth/AuthProvider";
import type { CurrentUser } from "../features/auth/types";
import { UserRole } from "../features/auth/types";

vi.mock("../features/auth/AuthProvider", () => ({
  useAuth: vi.fn(),
}));

const mockedUseAuth = vi.mocked(useAuth);

const testUser: CurrentUser = {
  id: "1",
  username: "admin",
  email: "admin@sourcemotionpt.test",
  role: UserRole.Admin,
  organizationId: "org-1",
  isPlatformSuperAdmin: false,
  mustChangePassword: false,
};

function renderProtectedRoute() {
  return render(
    <MemoryRouter initialEntries={["/dashboard"]}>
      <Routes>
        <Route path="/login" element={<div>Login page</div>} />
        <Route element={<ProtectedRoute />}>
          <Route path="/dashboard" element={<div>Protected content</div>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

describe("ProtectedRoute", () => {
  it("shows a loading state while the session check is in flight", () => {
    mockedUseAuth.mockReturnValue({
      user: null,
      isLoading: true,
      loginError: null,
      isLoggingIn: false,
      signIn: vi.fn(),
      signOut: vi.fn(),
    });

    renderProtectedRoute();
    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });

  it("redirects to /login when there is no signed-in user", () => {
    mockedUseAuth.mockReturnValue({
      user: null,
      isLoading: false,
      loginError: null,
      isLoggingIn: false,
      signIn: vi.fn(),
      signOut: vi.fn(),
    });

    renderProtectedRoute();
    expect(screen.getByText("Login page")).toBeInTheDocument();
  });

  it("renders the protected content when a user is signed in", () => {
    mockedUseAuth.mockReturnValue({
      user: testUser,
      isLoading: false,
      loginError: null,
      isLoggingIn: false,
      signIn: vi.fn(),
      signOut: vi.fn(),
    });

    renderProtectedRoute();
    expect(screen.getByText("Protected content")).toBeInTheDocument();
  });
});
