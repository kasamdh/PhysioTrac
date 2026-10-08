import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MeasurementsPanel } from "./MeasurementsPanel";
import { SpecialTestsPanel } from "./SpecialTestsPanel";
import {
  comparison,
  measurementKey,
  mmtScore,
  type Measurement,
  type MeasurementHistory,
} from "./model";
import * as api from "./api";
import type {
  SpecialTest,
  SpecialTestDefinition,
  SpecialTestHistory,
} from "./types";

vi.mock("./api", () => ({
  fetchSpecialTests: vi.fn(),
  setSpecialTestFavorite: vi.fn(),
}));

function Harness({
  initial = [],
  history = [],
}: {
  initial?: Measurement[];
  history?: MeasurementHistory[];
}) {
  const [rows, setRows] = useState(initial);
  return (
    <>
      <MeasurementsPanel
        value={rows}
        onChange={setRows}
        readOnly={false}
        history={history}
      />
      <output data-testid="rows">{JSON.stringify(rows)}</output>
    </>
  );
}
const rows = () =>
  JSON.parse(screen.getByTestId("rows").textContent!) as Measurement[];

describe("Objective measurements", () => {
  it("adds a region's motions and stores AROM and PROM as separate measurements with units", async () => {
    render(<Harness />);
    await userEvent.click(
      screen.getByRole("button", { name: "+ Add Knee motions" }),
    );
    await userEvent.type(
      screen.getByLabelText("Knee Flexion AROM degrees"),
      "100",
    );
    await userEvent.type(
      screen.getByLabelText("Knee Flexion PROM degrees"),
      "108",
    );
    await userEvent.selectOptions(
      screen.getAllByLabelText("End feel")[0],
      "Firm",
    );

    const flexion = rows().filter(
      (r) => r.item === "Knee" && r.movement === "Flexion",
    );
    expect(flexion).toEqual([
      expect.objectContaining({
        category: 0,
        mode: "AROM",
        numericValue: 100,
        unit: "deg",
        side: 1,
      }),
      expect.objectContaining({
        category: 0,
        mode: "PROM",
        numericValue: 108,
        unit: "deg",
        endFeel: "Firm",
      }),
    ]);
    expect(screen.getAllByText(/Normal 135 deg/).length).toBeGreaterThan(0);
  });

  it("records strength by manual muscle test or dynamometer, with pain and compensation", async () => {
    render(<Harness />);
    await userEvent.click(
      screen.getByRole("button", { name: "+ Add strength" }),
    );
    await userEvent.type(
      screen.getByLabelText("Muscle or movement"),
      "Quadriceps",
    );
    await userEvent.selectOptions(screen.getByLabelText("MMT grade"), "4-");
    await userEvent.click(screen.getByRole("checkbox", { name: "Painful" }));
    await userEvent.type(screen.getByLabelText("Compensation"), "Hip hike");
    expect(rows()[0]).toMatchObject({
      category: 1,
      item: "Quadriceps",
      mode: "MMT",
      textValue: "4-",
      painful: true,
      compensation: "Hip hike",
    });

    await userEvent.selectOptions(
      screen.getByLabelText("Method"),
      "Dynamometer",
    );
    await userEvent.type(screen.getByLabelText("Force"), "42");
    await userEvent.selectOptions(screen.getByLabelText("Force unit"), "kg");
    expect(rows()[0]).toMatchObject({
      mode: "Dynamometer",
      numericValue: 42,
      unit: "kg",
      textValue: null,
    });
  });

  it("covers neurological, gait, balance and functional tests", async () => {
    render(<Harness />);
    await userEvent.click(
      screen.getByRole("button", { name: "+ Add neurological" }),
    );
    await userEvent.selectOptions(
      screen.getByLabelText("Neurological test"),
      "3",
    );
    await userEvent.type(screen.getByLabelText("Level or item"), "L4");
    await userEvent.type(screen.getByLabelText("Result"), "Impaired");
    await userEvent.click(screen.getByRole("button", { name: "+ Add gait" }));
    await userEvent.selectOptions(screen.getByLabelText("Gait item"), "Speed");
    await userEvent.type(screen.getByLabelText("Value"), "0.9");
    await userEvent.selectOptions(screen.getByLabelText("Value unit"), "m/s");
    await userEvent.type(screen.getByLabelText("Assistive device"), "Cane");
    await userEvent.click(
      screen.getByRole("button", { name: "+ Add balance" }),
    );
    await userEvent.selectOptions(screen.getByLabelText("Eyes"), "Eyes closed");
    await userEvent.type(screen.getByLabelText("Time"), "12");
    await userEvent.click(
      screen.getByRole("checkbox", { name: "Loss of balance" }),
    );
    await userEvent.click(
      screen.getByRole("button", { name: "+ Add functional test" }),
    );
    await userEvent.selectOptions(screen.getByLabelText("Activity"), "Stairs");

    const byCat = (c: number) => rows().find((r) => r.category === c)!;
    expect(byCat(3)).toMatchObject({ item: "L4", textValue: "Impaired" });
    expect(byCat(9)).toMatchObject({
      item: "Speed",
      numericValue: 0.9,
      unit: "m/s",
      assistiveDevice: "Cane",
    });
    expect(byCat(10)).toMatchObject({
      condition: "Eyes closed",
      numericValue: 12,
      unit: "sec",
      textValue: "Loss of balance",
    });
    expect(byCat(11)).toMatchObject({ item: "Stairs" });
  });

  it("shows baseline, previous and change for the same measurement", () => {
    const current: Measurement = {
      category: 0,
      item: "Knee",
      movement: "Flexion",
      side: 1,
      mode: "AROM",
      numericValue: 110,
      unit: "deg",
    };
    const history: MeasurementHistory[] = [
      {
        category: 0,
        item: "Knee",
        movement: "Flexion",
        side: 1,
        mode: "AROM",
        unit: "deg",
        baseline: {
          serviceDate: "2026-09-01",
          numericValue: 85,
          textValue: null,
        },
        previous: {
          serviceDate: "2026-09-15",
          numericValue: 100,
          textValue: null,
        },
      },
    ];
    render(<Harness initial={[current]} history={history} />);
    expect(
      screen.getByText(
        /AROM: Baseline 85 deg \(09\/01\/26\) · Previous 100 deg \(09\/15\/26\) · \+25 deg since baseline/,
      ),
    ).toBeInTheDocument();
    expect(measurementKey(current)).toBe(measurementKey(history[0]));
    expect(mmtScore("4-")).toBeCloseTo(3.667, 2);
    expect(
      comparison(
        {
          ...history[0],
          mode: "MMT",
          baseline: {
            serviceDate: "2026-09-01",
            numericValue: null,
            textValue: "3+",
          },
          previous: null,
        },
        { textValue: "4" },
        null,
        "MMT",
      ),
    ).toBe("Baseline 3+ (09/01/26) · +2/3 grade since baseline");
  });

  it("is read-only after signing", () => {
    render(
      <MeasurementsPanel
        value={[
          {
            category: 1,
            item: "Quadriceps",
            side: 1,
            mode: "MMT",
            textValue: "4",
            painful: true,
          },
        ]}
        onChange={() => {}}
        readOnly
        history={[]}
      />,
    );
    expect(
      screen.getByText(/Quadriceps \(Right\) — 4, painful/),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: /\+ Add/ }),
    ).not.toBeInTheDocument();
  });
});

