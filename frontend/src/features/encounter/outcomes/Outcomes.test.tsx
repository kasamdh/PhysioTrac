import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ToastProvider } from "../../../components/Toast";
import * as api from "./api";
import fixture from "./definitions.fixture.json";
import {
  compare,
  describeChange,
  scoreResponses,
  type ItemResponse,
  type OutcomeDefinition,
  type OutcomeScore,
} from "./model";
import { OutcomesWorkspace } from "./OutcomesWorkspace";

vi.mock("./api", () => ({
  fetchOutcomeDefinitions: vi.fn(),
  fetchPatientOutcomes: vi.fn(),
  recordOutcomeScore: vi.fn(),
  deleteOutcomeScore: vi.fn(),
}));

// The server's catalog, kept identical by OutcomeDefinitionsFixtureTests.
const defs = fixture as OutcomeDefinition[];
const def = (code: string) => defs.find((d) => d.code === code)!;
const all = (code: string, value: number): ItemResponse[] =>
  def(code).items.map((i) => ({ key: i.key, value }));
const score = (code: string, r: ItemResponse[]) => scoreResponses(def(code), r);

const outcome = (over: Partial<OutcomeScore>): OutcomeScore => ({
  id: crypto.randomUUID(),
  patientId: "p1",
  noteId: null,
  measure: 0,
  measuredOn: "2026-09-01",
  score: 40,
  maximumScore: 80,
  interpretation: null,
  itemResponses: null,
  notes: null,
  isLocked: true,
  ...over,
});

describe("Outcome measure scoring (mirrors the server)", () => {
  it("covers all ten measures", () => {
    expect(defs.map((d) => d.abbreviation)).toEqual([
      "LEFS",
      "ODI",
      "NDI",
      "QuickDASH",
      "PSFS",
      "BBS",
      "TUG",
      "5xSTS",
      "ABC",
      "FGA",
    ]);
  });

  it("LEFS sums 20 items out of 80", () => {
    expect(score("lefs", all("lefs", 4))).toMatchObject({
      score: 80,
      interpretation: "100% of maximum function (higher is better)",
    });
    expect(score("lefs", all("lefs", 3).slice(1)).errors).toEqual([
      "Answer all 20 items (19 answered).",
    ]);
  });

  it("ODI is a percentage of the sections answered", () => {
    expect(score("odi", all("odi", 2))).toMatchObject({
      score: 40,
      interpretation: "Moderate disability (21–40%)",
    });
    expect(score("odi", all("odi", 3).slice(0, 9)).score).toBe(60);
    expect(score("odi", all("odi", 3).slice(0, 8)).score).toBeNull();
  });

  it("NDI is out of 50, scaled when a section is skipped", () => {
    expect(score("ndi", all("ndi", 1)).score).toBe(10);
    expect(score("ndi", all("ndi", 2).slice(0, 9))).toMatchObject({
      score: 20,
      interpretation: "Moderate disability (15–24)",
    });
  });

  it("QuickDASH is (mean − 1) × 25 with 10 of 11 items", () => {
    expect(score("quickdash", all("quickdash", 3)).score).toBe(50);
    const mixed = [1, 2, 3, 4, 5, 1, 2, 3, 4, 5, 1].map((v, i) => ({
      key: `i${i + 1}`,
      value: v,
    }));
    expect(score("quickdash", mixed).score).toBe(45.5);
    expect(
      score("quickdash", all("quickdash", 2).slice(0, 9)).score,
    ).toBeNull();
  });

  it("PSFS averages the named activities", () => {
    expect(
      score("psfs", [
        { key: "activity1", value: 3, label: "Stairs" },
        { key: "activity2", value: 5, label: "Gardening" },
        { key: "activity3", value: 7, label: "Dog walks" },
      ]).score,
    ).toBe(5);
    expect(score("psfs", [{ key: "activity1", value: 4 }]).errors).toEqual([
      "Activity 1: name the activity.",
    ]);
  });

  it("Berg sums 14 tasks with fall-risk bands", () => {
    expect(score("berg", all("berg", 4)).interpretation).toBe(
      "Low fall risk (41–56)",
    );
    expect(score("berg", all("berg", 2))).toMatchObject({
      score: 28,
      interpretation: "Medium fall risk (21–40)",
    });
    expect(score("berg", all("berg", 1)).interpretation).toBe(
      "High fall risk (0–20)",
    );
  });

  it("TUG and 5xSTS average the timed trials", () => {
    expect(
      score("tug", [
        { key: "trial1", value: 12.4 },
        { key: "trial2", value: 13 },
        { key: "trial3", value: 14.2 },
      ]),
    ).toMatchObject({
      score: 13.2,
      interpretation: "Slower than typical (10–13.4 s)",
    });
    expect(
      score("5xsts", [
        { key: "trial1", value: 10.5 },
        { key: "trial2", value: 11.5 },
      ]),
    ).toMatchObject({ score: 11, interpretation: "Typical (under 12 s)" });
    expect(score("tug", [{ key: "trial1", value: 0 }]).score).toBeNull();
  });

  it("ABC is the mean confidence of 16 items", () => {
    expect(score("abc", all("abc", 60)).score).toBe(60);
    const mixed = all("abc", 90).map((r, i) =>
      i < 4 ? { ...r, value: 30 } : r,
    );
    expect(score("abc", mixed).score).toBe(75);
    expect(score("abc", all("abc", 101)).score).toBeNull();
  });

  it("FGA sums 10 tasks out of 30", () => {
    expect(score("fga", all("fga", 2))).toMatchObject({
      score: 20,
      interpretation: "Increased fall risk (22 or less, older adults)",
    });
    expect(score("fga", all("fga", 3)).score).toBe(30);
  });
});

