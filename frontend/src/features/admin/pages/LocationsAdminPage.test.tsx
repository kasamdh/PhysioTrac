import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { LocationsAdminPage } from "./LocationsAdminPage";
import { createLocation, fetchLocations, updateLocation } from "../api";
import { ToastProvider } from "../../../components/Toast";
import type { AdminLocation } from "../types";

vi.mock("../api", () => ({
  fetchLocations: vi.fn(),
  createLocation: vi.fn(),
  updateLocation: vi.fn(),
  setLocationActive: vi.fn(),
}));

const raleigh: AdminLocation = {
  id: "loc-1",
  name: "Raleigh",
  addressLine1: "500 Wellness Blvd",
  addressLine2: null,
  city: "Raleigh",
  state: "NC",
  zipCode: "27601",
  phone: "919-555-0199",
  timezone: "America/New_York",
  npiNumber: "123", // an existing odd value must not block other edits
  taxId: null,
  isActive: true,
};

function renderPage() {
  vi.mocked(fetchLocations).mockResolvedValue([raleigh]);
  render(
    <QueryClientProvider client={new QueryClient()}>
      <ToastProvider>
        <MemoryRouter>
          <LocationsAdminPage />
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

describe("LocationsAdminPage dialogs", () => {
  beforeEach(() => vi.clearAllMocks());

  it("adds a location in an Add Location dialog with Name required", async () => {
    vi.mocked(createLocation).mockResolvedValue({ ...raleigh, id: "loc-2", name: "Cary" });
    renderPage();
    await userEvent.click(await screen.findByRole("button", { name: "+ Add location" }));

    const dialog = screen.getByRole("dialog", { name: "Add Location" });
    expect(within(dialog).getByText("Required")).toBeInTheDocument();
    const save = within(dialog).getByRole("button", { name: "Save and Close" });
    expect(save).toBeDisabled();

    await userEvent.type(within(dialog).getByLabelText("Location Name"), "Cary");
    await userEvent.type(within(dialog).getByLabelText("City"), "Cary");
    await userEvent.click(save);

    await waitFor(() =>
      expect(createLocation).toHaveBeenCalledWith(expect.objectContaining({ name: "Cary", city: "Cary", addressLine1: null })),
    );
  });

  it("edits a location in an Edit Location dialog, keeping an existing odd NPI", async () => {
    vi.mocked(updateLocation).mockResolvedValue({ ...raleigh, phone: "919-555-0100" });
    renderPage();
    await userEvent.click(await screen.findByRole("button", { name: "Raleigh" }));

    const dialog = screen.getByRole("dialog", { name: "Edit Location" });
    const save = within(dialog).getByRole("button", { name: "Save and Close" });
    expect(save).toBeDisabled(); // nothing changed yet
    const phone = within(dialog).getByLabelText("Phone");
    await userEvent.clear(phone);
    await userEvent.type(phone, "919-555-0100");
    await userEvent.click(save);

    await waitFor(() =>
      expect(updateLocation).toHaveBeenCalledWith("loc-1", expect.objectContaining({ phone: "919-555-0100", npiNumber: "123" })),
    );
  });
});
