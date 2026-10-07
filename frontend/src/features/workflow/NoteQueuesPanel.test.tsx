import { render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { NoteQueuesPanel } from "./NoteQueuesPanel";
import { fetchNoteQueues } from "../charting/api";
import { NoteStatus, NoteType } from "./types";
import type { NoteQueueItem } from "../charting/types";

vi.mock("../charting/api", () => ({ fetchNoteQueues: vi.fn() }));

const item = (over: Partial<NoteQueueItem>): NoteQueueItem => ({
  noteId: "n1",
  patientId: "p1",
  patientName: "Quinn Alvarez",
  medicalRecordNumber: "SM-1",
  noteType: NoteType.Daily,
  status: NoteStatus.Draft,
  serviceDate: "2026-10-05",
  authorName: "Jamie Chen",
  isAmendment: false,
  ...over,
});

const renderPanel = () =>
  render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <MemoryRouter>
        <NoteQueuesPanel today="2026-10-07" />
      </MemoryRouter>
    </QueryClientProvider>,
  );

describe("NoteQueuesPanel", () => {
  it("lists earlier unfinished notes and notes awaiting my cosign, but not today's drafts", async () => {
    vi.mocked(fetchNoteQueues).mockResolvedValue({
      myUnsignedNotes: [
        item({ noteId: "old" }),
        item({
          noteId: "today",
          serviceDate: "2026-10-07",
          patientName: "Today Patient",
        }),
        item({
          noteId: "amend",
          serviceDate: "2026-10-07",
          isAmendment: true,
          patientName: "Rowan Hale",
        }),
      ],
      awaitingMyCosign: [
        item({
          noteId: "pta",
          status: NoteStatus.ReviewRequired,
          authorName: "Sam Lee",
          patientName: "Avery Kim",
        }),
      ],
    });
    renderPanel();

    const mine = await screen.findByRole("region", {
      name: "Your unfinished notes",
    });
    expect(within(mine).getByText("Quinn Alvarez")).toBeInTheDocument();
    expect(within(mine).getByText("Rowan Hale")).toBeInTheDocument();
    expect(within(mine).queryByText("Today Patient")).not.toBeInTheDocument();

    const cosign = screen.getByRole("region", {
      name: "Waiting for your cosign",
    });
    expect(within(cosign).getByText(/Sam Lee/)).toBeInTheDocument();
    expect(within(cosign).getByRole("link", { name: "Open" })).toHaveAttribute(
      "href",
      "/chart/pta",
    );
  });

  it("shows nothing when there is nothing to do", async () => {
    vi.mocked(fetchNoteQueues).mockResolvedValue({
      myUnsignedNotes: [],
      awaitingMyCosign: [],
    });
    const { container } = renderPanel();
    await new Promise((r) => setTimeout(r, 50));
    expect(container).toBeEmptyDOMElement();
  });
});
