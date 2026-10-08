import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ToastProvider } from "../../../components/Toast";
import { FlowsheetPanel } from "./FlowsheetPanel";
import * as api from "./api";
import {
  analyze,
  blankEntry,
  unitsFor,
  type FlowsheetEntry,
  type LibraryItem,
  type PreviousFlowsheet,
} from "./model";

vi.mock("./api", () => ({
  searchInterventions: vi.fn(),
  setInterventionFavorite: vi.fn(),
  fetchInterventionGroups: vi.fn(),
  createInterventionGroup: vi.fn(),
  deleteInterventionGroup: vi.fn(),
}));

const item = (over: Partial<LibraryItem>): LibraryItem => ({
  id: "i1",
  code: "bridges",
  name: "Bridges",
  category: 0,
  cptCode: "97110",
  isTimed: true,
  bodyRegion: "Hip",
  description: null,
  defaultSets: 3,
  defaultRepetitions: 10,
  defaultResistance: null,
  defaultDuration: null,
  defaultEquipment: null,
  defaultPosition: "Supine",
  isActive: true,
  isSystem: true,
  isFavorite: false,
  ...over,
});

function Harness({
  initial = [],
  previous = null,
}: {
  initial?: FlowsheetEntry[];
  previous?: PreviousFlowsheet | null;
}) {
  const [entries, setEntries] = useState(initial);
  return (
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <ToastProvider>
        <FlowsheetPanel
          value={entries}
          onChange={setEntries}
          readOnly={false}
          previous={previous}
          ruleVariant="Medicare"
          canShareGroups
        />
        <output data-testid="entries">{JSON.stringify(entries)}</output>
      </ToastProvider>
    </QueryClientProvider>
  );
}
const totals = () =>
  screen
    .getAllByRole("status")
    .find((el) => /timed minutes/.test(el.textContent ?? ""))!;
const entries = () =>
  JSON.parse(screen.getByTestId("entries").textContent!) as FlowsheetEntry[];

