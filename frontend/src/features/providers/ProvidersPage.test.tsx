import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ProvidersPage } from "./ProvidersPage";
import {
  createProvider,
  deleteProvider,
  fetchLinkableUsers,
  fetchProviders,
  setProviderActive,
  updateProvider,
} from "./api";
import { fetchLocations } from "../admin/api";
import { useAuth } from "../auth/AuthProvider";
import { UserRole, type CurrentUser } from "../auth/types";
import { ToastProvider } from "../../components/Toast";
import { ProviderDiscipline, type Provider } from "./types";

vi.mock("./api", () => ({
  fetchProviders: vi.fn(),
  createProvider: vi.fn(),
  updateProvider: vi.fn(),
  setProviderActive: vi.fn(),
  deleteProvider: vi.fn(),
  fetchLinkableUsers: vi.fn(),
}));
vi.mock("../admin/api", () => ({ fetchLocations: vi.fn() }));
vi.mock("../auth/AuthProvider", () => ({ useAuth: vi.fn() }));

function provider(overrides: Partial<Provider>): Provider {
  return {
    id: crypto.randomUUID(),
    firstName: "A",
    lastName: "B",
    fullName: "A B",
    specialty: null,
    credentials: null,
    npiNumber: null,
    isActive: true,
    onlineBookingEnabled: false,
    locationIds: [],
    discipline: ProviderDiscipline.PT,
    hasLogin: true,
    userId: null,
    ...overrides,
  };
}

const jamie = provider({
  id: "p-jamie",
  firstName: "Jamie",
  lastName: "Chen",
  fullName: "Jamie Chen",
  credentials: "PT, DPT",
  locationIds: ["loc-1"],
  npiNumber: "1234567890",
});

