import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { TemplateForm } from "./TemplateForm";
import { TemplatesPage } from "./pages/TemplatesPage";
import { TemplateEditorPage } from "./pages/TemplateEditorPage";
import * as api from "./api";
import { apiRequest } from "../../lib/apiClient";
import { useAuth } from "../auth/AuthProvider";
import { UserRole } from "../auth/types";
import { ToastProvider } from "../../components/Toast";
import { isVisible, missingRequired, toKey } from "./rules";
import {
  FieldType,
  type DocTemplate,
  type DocTemplateDetail,
  type FieldValues,
  type TemplateSection,
} from "./types";

vi.mock("./api", () => ({
  fetchTemplates: vi.fn(),
  fetchTemplate: vi.fn(),
  fetchTemplateVersions: vi.fn(),
  fetchTemplateVersion: vi.fn(),
  createTemplate: vi.fn(),
  updateTemplate: vi.fn(),
  setTemplateActive: vi.fn(),
  copyTemplate: vi.fn(),
  setTemplateFavorite: vi.fn(),
}));
vi.mock("../../lib/apiClient", async (orig) => ({
  ...(await orig<typeof import("../../lib/apiClient")>()),
  apiRequest: vi.fn(),
}));
vi.mock("../auth/AuthProvider", () => ({ useAuth: vi.fn() }));
const navigateSpy = vi.fn();
vi.mock("react-router-dom", async (orig) => ({
  ...(await orig<typeof import("react-router-dom")>()),
  useNavigate: () => navigateSpy,
}));

const sections: TemplateSection[] = [
  {
    key: "subjective",
    title: "Subjective",
    fields: [
      {
        key: "report",
        label: "Patient report",
        fieldType: FieldType.LongText,
        isRequired: true,
        noteColumn: "subjective",
      },
      {
        key: "pain",
        label: "Pain now",
        fieldType: FieldType.PainScale,
        isRequired: true,
        scaleMin: 0,
        scaleMax: 10,
      },
      {
        key: "events",
        label: "Adverse events",
        fieldType: FieldType.Radio,
        isRequired: false,
        options: ["Yes", "No"],
      },
      {
        key: "eventDetail",
        label: "Event details",
        fieldType: FieldType.LongText,
        isRequired: true,
        condition: { field: "events", value: "Yes" },
      },
      {
        key: "reps",
        label: "Reps",
        fieldType: FieldType.Number,
        isRequired: false,
        unit: "reps",
        validation: { min: 1, max: 50 },
      },
      {
        key: "aids",
        label: "Aids",
        fieldType: FieldType.Multiselect,
        isRequired: false,
        options: ["Cane", "Walker"],
      },
      {
        key: "exercises",
        label: "Exercises",
        fieldType: FieldType.StructuredTable,
        isRequired: false,
        columns: [{ key: "name", label: "Name" }],
      },
      {
        key: "signature",
        label: "Therapist signature",
        fieldType: FieldType.Signature,
        isRequired: false,
      },
    ],
  },
  { key: "goals", title: "Goals", fields: [], component: "goals" },
];

function FormHarness() {
  const [values, setValues] = useState<FieldValues>({});
  const [columns, setColumns] = useState<Record<string, string>>({});
  return (
    <>
      <TemplateForm
        sections={sections}
        values={values}
        columns={columns}
        onChange={(v) => setValues((p) => ({ ...p, [v.key]: v }))}
        onColumnChange={(c, t) => setColumns((p) => ({ ...p, [c]: t }))}
      />
      <output data-testid="state">{JSON.stringify({ values, columns })}</output>
    </>
  );
}

