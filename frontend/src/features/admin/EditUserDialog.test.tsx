import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { EditUserDialog } from "./EditUserDialog";
import { updateUser } from "./api";
import { UserRole } from "../auth/types";
import { UserStatus, type StaffUser } from "./types";

vi.mock("./api", () => ({ updateUser: vi.fn() }));

const amanda: StaffUser = {
  id: "u1",
  userName: "AFuentez",
  email: "amanda@test",
  firstName: "Amanda",
  lastName: "Fuentez",
  role: UserRole.Therapist,
  status: UserStatus.Active,
  mustChangePassword: false,
};
const roles = [UserRole.Admin, UserRole.Therapist, UserRole.Scheduler];

function renderDialog(props: Partial<Parameters<typeof EditUserDialog>[0]> = {}) {
  const onSaved = vi.fn();
  render(
    <QueryClientProvider client={new QueryClient()}>
      <EditUserDialog user={amanda} isSelf={false} assignableRoles={roles} onClose={vi.fn()} onSaved={onSaved} {...props} />
    </QueryClientProvider>,
  );
  return onSaved;
}

describe("EditUserDialog", () => {
  beforeEach(() => vi.clearAllMocks());

  it("keeps Save and Close disabled until something changes", async () => {
    renderDialog();
    const save = screen.getByRole("button", { name: "Save and Close" });
    expect(save).toBeDisabled();
    await userEvent.type(screen.getByLabelText("Last name"), "-Ruiz");
    expect(save).toBeEnabled();
  });

  it("saves the edited fields, new status and access level", async () => {
    vi.mocked(updateUser).mockResolvedValue({ ...amanda, status: UserStatus.Suspended });
    const onSaved = renderDialog();

    await userEvent.clear(screen.getByLabelText("User ID"));
    await userEvent.type(screen.getByLabelText("User ID"), "AFuentez2");
    await userEvent.click(screen.getByRole("button", { name: "Suspended" }));
    await userEvent.selectOptions(screen.getByLabelText("Access Level"), String(UserRole.Scheduler));
    expect(screen.getByText(/signed out everywhere when you save/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Save and Close" }));

    await waitFor(() =>
      expect(updateUser).toHaveBeenCalledWith("u1", {
        userName: "AFuentez2",
        firstName: "Amanda",
        lastName: "Fuentez",
        email: "amanda@test",
        role: UserRole.Scheduler,
        status: UserStatus.Suspended,
        newPassword: null,
      }),
    );
    expect(onSaved).toHaveBeenCalledWith(expect.objectContaining({ id: "u1" }), false);
  });

  it("blocks a new password that breaks the rules, and sends a valid one", async () => {
    vi.mocked(updateUser).mockResolvedValue(amanda);
    const onSaved = renderDialog();
    const save = screen.getByRole("button", { name: "Save and Close" });

    await userEvent.type(screen.getByLabelText("New Password"), "short");
    expect(screen.getByText(/Needs at least 10 characters/)).toBeInTheDocument();
    expect(save).toBeDisabled();

    await userEvent.clear(screen.getByLabelText("New Password"));
    await userEvent.type(screen.getByLabelText("New Password"), "Brand!New2026");
    await userEvent.click(save);

    await waitFor(() => expect(updateUser).toHaveBeenCalledWith("u1", expect.objectContaining({ newPassword: "Brand!New2026" })));
    expect(onSaved).toHaveBeenCalledWith(expect.anything(), true);
  });

  it("locks status and access level when editing your own account", () => {
    renderDialog({ isSelf: true });
    expect(screen.queryByRole("button", { name: "Suspended" })).not.toBeInTheDocument();
    expect(screen.getByText("You can't change your own status.")).toBeInTheDocument();
    expect(screen.getByLabelText("Access Level")).toBeDisabled();
  });
});
