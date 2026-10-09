import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ExerciseLibraryPage } from "./ExerciseLibraryPage";
import { ExerciseEditorPage } from "./ExerciseEditorPage";
import * as api from "./api";
import { useAuth } from "../auth/AuthProvider";
import { UserRole, type CurrentUser } from "../auth/types";
import { ToastProvider } from "../../components/Toast";
import type { Exercise, ExerciseImage, ExerciseSummary } from "./types";

vi.mock("./api", async (orig) => ({
  ...(await orig<typeof import("./api")>()),
  searchExercises: vi.fn(),
  fetchExercise: vi.fn(),
  createExercise: vi.fn(),
  updateExercise: vi.fn(),
  setExerciseActive: vi.fn(),
  markExerciseReviewed: vi.fn(),
  uploadExerciseImage: vi.fn(),
  replaceExerciseImage: vi.fn(),
  updateExerciseImage: vi.fn(),
  removeExerciseImage: vi.fn(),
  reorderExerciseImages: vi.fn(),
}));
vi.mock("../auth/AuthProvider", () => ({ useAuth: vi.fn() }));

const img = (id: string, sequence: number, caption: string): ExerciseImage => ({
  id,
  sequence,
  altText: `Alt ${caption}`,
  caption,
  sourceAttribution: "Drawn by clinic staff",
  width: 800,
  height: 600,
  contentType: "image/png",
  isCurrent: true,
  createdAt: "2026-10-08T12:00:00Z",
});

const bridgeSummary: ExerciseSummary = {
  id: "ex-bridge",
  code: "glute-bridge",
  name: "Bridges",
  patientDescription: "Lift your hips off the floor.",
  bodyRegion: 5,
  category: 13,
  equipment: null,
  difficulty: 0,
  position: 2,
  isActive: true,
  isPlatform: true,
  needsClinicalReview: true,
  coverImage: img("m1", 1, "Starting position"),
  imageCount: 3,
};
const clamSummary: ExerciseSummary = {
  ...bridgeSummary,
  id: "ex-clam",
  name: "Clamshells",
  coverImage: null,
  imageCount: 0,
  needsClinicalReview: false,
  isPlatform: false,
  equipment: "Resistance band",
};

const bridge: Exercise = {
  id: "ex-bridge",
  code: "glute-bridge",
  name: "Bridges",
  patientDescription: "Lift your hips off the floor.",
  clinicalPurpose: "Hip strength.",
  bodyRegion: 5,
  targetMuscles: "Gluteus maximus",
  category: 13,
  startingPosition: "Lie on your back, knees bent.",
  instructions: "Tighten your buttocks.\nLift your hips.\nLower slowly.",
  endingPosition: "Lying flat.",
  equipment: null,
  difficulty: 0,
  position: 2,
  laterality: 3,
  breathingInstructions: "Breathe normally.",
  commonMistakes: "Arching the low back.",
  safetyPrecautions: "Stop if you feel sharp pain.",
  contraindications: null,
  progressions: "Single-leg bridge.",
  regressions: null,
  videoUrl: null,
  isActive: true,
  isPlatform: false,
  needsClinicalReview: true,
  reviewedAt: null,
  reviewedByName: null,
  canEdit: true,
  images: [
    img("m1", 1, "Starting position"),
    img("m2", 2, "Movement"),
    img("m3", 3, "Ending position"),
  ],
  createdAt: "2026-10-08T12:00:00Z",
  createdByName: "Jamie Chen",
  updatedAt: "2026-10-08T12:00:00Z",
  updatedByName: "Jamie Chen",
};

function renderAt(path: string, role: UserRole = UserRole.Therapist) {
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
  render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <ToastProvider>
        <MemoryRouter initialEntries={[path]}>
          <Routes>
            <Route path="/exercises" element={<ExerciseLibraryPage />} />
            <Route path="/exercises/new" element={<ExerciseEditorPage />} />
            <Route
              path="/exercises/:exerciseId/edit"
              element={<ExerciseEditorPage />}
            />
          </Routes>
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  );
}