describe("Intervention flowsheet", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.searchInterventions).mockResolvedValue([
      item({}),
      item({
        id: "i2",
        name: "Epley maneuver",
        category: 8,
        cptCode: "95992",
        isTimed: false,
        isFavorite: true,
      }),
    ]);
    vi.mocked(api.fetchInterventionGroups).mockResolvedValue([
      {
        id: "g1",
        name: "Knee strength",
        description: null,
        isShared: false,
        isMine: true,
        isFavorite: false,
        items: [
          {
            name: "Quad sets",
            category: 0,
            isTimed: true,
            cptCode: "97110",
            sets: 3,
            repetitions: 10,
            resistance: null,
            duration: null,
            equipment: null,
            position: null,
            libraryItemId: null,
          },
        ],
      },
    ]);
  });

  it("adds from the library by typing and pressing Enter, with the item's defaults", async () => {
    render(<Harness />);
    await userEvent.type(
      screen.getByRole("searchbox", { name: "Add intervention" }),
      "bri{Enter}",
    );
    await waitFor(() => expect(entries()).toHaveLength(1));
    expect(entries()[0]).toMatchObject({
      description: "Bridges",
      cptCode: "97110",
      isTimed: true,
      sets: 3,
      repetitions: 10,
      position: "Supine",
      libraryItemId: "i1",
    });
  });

  it("Enter picks from what was typed, not the previous search's list", async () => {
    // Typed searches answer slowly, so Enter lands while the list still
    // shows the empty search's results.
    vi.mocked(api.searchInterventions).mockImplementation(({ search }) =>
      search?.trim()
        ? new Promise((done) =>
            setTimeout(
              () => done([item({ id: "i3", name: "Mini squats" })]),
              300,
            ),
          )
        : Promise.resolve([
            item({ id: "i4", name: "ADL training", cptCode: "97535" }),
          ]),
    );
    render(<Harness />);
    await waitFor(() => expect(api.searchInterventions).toHaveBeenCalled());
    await new Promise((done) => setTimeout(done, 50));
    await userEvent.type(
      screen.getByRole("searchbox", { name: "Add intervention" }),
      "mini squat{Enter}",
    );
    await waitFor(() => expect(entries()).toHaveLength(1));
    expect(entries()[0]).toMatchObject({
      description: "Mini squats",
      libraryItemId: "i3",
    });
  });

  it("adds favorites and groups in one click", async () => {
    render(<Harness />);
    await userEvent.click(
      await screen.findByRole("button", { name: "Epley maneuver" }),
    );
    await userEvent.click(
      await screen.findByRole("button", { name: /\+ Knee strength \(1\)/ }),
    );
    expect(entries().map((e) => e.description)).toEqual([
      "Epley maneuver",
      "Quad sets",
    ]);
    expect(entries()[0].isTimed).toBe(false);
  });

  it("records dosage, timing, pain and response, filling minutes from the times", async () => {
    render(
      <Harness
        initial={[blankEntry({ description: "Bridges", cptCode: "97110" })]}
      />,
    );
    await userEvent.type(screen.getByLabelText("Start"), "09:00");
    await userEvent.type(screen.getByLabelText("End"), "09:12");
    await userEvent.type(screen.getByLabelText("Sets"), "3");
    await userEvent.type(screen.getByLabelText("Reps"), "12");
    await userEvent.type(screen.getByLabelText("Resistance"), "Red band");
    await userEvent.type(screen.getByLabelText("Pain before (0–10)"), "5");
    await userEvent.type(screen.getByLabelText("Pain after (0–10)"), "2");
    await userEvent.type(
      screen.getByLabelText("Patient response"),
      "Tolerated well",
    );
    await userEvent.selectOptions(screen.getByLabelText("Status"), "1");
    expect(entries()[0]).toMatchObject({
      minutes: 12,
      sets: 3,
      repetitions: 12,
      resistance: "Red band",
      painBefore: 5,
      painAfter: 2,
      patientResponse: "Tolerated well",
      status: 1,
    });
    expect(totals()).toHaveTextContent(
      "12 timed minutes · 0 untimed services · estimated 1 timed unit",
    );
  });

  it("carries forward only the chosen entries, marked for review", async () => {
    const previous: PreviousFlowsheet = {
      noteId: "prev",
      serviceDate: "2026-10-01",
      entries: [
        blankEntry({
          description: "Bridges",
          sets: 3,
          repetitions: 10,
          minutes: 12,
          patientResponse: "Old response",
          painBefore: 6,
        }),
        blankEntry({ description: "Joint mobilization", minutes: 15 }),
      ],
    };
    render(<Harness previous={previous} />);
    await userEvent.click(
      screen.getByRole("button", { name: /Carry forward from 10\/01\/2026/ }),
    );
    await userEvent.click(screen.getByRole("checkbox", { name: /Bridges/ }));
    await userEvent.click(
      screen.getByRole("button", { name: "Add 1 selected" }),
    );

    const [carried] = entries();
    expect(entries()).toHaveLength(1);
    expect(carried).toMatchObject({
      description: "Bridges",
      sets: 3,
      carriedForwardFromNoteId: "prev",
      minutes: 0,
      carryForwardReviewed: false,
      patientResponse: null,
      painBefore: null,
    });
    await userEvent.click(
      screen.getByRole("checkbox", {
        name: /Carried forward from 10\/01\/2026 — reviewed/,
      }),
    );
    expect(entries()[0].carryForwardReviewed).toBe(true);
    expect(
      screen.getByText(
        /Last visit \(10\/01\/2026\): 3x10, 12 min · Old response/,
      ),
    ).toBeInTheDocument();
  });

  it("warns, as advice only, about overlaps, missing responses and units", () => {
    const flowsheet: FlowsheetEntry[] = [
      blankEntry({
        description: "Bridges",
        startTime: "09:00",
        endTime: "09:12",
        minutes: 12,
        units: 3,
      }),
      blankEntry({
        description: "Mobs",
        startTime: "09:10",
        endTime: "09:25",
        minutes: 15,
        patientResponse: "ok",
      }),
    ];
    const s = analyze(flowsheet, "Medicare");
    expect(s.warnings.map((w) => w.code).sort()).toEqual(
      ["missing_response", "overlap", "units_mismatch"].sort(),
    );
    expect(unitsFor(27, "Medicare")).toBe(2);
    expect(unitsFor(22, "RoundedFifteenMinute")).toBe(1);
    render(<Harness initial={flowsheet} />);
    expect(within(totals()).getByText(/overlap in time/)).toBeInTheDocument();
    expect(
      screen.getByText(/nothing is billed or submitted/),
    ).toBeInTheDocument();
  });

  it("saves the current entries as a reusable group", async () => {
    vi.mocked(api.createInterventionGroup).mockResolvedValue({
      id: "g2",
      name: "Hip set",
      description: null,
      isShared: true,
      isMine: false,
      isFavorite: false,
      items: [],
    });
    render(
      <Harness
        initial={[
          blankEntry({ description: "Clamshells", category: 0, sets: 3 }),
        ]}
      />,
    );
    await userEvent.click(
      screen.getByRole("button", { name: "Save as group…" }),
    );
    await userEvent.type(screen.getByLabelText("Group name"), "Hip set");
    await userEvent.click(
      screen.getByRole("checkbox", { name: "Share with the clinic" }),
    );
    await userEvent.click(
      screen.getByRole("button", { name: "Save 1 as group" }),
    );
    await waitFor(() =>
      expect(api.createInterventionGroup).toHaveBeenCalledWith(
        expect.objectContaining({
          name: "Hip set",
          isShared: true,
          items: [expect.objectContaining({ name: "Clamshells", sets: 3 })],
        }),
      ),
    );
  });

  it("shows a signed flowsheet read-only", () => {
    render(
      <QueryClientProvider client={new QueryClient()}>
        <FlowsheetPanel
          value={[
            blankEntry({
              description: "Bridges",
              cptCode: "97110",
              sets: 3,
              repetitions: 10,
              minutes: 12,
              painBefore: 5,
              painAfter: 2,
              patientResponse: "Good",
            }),
          ]}
          onChange={() => {}}
          readOnly
          previous={null}
          ruleVariant="Medicare"
          canShareGroups={false}
        />
      </QueryClientProvider>,
    );
    expect(screen.getByText(/3x10, 12 min/)).toBeInTheDocument();
    expect(
      screen.getByText(/Pain 5→2\/10 · Response: Good/),
    ).toBeInTheDocument();
    expect(screen.queryByRole("searchbox")).not.toBeInTheDocument();
  });
});
