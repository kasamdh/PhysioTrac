import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { LoginPage } from "./LoginPage";
import { useAuth } from "../features/auth/AuthProvider";

vi.mock("../features/auth/AuthProvider", () => ({
  useAuth: vi.fn(),
}));

const mockedUseAuth = vi.mocked(useAuth);

function renderLoginPage() {
  return render(
    <MemoryRouter>
      <LoginPage />
    </MemoryRouter>,
  );
}

describe("LoginPage", () => {
  beforeEach(() => {
    mockedUseAuth.mockReturnValue({
      user: null,
      isLoading: false,
      loginError: null,
      isLoggingIn: false,
      signIn: vi.fn(),
      signOut: vi.fn(),
    });
  });

  it("shows validation errors when submitted empty, without calling signIn", async () => {
    const signIn = vi.fn();
    mockedUseAuth.mockReturnValue({
      user: null,
      isLoading: false,
      loginError: null,
      isLoggingIn: false,
      signIn,
      signOut: vi.fn(),
    });

    renderLoginPage();
    await userEvent.click(screen.getByRole("button", { name: /^login$/i }));

    expect(await screen.findByText(/login user id is required/i)).toBeInTheDocument();
    expect(await screen.findByText(/login password is required/i)).toBeInTheDocument();
    expect(signIn).not.toHaveBeenCalled();
  });

  it("calls signIn with the entered credentials on valid submit", async () => {
    const signIn = vi.fn().mockResolvedValue(undefined);
    mockedUseAuth.mockReturnValue({
      user: null,
      isLoading: false,
      loginError: null,
      isLoggingIn: false,
      signIn,
      signOut: vi.fn(),
    });

    renderLoginPage();
    await userEvent.type(screen.getByLabelText(/login user id/i), "admin");
    await userEvent.type(screen.getByLabelText(/login password/i), "DemoPass123!");
    await userEvent.click(screen.getByRole("button", { name: /^login$/i }));

    await waitFor(() => expect(signIn).toHaveBeenCalledWith({ username: "admin", password: "DemoPass123!" }));
  });

  it("shows the server-reported error message when login fails", () => {
    mockedUseAuth.mockReturnValue({
      user: null,
      isLoading: false,
      loginError: "Invalid username or password.",
      isLoggingIn: false,
      signIn: vi.fn(),
      signOut: vi.fn(),
    });

    renderLoginPage();
    expect(screen.getByText("Invalid username or password.")).toBeInTheDocument();
  });

  it("toggles password visibility with the eye button", async () => {
    renderLoginPage();
    const input = screen.getByLabelText(/login password/i);
    expect(input).toHaveAttribute("type", "password");
    await userEvent.click(screen.getByRole("button", { name: /show password/i }));
    expect(input).toHaveAttribute("type", "text");
    await userEvent.click(screen.getByRole("button", { name: /hide password/i }));
    expect(input).toHaveAttribute("type", "password");
  });
});