describe("Exercise library", () => {
  beforeEach(() => {
    vi.mocked(api.searchExercises).mockResolvedValue([
      bridgeSummary,
      clamSummary,
    ]);
    vi.mocked(api.fetchExercise).mockResolvedValue(bridge);
  });
  afterEach(() => vi.clearAllMocks());

  it("shows cards with the image, or a labelled placeholder when there is none", async () => {
    renderAt("/exercises");
    const card = (
      await screen.findByRole("heading", { name: "Bridges" })
    ).closest("li")!;
    const image = within(card).getByRole("img", {
      name: "Alt Starting position",
    });
    expect(image).toHaveAttribute(
      "src",
      expect.stringContaining(
        "/api/v1/exercises/ex-bridge/media/m1?size=thumb",
      ),
    );
    expect(within(card).getByText("Needs clinical review")).toBeInTheDocument();
    expect(within(card).getByText(/3 steps/)).toBeInTheDocument();

    const clam = screen
      .getByRole("heading", { name: "Clamshells" })
      .closest("li")!;
    expect(
      within(clam).getByRole("img", { name: "Image pending approval" }),
    ).toBeInTheDocument();
    expect(
      within(clam).getByText("Equipment: Resistance band"),
    ).toBeInTheDocument();
  });

  it("filters by body region and equipment", async () => {
    renderAt("/exercises");
    await screen.findByRole("heading", { name: "Bridges" });
    await userEvent.selectOptions(screen.getByLabelText("Body region"), "Knee");
    await userEvent.type(screen.getByLabelText("Equipment"), "band");
    await waitFor(() =>
      expect(api.searchExercises).toHaveBeenLastCalledWith(
        expect.objectContaining({ bodyRegion: 6, equipment: "band" }),
      ),
    );
  });

  it("opens the exercise with its numbered image sequence and instructions", async () => {
    renderAt("/exercises");
    await userEvent.click(
      await screen.findByRole("button", { name: "View details: Bridges" }),
    );
    const dialog = await screen.findByRole("dialog", { name: "Bridges" });
    expect(
      within(dialog).getByText("Step 1 — Starting position"),
    ).toBeInTheDocument();
    const sequence = within(dialog).getByRole("list", {
      name: "Image sequence",
    });
    expect(within(sequence).getAllByRole("button")).toHaveLength(3);
    await userEvent.click(
      within(sequence).getByRole("button", {
        name: "Show Step 3 — Ending position",
      }),
    );
    expect(
      within(dialog).getByText("Step 3 — Ending position"),
    ).toBeInTheDocument();
    expect(
      within(dialog).getByRole("img", { name: "Alt Ending position" }),
    ).toHaveAttribute("src", expect.stringContaining("/media/m3"));
    expect(within(dialog).getByText("Lift your hips.")).toBeInTheDocument();
    expect(
      within(dialog).getByText("Stop if you feel sharp pain."),
    ).toBeInTheDocument();
    expect(
      within(dialog).getByRole("button", { name: "Mark as reviewed" }),
    ).toBeInTheDocument();
  });

  it("hides adding and editing from an assistant", async () => {
    renderAt("/exercises", UserRole.Assistant);
    await screen.findByRole("heading", { name: "Bridges" });
    expect(
      screen.queryByRole("link", { name: "+ Add exercise" }),
    ).not.toBeInTheDocument();
  });
});