describe("Progress comparison", () => {
  it("finds baseline, previous and current, and judges the change", () => {
    const lefs = def("lefs");
    const c = compare(lefs, [
      outcome({ measuredOn: "2026-10-01", score: 50 }),
      outcome({ measuredOn: "2026-09-01", score: 40 }),
      outcome({ measuredOn: "2026-09-15", score: 45 }),
      outcome({ measure: 1, score: 30 }),
    ])!;
    expect([c.baseline.score, c.previous!.score, c.current.score]).toEqual([
      40, 45, 50,
    ]);
    expect(c.fromBaseline!.text).toBe("+10 points — meaningful improvement");
    expect(c.fromPrevious!.text).toBe("+5 points — improved");
    // Lower is better on the ODI and TUG.
    expect(describeChange(def("odi"), 40, 52).text).toBe(
      "+12 points — meaningful decline",
    );
    expect(describeChange(def("tug"), 14, 11.5)).toMatchObject({
      text: "-2.5 seconds — improved",
      tone: "text-success",
    });
  });
});

function renderWorkspace(readOnly = false) {
  return render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <ToastProvider>
        <OutcomesWorkspace
          patientId="p1"
          noteId="n2"
          serviceDate="2026-10-07"
          readOnly={readOnly}
        />
      </ToastProvider>
    </QueryClientProvider>,
  );
}

describe("Outcomes workspace", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.fetchOutcomeDefinitions).mockResolvedValue(defs);
    vi.mocked(api.fetchPatientOutcomes).mockResolvedValue([
      outcome({ measuredOn: "2026-09-01", score: 40, noteId: "n0" }),
      outcome({
        measuredOn: "2026-10-07",
        score: 52,
        noteId: "n2",
        isLocked: false,
        interpretation: "65% of maximum function (higher is better)",
      }),
    ]);
  });

  it("scores items as they are entered and records the responses", async () => {
    vi.mocked(api.recordOutcomeScore).mockResolvedValue(
      outcome({ measure: 4, score: 13.2 }),
    );
    renderWorkspace();
    await userEvent.selectOptions(
      await screen.findByRole("combobox", { name: "Measure" }),
      "4",
    );
    const status = within(
      screen.getByRole("form", { name: "Record outcome measure" }),
    ).getByRole("status");
    expect(status).toHaveTextContent("0 of 3 items answered.");
    await userEvent.type(screen.getByLabelText("Trial 1 (seconds)"), "12.4");
    await userEvent.type(screen.getByLabelText("Trial 2 (seconds)"), "13");
    await userEvent.type(screen.getByLabelText("Trial 3 (seconds)"), "14.2");
    expect(status).toHaveTextContent(
      "TUG score: 13.2 s — Slower than typical (10–13.4 s)",
    );
    await userEvent.type(
      screen.getByLabelText("Notes (optional)"),
      "Single-point cane",
    );
    await userEvent.click(screen.getByRole("button", { name: "Record TUG" }));
    await waitFor(() =>
      expect(api.recordOutcomeScore).toHaveBeenCalledWith(
        expect.objectContaining({
          noteId: "n2",
          measure: 4,
          measuredOn: "2026-10-07",
          score: null,
          notes: "Single-point cane",
          itemResponses: expect.arrayContaining([
            expect.objectContaining({ key: "trial1", value: 12.4 }),
          ]),
        }),
      ),
    );
  });

  it("compares baseline and current with a trend chart and history", async () => {
    renderWorkspace();
    const card = await screen.findByRole("listitem", {
      name: "Lower Extremity Functional Scale",
    });
    expect(card).toHaveTextContent("recorded this visit");
    expect(card).toHaveTextContent("Baseline40/80 09/01/2026");
    expect(card).toHaveTextContent("Current52/80 10/07/2026");
    expect(card).toHaveTextContent(
      "Change from baseline+12 points — meaningful improvement",
    );
    expect(card).toHaveTextContent(
      "Interpretation: 65% of maximum function (higher is better)",
    );
    expect(
      within(card).getByRole("img", {
        name: /LEFS trend: 40 on 09\/01\/2026, 52 on 10\/07\/2026/,
      }),
    ).toBeInTheDocument();
    await userEvent.click(within(card).getByText("Score history (2)"));
    // Only this visit's unsigned score can be removed.
    expect(
      within(card).getAllByRole("button", { name: /^Remove/ }),
    ).toHaveLength(1);
  });

  it("is read-only on the patient's documentation", async () => {
    renderWorkspace(true);
    await screen.findByRole("listitem", {
      name: "Lower Extremity Functional Scale",
    });
    expect(
      screen.queryByRole("form", { name: "Record outcome measure" }),
    ).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^Remove/ })).toBeNull();
  });
});
