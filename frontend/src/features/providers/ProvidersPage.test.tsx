import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ProvidersPage } from "./ProvidersPage";
import { fetchProviders } from "./api";
import { fetchLocations } from "../admin/api";
import { ProviderDiscipline, type Provider } from "./types";

vi.mock("./api", () => ({ fetchProviders: vi.fn() }));
vi.mock("../admin/api", () => ({ fetchLocations: vi.fn() }));

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
    ...overrides,
  };
}

function renderPage() {
  vi.mocked(fetchLocations).mockResolvedValue([{ id: "loc-1", name: "Fuquay-Varina" } as never]);
  vi.mocked(fetchProviders).mockResolvedValue([
    provider({ fullName: "Jamie Chen", credentials: "PT, DPT", locationIds: ["loc-1"] }),
    provider({ fullName: "Avery Kim", discipline: ProviderDiscipline.PTA }),
    provider({ fullName: "Retired Rae", isActive: false }),
  ]);
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <MemoryRouter>
        <ProvidersPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

const names = () =>
  screen
    .getAllByRole("row")
    .slice(1)
    .map((row) => within(row).getAllByRole("cell")[1].textContent);

describe("ProvidersPage", () => {
  it("lists active providers by default, with their location names", async () => {
    renderPage();
    expect(await screen.findByRole("link", { name: "Jamie Chen" })).toBeInTheDocument();
    expect(names()).toEqual(["Jamie Chen", "Avery Kim"]);
    expect(await screen.findByRole("cell", { name: "Fuquay-Varina" })).toBeInTheDocument();
  });

  it("filters by discipline and status with the quick buttons", async () => {
    renderPage();
    await screen.findByRole("link", { name: "Jamie Chen" });

    await userEvent.click(screen.getByRole("button", { name: "PTA" }));
    expect(names()).toEqual(["Avery Kim"]);

    await userEvent.click(within(screen.getByRole("group", { name: "Discipline" })).getByRole("button", { name: "All" }));
    expect(names()).toEqual(["Jamie Chen", "Avery Kim"]);

    await userEvent.click(within(screen.getByRole("group", { name: "Status" })).getByRole("button", { name: "Inactive" }));
    expect(names()).toEqual(["Retired Rae"]);
  });
});
