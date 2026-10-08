import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ToastProvider } from "../../../components/Toast";
import * as api from "./api";
import { EpisodePanel } from "./EpisodePanel";

vi.mock("./api", () => ({
  SUMMARY_NOTE_TYPES: new Set([4, 5, 6, 11]),
  fetchEpisodeSummary: vi.fn(),
  prefillNote: vi.fn(),
  reviewPrefill: vi.fn(),
}));

const summary: api.EpisodeSummary = {
  noteType: 4,
  episodeStart: "2026-09-01",
  periodStart: "2026-09-01",
  periodEnd: "2026-10-01",
  periodBasis: "since the evaluation of 09/01/2026",
  planOfCareId: "poc1",
  planStart: "2026-09-01",
  planEnd: "2026-10-27",
  frequencyPerWeek: 2,
  durationWeeks: 8,
  visitsInPeriod: 3,
  visitsInEpisode: 3,
  attendance: {
    attended: 3,
    cancelled: 1,
    noShows: 1,
    text: "3 of 5 scheduled visits attended (1 no-show, 1 cancellation).",
  },
  painSummary: "Pain now: 6/10 (09/01/2026) → 3/10 (09/29/2026)",
  measurementChanges: [
    "Knee Flexion AROM (Right): 95 deg (09/01/2026) → 118 deg (09/29/2026)",
  ],
  outcomeChanges: [
    "LEFS: 40/80 (09/01/2026) → 52/80 (09/15/2026), +12 points, meaningful improvement",
  ],
  goalLines: ["STG: Climb 12 stairs — baseline 4, now 8, target 12 stairs"],
  evaluationNoteId: "e1",
  evaluationDate: "2026-09-01",
  sinceEvaluation: [],
  previousProgressNoteId: null,
  previousProgressDate: null,
  sinceProgress: [],
  homeProgram: "Daily quad sets.",
  signedNotesUsed: 3,
};

function renderPanel(props: Partial<Parameters<typeof EpisodePanel>[0]> = {}) {
  const onFilled = vi.fn();
  render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <ToastProvider>
        <EpisodePanel
          noteId="n1"
          readOnly={false}
          saveVersion={4}
          busy={false}
          prefilledAt={null}
          prefillReviewedAt={null}
          onFilled={onFilled}
          {...props}
        />
      </ToastProvider>
    </QueryClientProvider>,
  );
  return onFilled;
}

describe("Episode panel", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.fetchEpisodeSummary).mockResolvedValue(summary);
  });

  it("summarizes the signed charting for the period", async () => {
    renderPanel();
    expect(
      await screen.findByText(
        "3 of 5 scheduled visits attended (1 no-show, 1 cancellation).",
      ),
    ).toBeInTheDocument();
    expect(
      screen.getByText("since the evaluation of 09/01/2026"),
    ).toBeInTheDocument();
    expect(screen.getByText("3 this period")).toBeInTheDocument();
    expect(screen.getByText("2x/week for 8 weeks")).toBeInTheDocument();
    await userEvent.click(
      screen.getByText("Changes over the episode (3 signed notes)"),
    );
    expect(
      screen.getByText(/LEFS: 40\/80 \(09\/01\/2026\) → 52\/80/),
    ).toBeInTheDocument();
    expect(screen.getByText("Daily quad sets.")).toBeInTheDocument();
  });

  it("fills empty fields from the saved note, then reloads it", async () => {
    vi.mocked(api.prefillNote).mockResolvedValue({
      saveVersion: 5,
      filledFields: ["Period start", "Objective changes"],
      goalsAdded: 1,
      summary,
    });
    const onFilled = renderPanel();
    await userEvent.click(
      await screen.findByRole("button", {
        name: "Fill empty fields from charting",
      }),
    );
    await waitFor(() => expect(api.prefillNote).toHaveBeenCalledWith("n1", 4));
    await waitFor(() => expect(onFilled).toHaveBeenCalled());
    expect(
      await screen.findByText(
        "Filled 2 fields and added 1 goal from signed charting. Review before signing.",
      ),
    ).toBeInTheDocument();
  });

  it("waits for unsaved changes before filling", async () => {
    renderPanel({ busy: true });
    expect(
      await screen.findByRole("button", {
        name: "Fill empty fields from charting",
      }),
    ).toBeDisabled();
  });

  it("requires the therapist to confirm the review of pre-filled content", async () => {
    vi.mocked(api.reviewPrefill).mockResolvedValue({
      prefillReviewedAt: "2026-10-08T12:00:00Z",
    });
    renderPanel({ prefilledAt: "2026-10-08T11:00:00Z" });
    expect(screen.getByRole("alert")).toHaveTextContent(
      "the note can’t be signed until you confirm",
    );
    await userEvent.click(
      screen.getByRole("button", {
        name: "I have reviewed the pre-filled content",
      }),
    );
    expect(
      await screen.findByText("Pre-filled content reviewed 10/08/2026."),
    ).toBeInTheDocument();
    expect(api.reviewPrefill).toHaveBeenCalledWith("n1");
  });
});