describe("Exercise editor", () => {
  beforeEach(() => vi.mocked(api.fetchExercise).mockResolvedValue(bridge));
  afterEach(() => vi.clearAllMocks());

  it("adds an exercise, requiring a name and an https video link", async () => {
    vi.mocked(api.createExercise).mockResolvedValue({
      ...bridge,
      id: "ex-new",
      name: "Fictional drill",
      images: [],
    });
    renderAt("/exercises/new");
    const add = await screen.findByRole("button", { name: "Add exercise" });
    expect(add).toBeDisabled();
    await userEvent.type(screen.getByLabelText("Name"), "Fictional drill");
    await userEvent.type(
      screen.getByLabelText("Instructional video (optional)"),
      "javascript:alert(1)",
    );
    expect(
      screen.getByText("The link must start with https://"),
    ).toBeInTheDocument();
    expect(add).toBeDisabled();
    await userEvent.clear(
      screen.getByLabelText("Instructional video (optional)"),
    );
    await userEvent.type(
      screen.getByLabelText("Step-by-step instructions"),
      "Step one",
    );
    await userEvent.click(add);
    await waitFor(() =>
      expect(api.createExercise).toHaveBeenCalledWith(
        expect.objectContaining({
          name: "Fictional drill",
          instructions: "Step one",
          videoUrl: null,
        }),
      ),
    );
  });

  it("uploads an image with its description and step number", async () => {
    vi.mocked(api.uploadExerciseImage).mockResolvedValue(img("m4", 2, "New"));
    renderAt("/exercises/ex-bridge/edit");
    const form = await screen.findByRole("form", { name: "Add an image" });
    const upload = within(form).getByRole("button", { name: "Upload image" });
    const file = new File(
      [new Uint8Array([0x89, 0x50, 0x4e, 0x47])],
      "step.png",
      { type: "image/png" },
    );
    await userEvent.upload(within(form).getByLabelText("Image file"), file);
    expect(upload).toBeDisabled(); // a description is required
    await userEvent.type(
      within(form).getByLabelText("Description for screen readers (required)"),
      "Hips lifted",
    );
    await userEvent.selectOptions(
      within(form).getByLabelText("Step number"),
      "2",
    );
    await userEvent.click(upload);
    await waitFor(() =>
      expect(api.uploadExerciseImage).toHaveBeenCalledWith(
        "ex-bridge",
        expect.objectContaining({ file, altText: "Hips lifted", sequence: 2 }),
      ),
    );
  });

  it("resets the step number to the new last step after each upload", async () => {
    vi.mocked(api.uploadExerciseImage).mockResolvedValue(img("m4", 4, "New"));
    renderAt("/exercises/ex-bridge/edit");
    const form = await screen.findByRole("form", { name: "Add an image" });
    expect(within(form).getByLabelText("Step number")).toHaveValue("4");
    await userEvent.selectOptions(
      within(form).getByLabelText("Step number"),
      "1",
    );
    vi.mocked(api.fetchExercise).mockResolvedValue({
      ...bridge,
      images: [...bridge.images, img("m4", 4, "New")],
    });
    await userEvent.upload(
      within(form).getByLabelText("Image file"),
      new File([new Uint8Array([1])], "a.png", { type: "image/png" }),
    );
    await userEvent.type(
      within(form).getByLabelText("Description for screen readers (required)"),
      "x",
    );
    await userEvent.click(
      within(form).getByRole("button", { name: "Upload image" }),
    );
    await waitFor(() =>
      expect(
        within(
          screen.getByRole("form", { name: "Add an image" }),
        ).getByLabelText("Step number"),
      ).toHaveValue("5"),
    );
  });

  it("reorders and removes images", async () => {
    vi.spyOn(window, "confirm").mockReturnValue(true);
    vi.mocked(api.reorderExerciseImages).mockResolvedValue([]);
    vi.mocked(api.removeExerciseImage).mockResolvedValue(undefined);
    renderAt("/exercises/ex-bridge/edit");
    await userEvent.click(
      await screen.findByRole("button", { name: "Move step 1 down" }),
    );
    await waitFor(() =>
      expect(api.reorderExerciseImages).toHaveBeenCalledWith("ex-bridge", [
        "m2",
        "m1",
        "m3",
      ]),
    );
    expect(
      screen.getByRole("button", { name: "Move step 1 up" }),
    ).toBeDisabled();
    const row = screen.getByText("Step 3 — Ending position").closest("li")!;
    await userEvent.click(within(row).getByRole("button", { name: "Remove" }));
    await waitFor(() =>
      expect(api.removeExerciseImage).toHaveBeenCalledWith("ex-bridge", "m3"),
    );
    vi.mocked(window.confirm).mockRestore();
  });

  it("is read-only for a platform exercise the clinic can't change", async () => {
    vi.mocked(api.fetchExercise).mockResolvedValue({
      ...bridge,
      isPlatform: true,
      canEdit: false,
    });
    renderAt("/exercises/ex-bridge/edit");
    expect(
      await screen.findByText(/only the platform can change it/),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Save exercise" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("form", { name: "Add an image" }),
    ).not.toBeInTheDocument();
  });
});