const lachman: SpecialTestDefinition = {
  id: "d1",
  code: "lachman",
  name: "Lachman test",
  specialty: 1,
  bodyRegion: "Knee",
  description: null,
  resultKind: 0,
  unit: null,
  interpretationGuide:
    "Increased translation may be consistent with ACL insufficiency.",
  contraindicationWarning: null,
  isActive: true,
  isSystem: true,
  isFavorite: false,
};
const dix: SpecialTestDefinition = {
  ...lachman,
  id: "d2",
  code: "dix-hallpike",
  name: "Dix-Hallpike test",
  specialty: 7,
  bodyRegion: "Vestibular",
  contraindicationWarning: "Screen for cervical instability first.",
  isFavorite: true,
};
const hop: SpecialTestDefinition = {
  ...lachman,
  id: "d3",
  code: "single-hop",
  name: "Single-leg hop",
  resultKind: 1,
  unit: "cm",
};

function TestsHarness({ history = [] }: { history?: SpecialTestHistory[] }) {
  const [tests, setTests] = useState<SpecialTest[]>([]);
  return (
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <SpecialTestsPanel
        value={tests}
        onChange={setTests}
        readOnly={false}
        history={history}
      />
      <output data-testid="tests">
        {JSON.stringify(tests.map(({ definition: _d, ...t }) => t))}
      </output>
    </QueryClientProvider>
  );
}
const tests = () =>
  JSON.parse(screen.getByTestId("tests").textContent!) as SpecialTest[];

describe("Special tests", () => {
  beforeEach(() =>
    vi.mocked(api.fetchSpecialTests).mockResolvedValue([dix, lachman, hop]),
  );

  it("picks a test from the library and records the therapist's result and interpretation", async () => {
    render(
      <TestsHarness
        history={[
          {
            testName: "Lachman test",
            side: 1,
            serviceDate: "2026-09-01",
            outcome: 1,
            numericValue: null,
            unit: null,
          },
        ]}
      />,
    );
    await userEvent.click(
      await screen.findByRole("button", { name: /^Lachman test/ }),
    );
    await userEvent.selectOptions(
      screen.getByRole("combobox", { name: "Side" }),
      "1",
    );
    await userEvent.click(
      within(screen.getByRole("radiogroup", { name: "Result" })).getByRole(
        "radio",
        { name: "Negative" },
      ),
    );
    await userEvent.type(
      screen.getByLabelText("Interpretation (therapist)"),
      "Firm end feel",
    );

    expect(tests()[0]).toMatchObject({
      testName: "Lachman test",
      definitionId: "d1",
      side: 1,
      outcome: 2,
      interpretation: "Firm end feel",
    });
    expect(
      screen.getByText(/Last result \(09\/01\/2026\): Positive/),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Interpretation guide (reference only)"),
    ).toBeInTheDocument();
  });

  it("shows a test's precaution before it is recorded, and numeric results with their unit", async () => {
    render(<TestsHarness />);
    await userEvent.click(
      await screen.findByRole("button", { name: /^Dix-Hallpike test/ }),
    );
    expect(screen.getByRole("note")).toHaveTextContent(
      "Before testing: Screen for cervical instability first.",
    );
    await userEvent.click(
      screen.getByRole("button", { name: /^Single-leg hop/ }),
    );
    await userEvent.type(screen.getByLabelText("Result (cm)"), "92");
    expect(tests()[1]).toMatchObject({ numericValue: 92, unit: "cm" });
  });

  it("searches the library and toggles favorites", async () => {
    vi.mocked(api.setSpecialTestFavorite).mockResolvedValue(undefined);
    render(<TestsHarness />);
    await userEvent.type(await screen.findByLabelText("Find a test"), "knee");
    await waitFor(() =>
      expect(api.fetchSpecialTests).toHaveBeenLastCalledWith(
        expect.objectContaining({ search: "knee" }),
      ),
    );
    await userEvent.click(
      screen.getByRole("button", { name: "Add Lachman test to favorites" }),
    );
    expect(api.setSpecialTestFavorite).toHaveBeenCalledWith("d1", true);
  });
});