describe("TemplateForm", () => {
  it("renders every field from the template, with a conditional field appearing on its answer", async () => {
    render(<FormHarness />);
    expect(
      screen.getByRole("heading", { name: "Subjective" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/Goals — recorded in the encounter/),
    ).toBeInTheDocument();
    expect(screen.queryByLabelText(/Event details/)).not.toBeInTheDocument();

    await userEvent.click(
      within(
        screen.getByRole("radiogroup", { name: "Adverse events" }),
      ).getByRole("radio", { name: "Yes" }),
    );
    expect(screen.getByLabelText(/Event details/)).toBeInTheDocument();

    await userEvent.type(screen.getByLabelText(/Patient report/), "Better");
    await userEvent.click(
      within(screen.getByRole("group", { name: "Pain now *" })).getByRole(
        "button",
        { name: "3" },
      ),
    );
    await userEvent.click(
      within(screen.getByRole("group", { name: "Aids" })).getByRole("button", {
        name: "Cane",
      }),
    );
    await userEvent.click(screen.getByRole("button", { name: "+ Add row" }));
    await userEvent.type(
      screen.getByLabelText("Exercises row 1 Name"),
      "Bridges",
    );

    const state = JSON.parse(screen.getByTestId("state").textContent!);
    expect(state.columns.subjective).toBe("Better"); // stored in the note column, not a field value
    expect(state.values.pain.number).toBe(3);
    expect(JSON.parse(state.values.aids.json)).toEqual(["Cane"]);
    expect(JSON.parse(state.values.exercises.json)).toEqual([
      { name: "Bridges" },
    ]);
    expect(
      screen.getByText(/applied electronically when the note is signed/),
    ).toBeInTheDocument();
  });

  it("flags values outside the field's limits", async () => {
    render(<FormHarness />);
    await userEvent.type(screen.getByLabelText(/^Reps/), "99");
    expect(screen.getByRole("alert")).toHaveTextContent("Must be at most 50.");
  });
});

describe("template rules (client mirror)", () => {
  const fields = sections[0].fields;
  it("lists required visible fields that are empty", () => {
    expect(missingRequired(fields, {}, {})).toEqual([
      "Patient report",
      "Pain now",
    ]);
    expect(
      missingRequired(
        fields,
        {
          events: { key: "events", text: "Yes" },
          pain: { key: "pain", number: 0 },
        },
        { subjective: "ok" },
      ),
    ).toEqual(["Event details"]);
  });
  it("evaluates conditions and makes keys", () => {
    expect(
      isVisible(fields[3], { events: { key: "events", text: "yes" } }),
    ).toBe(true);
    expect(toKey("Pain at rest")).toBe("painAtRest");
    expect(toKey("Pain at rest", new Set(["painAtRest"]))).toBe("painAtRest2");
  });
});

const template = (over: Partial<DocTemplate> = {}): DocTemplate => ({
  id: "t1",
  name: "Daily Treatment SOAP Note",
  noteType: 1,
  specialty: 0,
  description: null,
  isSystem: true,
  isActive: true,
  currentVersionId: "v1",
  currentVersionNumber: 1,
  appointmentTypeIds: [],
  isFavorite: false,
  updatedAt: "2026-10-07T12:00:00Z",
  ...over,
});
const detail = (over: Partial<DocTemplate> = {}): DocTemplateDetail => ({
  template: template(over),
  currentVersion: {
    id: "v1",
    templateId: "t1",
    versionNumber: 1,
    changeSummary: null,
    createdAt: "2026-10-07T12:00:00Z",
    createdByName: null,
    sections,
  },
});

function renderAt(path: string, role: UserRole = UserRole.Admin) {
  vi.mocked(useAuth).mockReturnValue({
    user: {
      id: "u1",
      username: "a",
      email: null,
      role,
      organizationId: "o",
      isPlatformSuperAdmin: false,
      mustChangePassword: false,
    },
    isLoading: false,
    loginError: null,
    isLoggingIn: false,
    signIn: vi.fn(),
    signOut: vi.fn(),
  });
  vi.mocked(apiRequest).mockResolvedValue([
    { id: "at1", name: "Knee follow-up", isActive: true },
  ]);
  render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <ToastProvider>
        <MemoryRouter initialEntries={[path]}>
          <Routes>
            <Route path="/admin/templates" element={<TemplatesPage />} />
            <Route
              path="/admin/templates/new"
              element={<TemplateEditorPage />}
            />
            <Route
              path="/admin/templates/:templateId"
              element={<TemplateEditorPage />}
            />
          </Routes>
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

describe("Template management", () => {
  beforeEach(() => vi.clearAllMocks());

  it("lists templates and passes the filters to the server", async () => {
    vi.mocked(api.fetchTemplates).mockResolvedValue([
      template(),
      template({
        id: "t2",
        name: "Knee daily",
        isSystem: false,
        isFavorite: true,
      }),
    ]);
    renderAt("/admin/templates");
    expect(
      await screen.findByRole("link", { name: /Favorite:.*Knee daily/ }),
    ).toHaveAttribute("href", "/admin/templates/t2");
    expect(screen.getByRole("cell", { name: "System" })).toBeInTheDocument();
    await userEvent.selectOptions(screen.getByLabelText("Note type"), "4");
    await waitFor(() =>
      expect(api.fetchTemplates).toHaveBeenLastCalledWith(
        expect.objectContaining({ noteType: 4 }),
      ),
    );
  });

  it("creates a template with a section and field", async () => {
    vi.mocked(api.createTemplate).mockResolvedValue(
      detail({ isSystem: false, id: "new1", name: "Shoulder daily" }),
    );
    renderAt("/admin/templates/new");
    await userEvent.type(
      await screen.findByLabelText("Name"),
      "Shoulder daily",
    );
    await userEvent.click(screen.getByRole("button", { name: "+ Add field" }));
    await userEvent.click(screen.getByRole("button", { name: /^▸ New field/ }));
    const label = screen.getByLabelText("Label");
    await userEvent.clear(label);
    await userEvent.type(label, "Pain now");
    await userEvent.selectOptions(
      screen.getByLabelText("Type"),
      String(FieldType.PainScale),
    );
    await userEvent.click(
      screen.getByRole("checkbox", { name: "Required to sign" }),
    );
    await userEvent.click(
      screen.getByRole("button", { name: "Create template" }),
    );

    await waitFor(() => expect(api.createTemplate).toHaveBeenCalled());
    const body = vi.mocked(api.createTemplate).mock.calls[0][0];
    expect(body.name).toBe("Shoulder daily");
    expect(body.sections[0].fields[0]).toMatchObject({
      label: "Pain now",
      fieldType: FieldType.PainScale,
      isRequired: true,
      scaleMax: 10,
    });
    expect(navigateSpy).toHaveBeenCalledWith("/admin/templates/new1", {
      replace: true,
    });
  });

  it("shows the server's reasons when a template is invalid", async () => {
    const { ApiError } = await vi.importActual<
      typeof import("../../lib/apiClient")
    >("../../lib/apiClient");
    vi.mocked(api.createTemplate).mockRejectedValue(
      new ApiError("2 problems", 422, undefined, {
        errors: ["Enter a template name.", "Add the choices."],
      }),
    );
    renderAt("/admin/templates/new");
    await userEvent.click(
      await screen.findByRole("button", { name: "Create template" }),
    );
    expect(
      await screen.findByText("Enter a template name."),
    ).toBeInTheDocument();
    expect(screen.getByText("Add the choices.")).toBeInTheDocument();
  });

  it("opens a system template read-only, offering a copy", async () => {
    vi.mocked(api.fetchTemplate).mockResolvedValue(detail());
    vi.mocked(api.copyTemplate).mockResolvedValue(
      detail({ id: "c1", isSystem: false }),
    );
    renderAt("/admin/templates/t1");
    expect(
      await screen.findByText(/This is a system template/),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Save" }),
    ).not.toBeInTheDocument();
    expect(screen.getByLabelText("Name")).toHaveAttribute("readonly");
    await userEvent.click(
      screen.getByRole("button", { name: "Copy to customize" }),
    );
    await waitFor(() =>
      expect(navigateSpy).toHaveBeenCalledWith("/admin/templates/c1"),
    );
  });

  it("lets a therapist view but not edit a clinic template", async () => {
    vi.mocked(api.fetchTemplate).mockResolvedValue(detail({ isSystem: false }));
    renderAt("/admin/templates/t1", UserRole.Therapist);
    expect(await screen.findByLabelText("Name")).toHaveAttribute("readonly");
    expect(
      screen.queryByRole("button", { name: "Save" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: /Copy/ }),
    ).not.toBeInTheDocument();
  });

  it("saves an edit and reports the new version", async () => {
    vi.mocked(api.fetchTemplate).mockResolvedValue(detail({ isSystem: false }));
    vi.mocked(api.updateTemplate).mockResolvedValue({
      ...detail({ isSystem: false, currentVersionNumber: 2 }),
      currentVersion: {
        ...detail().currentVersion,
        id: "v2",
        versionNumber: 2,
      },
    });
    renderAt("/admin/templates/t1");
    await userEvent.type(
      await screen.findByLabelText(/What changed/),
      "Added a field",
    );
    await userEvent.click(screen.getByRole("button", { name: "Save" }));
    await waitFor(() =>
      expect(api.updateTemplate).toHaveBeenCalledWith(
        "t1",
        expect.objectContaining({ changeSummary: "Added a field" }),
      ),
    );
    expect(await screen.findByText("Saved as version 2.")).toBeInTheDocument();
  });
});
