import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { NewPatientForm } from "./NewPatientForm";
import { createPatient, fetchScheduleSettings, searchPatients } from "../schedule/api";

vi.mock("../schedule/api", () => ({
  fetchScheduleSettings: vi.fn(),
  searchPatients: vi.fn(),
  createPatient: vi.fn(),
}));

const settings = {
  locations: [{ id: "loc-1", name: "Fuquay-Varina" }],
  providers: [
    { id: "prov-1", name: "Jamie Chen", credentials: "PT", isActive: true, userId: "user-1" },
    { id: "prov-2", name: "No Login", credentials: null, isActive: true, userId: null },
  ],
};

function renderForm(onCreated = vi.fn()) {
  vi.mocked(fetchScheduleSettings).mockResolvedValue(settings as never);
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <NewPatientForm onCreated={onCreated} onCancel={vi.fn()} />
    </QueryClientProvider>,
  );
  return onCreated;
}

async function fillRequired() {
  await userEvent.type(screen.getByLabelText(/first name/i), "Nora");
  await userEvent.type(screen.getByLabelText(/last name/i), "Quill");
  await userEvent.type(screen.getByLabelText(/date of birth/i), "1988-04-02");
}

describe("NewPatientForm", () => {
  beforeEach(() => vi.clearAllMocks());

  it("creates the patient with the entered details when no duplicate exists", async () => {
    vi.mocked(searchPatients).mockResolvedValue([]);
    const created = { id: "p1", fullName: "Nora Quill", medicalRecordNumber: "SM-1" };
    vi.mocked(createPatient).mockResolvedValue(created as never);
    const onCreated = renderForm();

    await fillRequired();
    await userEvent.type(screen.getByLabelText(/^phone/i), "919-555-0199");
    await userEvent.selectOptions(await screen.findByLabelText(/assigned therapist/i), "user-1");
    await userEvent.click(screen.getByRole("button", { name: "Add patient" }));

    await waitFor(() => expect(onCreated).toHaveBeenCalledWith(created));
    expect(createPatient).toHaveBeenCalledWith(
      expect.objectContaining({
        firstName: "Nora",
        lastName: "Quill",
        dateOfBirth: "1988-04-02",
        phone: "919-555-0199",
        email: null,
        assignedTherapistId: "user-1",
      }),
    );
    // Providers without a login can't own a caseload, so they aren't offered.
    expect(screen.queryByRole("option", { name: "No Login" })).not.toBeInTheDocument();
  });

  it("shows an existing chart with the same name and birth date before creating", async () => {
    vi.mocked(searchPatients).mockResolvedValue([
      { id: "old", fullName: "Nora Quill", medicalRecordNumber: "SM-OLD", dateOfBirth: "1988-04-02", phone: null } as never,
    ]);
    renderForm();

    await fillRequired();
    await userEvent.click(screen.getByRole("button", { name: "Add patient" }));

    expect(await screen.findByText("Possible duplicate")).toBeInTheDocument();
    expect(screen.getByText("SM-OLD")).toBeInTheDocument();
    expect(createPatient).not.toHaveBeenCalled();
  });

  it("keeps Add disabled until first name, last name and date of birth are filled", async () => {
    renderForm();
    const add = screen.getByRole("button", { name: "Add patient" });
    expect(add).toBeDisabled();
    await fillRequired();
    expect(add).toBeEnabled();
  });
});
