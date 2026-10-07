import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { EditPatientDialog } from "./EditPatientDialog";
import { fetchPatientDetail, updatePatient } from "../admin/api";
import { fetchScheduleSettings } from "../schedule/api";
import { PatientStatus } from "./types";
import type { PatientDetail } from "../admin/types";

vi.mock("../admin/api", () => ({ fetchPatientDetail: vi.fn(), updatePatient: vi.fn() }));
vi.mock("../schedule/api", () => ({ fetchScheduleSettings: vi.fn() }));

const quinn: PatientDetail = {
  id: "p1",
  medicalRecordNumber: "SM-1",
  firstName: "Quinn",
  lastName: "Alvarez",
  fullName: "Quinn Alvarez",
  dateOfBirth: "2001-07-30",
  age: 25,
  phone: "555-0104",
  email: null,
  address: null,
  emergencyContact: null,
  preferredLanguage: "Spanish",
  diagnoses: null,
  precautions: null,
  assignedTherapistId: null,
  primaryLocationId: "loc-1",
  primaryCareProviderId: "pcp-9",
  referringProviderId: "ref-7",
  status: PatientStatus.Active,
};

function renderDialog() {
  vi.mocked(fetchPatientDetail).mockResolvedValue(quinn);
  vi.mocked(fetchScheduleSettings).mockResolvedValue({ locations: [{ id: "loc-1", name: "Raleigh" }], providers: [] } as never);
  const onSaved = vi.fn();
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <EditPatientDialog patientId="p1" onClose={vi.fn()} onSaved={onSaved} />
    </QueryClientProvider>,
  );
  return onSaved;
}

describe("EditPatientDialog", () => {
  beforeEach(() => vi.clearAllMocks());

  it("saves edits and sends back the fields it doesn't show, unchanged", async () => {
    vi.mocked(updatePatient).mockResolvedValue({ ...quinn, status: PatientStatus.Discharged });
    const onSaved = renderDialog();

    const dob = await screen.findByLabelText("Date of Birth");
    await userEvent.clear(dob);
    await userEvent.type(dob, "2001-08-30");
    await userEvent.click(screen.getByRole("button", { name: "Discharged" }));
    await userEvent.click(screen.getByRole("button", { name: "Save and Close" }));

    await waitFor(() =>
      expect(updatePatient).toHaveBeenCalledWith(
        "p1",
        expect.objectContaining({
          firstName: "Quinn",
          dateOfBirth: "2001-08-30",
          status: PatientStatus.Discharged,
          preferredLanguage: "Spanish",
          primaryLocationId: "loc-1",
          primaryCareProviderId: "pcp-9",
          referringProviderId: "ref-7",
        }),
      ),
    );
    expect(onSaved).toHaveBeenCalled();
  });

  it("keeps Save and Close disabled until something changes, and rejects a blank name", async () => {
    renderDialog();
    const save = await screen.findByRole("button", { name: "Save and Close" });
    expect(save).toBeDisabled();
    await userEvent.clear(screen.getByLabelText("First name"));
    expect(save).toBeDisabled();
    await userEvent.type(screen.getByLabelText("First name"), "Quin");
    expect(save).toBeEnabled();
  });
});