function renderPage(role: UserRole = UserRole.Admin) {
  const user: CurrentUser = {
    id: "u1",
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
  vi.mocked(fetchLocations).mockResolvedValue([
    { id: "loc-1", name: "Fuquay-Varina", isActive: true } as never,
    { id: "loc-2", name: "Raleigh", isActive: true } as never,
  ]);
  vi.mocked(fetchProviders).mockResolvedValue([
    jamie,
    provider({ fullName: "Avery Kim", discipline: ProviderDiscipline.PTA }),
    provider({ fullName: "Retired Rae", isActive: false }),
  ]);
  vi.mocked(fetchLinkableUsers).mockResolvedValue([
    { id: "u-avery", name: "Avery Kim", userName: "assistant", role: "Assistant" },
  ]);
  render(
    <QueryClientProvider
      client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
    >
      <ToastProvider>
        <MemoryRouter>
          <ProvidersPage />
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

const names = () =>
  screen
    .getAllByRole("row")
    .slice(1)
    .map((row) => within(row).getAllByRole("cell")[1].textContent);

const openMenu = async (name: string) =>
  userEvent.click(await screen.findByRole("button", { name: `Actions for ${name}` }));

describe("ProvidersPage", () => {
  afterEach(() => vi.clearAllMocks());

  it("lists active providers by default, with their location names", async () => {
    renderPage();
    expect(await screen.findByRole("button", { name: "Jamie Chen" })).toBeInTheDocument();
    expect(names()).toEqual(["Jamie Chen", "Avery Kim"]);
    expect(await screen.findByRole("cell", { name: "Fuquay-Varina" })).toBeInTheDocument();
  });

  it("filters by discipline and status with the quick buttons", async () => {
    renderPage();
    await screen.findByRole("button", { name: "Jamie Chen" });

    await userEvent.click(screen.getByRole("button", { name: "PTA" }));
    expect(names()).toEqual(["Avery Kim"]);

    await userEvent.click(within(screen.getByRole("group", { name: "Discipline" })).getByRole("button", { name: "All" }));
    expect(names()).toEqual(["Jamie Chen", "Avery Kim"]);

    await userEvent.click(within(screen.getByRole("group", { name: "Status" })).getByRole("button", { name: "Inactive" }));
    expect(names()).toEqual(["Retired Rae"]);
  });

  it("adds a provider", async () => {
    vi.mocked(createProvider).mockResolvedValue(provider({ fullName: "Sam Lee" }));
    renderPage();
    await userEvent.click(await screen.findByRole("button", { name: "+ Add provider" }));
    const dialog = screen.getByRole("dialog", { name: "Add Provider" });
    const save = within(dialog).getByRole("button", { name: "Save and Close" });
    expect(save).toBeDisabled(); // names required

    await userEvent.type(within(dialog).getByLabelText("First Name"), "Sam");
    await userEvent.type(within(dialog).getByLabelText("Last Name"), "Lee");
    await userEvent.selectOptions(within(dialog).getByLabelText("Discipline"), "PTA");
    await userEvent.type(within(dialog).getByLabelText("NPI"), "123");
    expect(within(dialog).getByText("An NPI is 10 digits.")).toBeInTheDocument();
    expect(save).toBeDisabled();
    await userEvent.type(within(dialog).getByLabelText("NPI"), "4567890");
    await userEvent.click(within(dialog).getByRole("checkbox", { name: "Raleigh" }));
    await userEvent.selectOptions(await within(dialog).findByLabelText("Login"), "u-avery");
    await userEvent.click(save);

    await waitFor(() =>
      expect(createProvider).toHaveBeenCalledWith({
        firstName: "Sam",
        lastName: "Lee",
        credentials: null,
        specialty: null,
        npiNumber: "1234567890",
        discipline: ProviderDiscipline.PTA,
        onlineBookingEnabled: true,
        locationIds: ["loc-2"],
        userId: "u-avery",
      }),
    );
    expect(await screen.findByText("Sam Lee added.")).toBeInTheDocument();
  });

  it("edits a provider from the name or the menu", async () => {
    vi.mocked(updateProvider).mockResolvedValue({ ...jamie, specialty: "Sports" });
    renderPage();
    await userEvent.click(await screen.findByRole("button", { name: "Jamie Chen" }));
    const dialog = screen.getByRole("dialog", { name: "Edit Provider" });
    expect(within(dialog).getByLabelText("NPI")).toHaveValue("1234567890");
    await userEvent.type(within(dialog).getByLabelText("Specialty"), "Sports");
    await userEvent.click(within(dialog).getByRole("checkbox", { name: /Active/ }));
    await userEvent.click(within(dialog).getByRole("button", { name: "Save and Close" }));

    await waitFor(() =>
      expect(updateProvider).toHaveBeenCalledWith(
        "p-jamie",
        expect.objectContaining({ specialty: "Sports", isActive: false, locationIds: ["loc-1"], userId: null }),
      ),
    );
  });

  it("deactivates and deletes after confirming", async () => {
    vi.spyOn(window, "confirm").mockReturnValue(true);
    vi.mocked(setProviderActive).mockResolvedValue({ ...jamie, isActive: false });
    vi.mocked(deleteProvider).mockRejectedValue(
      new Error("Jamie Chen has appointments and can't be deleted. Deactivate them instead; their history is kept."),
    );
    renderPage();

    await openMenu("Jamie Chen");
    await userEvent.click(screen.getByRole("menuitem", { name: "Deactivate" }));
    await waitFor(() => expect(setProviderActive).toHaveBeenCalledWith(jamie, false));
    expect(await screen.findByText("Jamie Chen deactivated.")).toBeInTheDocument();

    await openMenu("Jamie Chen");
    await userEvent.click(screen.getByRole("menuitem", { name: "Delete" }));
    await waitFor(() => expect(deleteProvider).toHaveBeenCalledWith("p-jamie"));
    expect(await screen.findByText(/can't be deleted. Deactivate them instead/)).toBeInTheDocument();
    vi.mocked(window.confirm).mockRestore();
  });

  it("offers editing but not deleting to a scheduler", async () => {
    renderPage(UserRole.Scheduler);
    expect(await screen.findByRole("button", { name: "+ Add provider" })).toBeInTheDocument();
    await openMenu("Jamie Chen");
    expect(screen.getByRole("menuitem", { name: "Edit" })).toBeInTheDocument();
    expect(screen.queryByRole("menuitem", { name: "Delete" })).not.toBeInTheDocument();
  });

  it("is read-only for a biller", async () => {
    renderPage(UserRole.Biller);
    expect(await screen.findByRole("link", { name: "Jamie Chen" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "+ Add provider" })).not.toBeInTheDocument();
    await openMenu("Jamie Chen");
    expect(screen.queryByRole("menuitem", { name: "Edit" })).not.toBeInTheDocument();
    expect(screen.queryByRole("menuitem", { name: "Deactivate" })).not.toBeInTheDocument();
  });
});
