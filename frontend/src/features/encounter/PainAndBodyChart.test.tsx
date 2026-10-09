import { fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { BodyChart } from "./bodychart/BodyChart";
import {
  BodySide,
  BodyView,
  centerOf,
  describe as describeFinding,
  sideForPoint,
  REGIONS,
} from "./bodychart/regions";
import { PainAssessmentPanel } from "./PainAssessmentPanel";
import { emptyPain } from "./pain";
import type { BodyFinding, PainAssessment } from "./types";

function ChartHarness({
  initial = [] as BodyFinding[],
  previous,
}: {
  initial?: BodyFinding[];
  previous?: BodyFinding[];
}) {
  const [findings, setFindings] = useState(initial);
  return (
    <>
      <BodyChart
        findings={findings}
        onChange={setFindings}
        readOnly={false}
        previous={previous}
        previousDate="10/01/2026"
      />
      <output data-testid="state">{JSON.stringify(findings)}</output>
    </>
  );
}
const state = () =>
  JSON.parse(screen.getByTestId("state").textContent!) as BodyFinding[];

// jsdom has no layout: give the drawings a 160 x 360 box at the origin.
beforeEach(() => {
  vi.spyOn(SVGElement.prototype, "getBoundingClientRect").mockReturnValue({
    left: 0,
    top: 0,
    width: 160,
    height: 360,
    right: 160,
    bottom: 360,
    x: 0,
    y: 0,
    toJSON: () => ({}),
  } as DOMRect);
});

describe("Body chart", () => {
  it("marks a finding where the body is tapped, with the region and the patient's side", async () => {
    render(<ChartHarness />);
    await userEvent.click(screen.getByRole("radio", { name: /Numbness/ }));
    const front = screen.getByRole("img", { name: /front view/ });
    // The viewer's left knee on the front view is the patient's right knee.
    const knee = front.querySelectorAll('[data-region="knee"]')[0];
    fireEvent.pointerDown(knee, { clientX: 69, clientY: 233, pointerId: 1 });

    const [f] = state();
    expect(f).toMatchObject({
      view: BodyView.Front,
      region: "knee",
      side: BodySide.Right,
      findingType: 1,
    });
    expect(f.x).toBeCloseTo(69 / 160, 3);
    expect(f.y).toBeCloseTo(233 / 360, 3);
    expect(
      screen.getByRole("button", {
        name: /Finding 1: Numbness: right knee \(front\)/,
      }),
    ).toBeInTheDocument();
  });

  it("can be completed from the findings table alone (keyboard / screen reader)", async () => {
    render(<ChartHarness />);
    await userEvent.click(
      screen.getByRole("button", { name: "+ Add finding" }),
    );
    await userEvent.selectOptions(
      screen.getByLabelText("Finding 1 view"),
      String(BodyView.Back),
    );
    await userEvent.selectOptions(
      screen.getByLabelText("Finding 1 region"),
      "lowBack",
    );
    await userEvent.selectOptions(
      screen.getByLabelText("Finding 1 side"),
      String(BodySide.Left),
    );
    await userEvent.selectOptions(screen.getByLabelText("Finding 1 type"), "8");
    await userEvent.type(
      screen.getByLabelText("Finding 1 severity, 0 to 10"),
      "7",
    );
    await userEvent.type(
      screen.getByLabelText("Finding 1 radiates to"),
      "Left posterior thigh",
    );

    const [f] = state();
    expect(f).toMatchObject({
      view: BodyView.Back,
      region: "lowBack",
      side: BodySide.Left,
      findingType: 8,
      severity: 7,
      radiatesTo: "Left posterior thigh",
    });
    expect(f).toMatchObject(centerOf(BodyView.Back, "lowBack", BodySide.Left));
    await userEvent.click(
      screen.getByRole("button", { name: "Remove finding 1" }),
    );
    expect(state()).toEqual([]);
  });

  it("overlays last visit's findings on request", async () => {
    const previous: BodyFinding[] = [
      {
        view: 0,
        region: "knee",
        side: 1,
        x: 0.43,
        y: 0.65,
        findingType: 0,
        severity: 7,
        radiatesTo: null,
        annotation: null,
        comment: null,
      },
    ];
    render(<ChartHarness previous={previous} />);
    expect(
      screen.queryByText(/Last visit \(10\/01\/2026\)/),
    ).not.toBeInTheDocument();
    await userEvent.click(
      screen.getByRole("checkbox", { name: /Show last visit’s findings/ }),
    );
    expect(
      screen.getByText("Pain: right knee (front), severity 7/10"),
    ).toBeInTheDocument();
  });

  it("is read-only after signing: a list, no editing controls", () => {
    const findings: BodyFinding[] = [
      {
        view: 1,
        region: "lowBack",
        side: 0,
        x: 0.45,
        y: 0.35,
        findingType: 0,
        severity: 5,
        radiatesTo: null,
        annotation: null,
        comment: null,
      },
    ];
    render(<BodyChart findings={findings} readOnly />);
    expect(
      screen.queryByRole("button", { name: "+ Add finding" }),
    ).not.toBeInTheDocument();
    expect(screen.queryByRole("radiogroup")).not.toBeInTheDocument();
    expect(
      within(
        screen.getByRole("list", { name: "Body-chart findings" }),
      ).getByText("Pain: left low back (back), severity 5/10"),
    ).toBeInTheDocument();
  });

  it("knows the patient's side on each view", () => {
    const chest = REGIONS[BodyView.Front].find((r) => r.key === "chest")!;
    const lowBack = REGIONS[BodyView.Back].find((r) => r.key === "lowBack")!;
    expect(sideForPoint(chest, BodyView.Front, 0.4)).toBe(BodySide.Right); // viewer's left on the front
    expect(sideForPoint(lowBack, BodyView.Back, 0.4)).toBe(BodySide.Left); // viewer's left on the back
    expect(sideForPoint(chest, BodyView.Front, 0.5)).toBe(BodySide.Midline);
    expect(
      describeFinding({
        view: 0,
        region: "neck",
        side: 3,
        x: 0.5,
        y: 0.15,
        findingType: 5,
        severity: null,
        radiatesTo: null,
        annotation: "C5-6",
        comment: null,
      }),
    ).toBe("Tenderness: midline neck (front), C5-6");
  });
});

function PainHarness({ previous }: { previous?: PainAssessment }) {
  const [pain, setPain] = useState<PainAssessment>(emptyPain());
  return (
    <>
      <PainAssessmentPanel
        value={pain}
        onChange={setPain}
        readOnly={false}
        previous={previous}
      />
      <output data-testid="pain">{JSON.stringify(pain)}</output>
    </>
  );
}
const painState = () =>
  JSON.parse(screen.getByTestId("pain").textContent!) as PainAssessment;

describe("Pain assessment", () => {
  it("records ratings, quality and behaviour, showing last visit beside each rating", async () => {
    render(<PainHarness previous={{ ...emptyPain(), current: 7 }} />);
    expect(screen.getByText(/last visit 7\/10/)).toBeInTheDocument();
    await userEvent.click(
      within(
        screen.getByRole("radiogroup", { name: /Current pain/ }),
      ).getByRole("radio", { name: "5" }),
    );
    await userEvent.click(
      within(
        screen.getByRole("radiogroup", { name: /After treatment/ }),
      ).getByRole("radio", { name: "2" }),
    );
    await userEvent.click(screen.getByRole("button", { name: "Sharp" }));
    await userEvent.click(
      within(
        screen.getByRole("radiogroup", { name: "Irritability" }),
      ).getByRole("radio", { name: "High" }),
    );
    await userEvent.type(screen.getByLabelText("Sleep impact"), "Wakes twice");

    expect(painState()).toMatchObject({
      current: 5,
      afterTreatment: 2,
      qualities: ["Sharp"],
      irritability: 2,
      sleepImpact: "Wakes twice",
    });
  });

  it("offers each scale's own values", async () => {
    render(<PainHarness />);
    await userEvent.selectOptions(screen.getByLabelText("Pain scale"), "2");
    expect(
      within(screen.getByRole("radiogroup", { name: /Current pain/ }))
        .getAllByRole("radio")
        .map((r) => r.textContent),
    ).toEqual(["0", "2", "4", "6", "8", "10"]);
    await userEvent.selectOptions(screen.getByLabelText("Pain scale"), "3");
    await userEvent.click(
      within(screen.getByRole("radiogroup", { name: /Worst pain/ })).getByRole(
        "radio",
        { name: "Severe" },
      ),
    );
    expect(painState()).toMatchObject({ scale: 3, worst: 3 });
  });
});
