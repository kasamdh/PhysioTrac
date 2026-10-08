import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ToastProvider } from "../../../components/Toast";
import * as api from "./api";
import { GoalProgressSummary, GoalTracker } from "./GoalTracker";
import {
  narrative,
  progressPercent,
  statement,
  wordingAdvice,
  type Goal,
  type GoalProgressRow,
} from "./model";

vi.mock("./api", () => ({
  goalsKey: (id: string) => ["chart", "goals", id],
  fetchPatientGoals: vi.fn(),
  createPatientGoal: vi.fn(),
  editGoal: vi.fn(),
  approvePatientGoal: vi.fn(),
  fetchGoalHistory: vi.fn(),
}));

const goal = (over: Partial<Goal>): Goal => ({
  id: "g1",
  patientId: "p1",
  functionalLimitation: "Unable to climb stairs without pain",
  functionalTask: "Climb 12 stairs reciprocally with one rail",
  term: 0,
  baselineValue: 4,
  targetValue: 12,
  currentValue: null,
  unit: "stairs",
  measurementMethod: "Stair count",
  targetDate: "2026-11-30",
  status: 4,
  progressPercent: null,
  approvedById: "u1",
  approvedAt: "2026-10-01T12:00:00Z",
  planOfCareId: null,
  comments: null,
  version: 1,
  ...over,
});

function Harness({
  initial = [],
  canApprove = true,
}: {
  initial?: GoalProgressRow[];
  canApprove?: boolean;
}) {
  const [rows, setRows] = useState(initial);
  const [assessment, setAssessment] = useState("");
  return (
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <ToastProvider>
        <GoalTracker
          patientId="p1"
          value={rows}
          onChange={setRows}
          readOnly={false}
          canApprove={canApprove}
          insertTarget={{
            label: "Progress toward goals",
            insert: (t) => setAssessment((a) => (a ? `${a}\n${t}` : t)),
          }}
        />
        <output data-testid="rows">{JSON.stringify(rows)}</output>
        <output data-testid="assessment">{assessment}</output>
      </ToastProvider>
    </QueryClientProvider>
  );
}
const rows = () =>
  JSON.parse(screen.getByTestId("rows").textContent!) as GoalProgressRow[];

describe("Goal model", () => {
  it("computes progress both ways and reads goals as measurable sentences", () => {
    expect(progressPercent(4, 12, 8)).toBe(50);
    expect(progressPercent(60, 20, 30)).toBe(75); // a goal that goes down (e.g. TUG seconds)
    expect(progressPercent(4, 12, 20)).toBe(100);
    expect(progressPercent(4, 12, null)).toBeNull();
    expect(statement(goal({}))).toBe(
      "Patient will climb 12 stairs reciprocally with one rail (baseline 4 → target 12 stairs, measured by Stair count) by 11/30/2026.",
    );
  });

  it("advises on vague or unmeasurable wording without blocking", () => {
    expect(
      wordingAdvice("Improve walking", "", "2026-10-01", "2026-10-08"),
    ).toHaveLength(4);
    expect(
      wordingAdvice(
        "Walk 300 ft independently on level ground",
        "Distance",
        "2026-12-01",
        "2026-10-08",
      ),
    ).toEqual([]);
  });

  it("builds the narrative from the goals ticked for the note", () => {
    const base = {
      term: 0,
      functionalTask: "Climb 12 stairs.",
      baselineValue: 4,
      targetValue: 12,
      unit: "stairs",
      targetDate: "2026-11-30",
    };
    expect(
      narrative([
        {
          ...base,
          goalId: "a",
          currentValue: 8,
          previousValue: 6,
          status: 1,
          comment: "Uses one rail.",
          includeInNarrative: true,
        },
        {
          ...base,
          goalId: "b",
          currentValue: 9,
          status: 1,
          comment: null,
          includeInNarrative: false,
        },
      ]),
    ).toBe(
      "STG 1: Climb 12 stairs; baseline 4 stairs; previous 6; today 8; target 12 by 11/30/2026; 50% toward target; in progress. Uses one rail.",
    );
  });
});

describe("Goal tracker", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.fetchPatientGoals).mockResolvedValue([
      goal({}),
      goal({
        id: "g2",
        functionalTask: "Walk 300 ft without a device",
        status: 0,
        baselineValue: 50,
        targetValue: 300,
        unit: "ft",
      }),
      goal({
        id: "g3",
        functionalTask: "Sit to stand without hands",
        status: 2,
        currentValue: 5,
        targetValue: 5,
        baselineValue: 0,
        unit: "reps",
      }),
    ]);
  });

  it("records this visit's value, moving a not-started goal to in progress", async () => {
    render(<Harness />);
    const card = await screen.findByRole("listitem", {
      name: "Climb 12 stairs reciprocally with one rail",
    });
    await userEvent.type(within(card).getByLabelText("Today (stairs)"), "8");
    expect(rows()).toEqual([
      expect.objectContaining({
        goalId: "g1",
        currentValue: 8,
        status: 1,
        includeInNarrative: true,
        functionalTask: "Climb 12 stairs reciprocally with one rail",
      }),
    ]);
    expect(
      within(card).getByRole("progressbar", {
        name: "Climb 12 stairs reciprocally with one rail progress",
      }),
    ).toHaveAttribute("aria-valuenow", "50");
    await userEvent.type(within(card).getByLabelText("Comment"), "One rail");
    await userEvent.selectOptions(within(card).getByLabelText("Status"), "5");
    expect(rows()[0]).toMatchObject({ status: 5, comment: "One rail" });
  });

  it("warns when a goal is marked met short of its target", async () => {
    render(<Harness />);
    const card = await screen.findByRole("listitem", {
      name: "Climb 12 stairs reciprocally with one rail",
    });
    await userEvent.type(within(card).getByLabelText("Today (stairs)"), "9");
    await userEvent.selectOptions(within(card).getByLabelText("Status"), "2");
    expect(card).toHaveTextContent(
      "Marked met, but today's value hasn't reached the target.",
    );
  });

  it("inserts the ticked goals' progress into the note", async () => {
    render(<Harness />);
    const card = await screen.findByRole("listitem", {
      name: "Climb 12 stairs reciprocally with one rail",
    });
    const insert = screen.getByRole("button", {
      name: "Insert goal progress into “Progress toward goals”",
    });
    expect(insert).toBeDisabled();
    await userEvent.type(within(card).getByLabelText("Today (stairs)"), "8");
    await userEvent.click(insert);
    expect(screen.getByTestId("assessment")).toHaveTextContent(
      "STG 1: Climb 12 stairs reciprocally with one rail; baseline 4 stairs; today 8; target 12 by 11/30/2026; 50% toward target; in progress.",
    );
    await userEvent.click(
      within(card).getByRole("checkbox", { name: "Include in note" }),
    );
    expect(insert).toBeDisabled();
  });

  it("asks for approval of draft goals and keeps closed goals apart", async () => {
    vi.mocked(api.approvePatientGoal).mockResolvedValue(goal({ id: "g2" }));
    render(<Harness />);
    const draft = await screen.findByRole("listitem", {
      name: "Walk 300 ft without a device",
    });
    expect(within(draft).queryByLabelText("Today (ft)")).toBeNull();
    await userEvent.click(
      within(draft).getByRole("button", { name: "Approve goal" }),
    );
    await waitFor(() =>
      expect(api.approvePatientGoal).toHaveBeenCalledWith("g2"),
    );
    expect(
      screen.getByText("Met, partially met and discontinued goals (1)"),
    ).toBeInTheDocument();
  });

  it("shows a goal's history and edits it as a new version", async () => {
    vi.mocked(api.fetchGoalHistory).mockResolvedValue([
      {
        id: "h1",
        kind: 0,
        goalVersion: 1,
        noteId: null,
        noteServiceDate: null,
        recordedByName: "Alex Rivera",
        recordedAt: "2026-10-01T12:00:00Z",
        status: 0,
        currentValue: null,
        progressPercent: null,
        comment: null,
        snapshot: { ...goal({}), comments: null },
      },
      {
        id: "h2",
        kind: 3,
        goalVersion: 1,
        noteId: "n1",
        noteServiceDate: "2026-10-05",
        recordedByName: "Alex Rivera",
        recordedAt: "2026-10-05T12:00:00Z",
        status: 1,
        currentValue: 8,
        progressPercent: 50,
        comment: "One rail",
        snapshot: { ...goal({}), comments: null },
      },
    ]);
    vi.mocked(api.editGoal).mockResolvedValue(goal({ version: 2 }));
    render(<Harness />);
    const card = await screen.findByRole("listitem", {
      name: "Climb 12 stairs reciprocally with one rail",
    });
    await userEvent.click(
      within(card).getByRole("button", { name: "History" }),
    );
    expect(
      await within(card).findByText(
        /Value 8 stairs \(50%\) · In progress · note of 10\/05\/2026 — One rail/,
      ),
    ).toBeInTheDocument();

    await userEvent.click(
      within(card).getByRole("button", { name: "Edit goal" }),
    );
    const form = within(card).getByRole("form", { name: "Edit goal" });
    expect(form).toHaveTextContent("Saving creates version 2");
    const task = within(form).getByLabelText("Functional goal");
    await userEvent.clear(task);
    await userEvent.type(task, "Improve stairs");
    expect(form).toHaveTextContent("Include a measurable amount");
    await userEvent.click(
      within(form).getByRole("button", { name: "Save changes" }),
    );
    await waitFor(() =>
      expect(api.editGoal).toHaveBeenCalledWith(
        "g1",
        expect.objectContaining({
          functionalTask: "Improve stairs",
          targetValue: 12,
        }),
      ),
    );
  });

  it("shows a signed note's goal progress as documented", () => {
    render(
      <GoalProgressSummary
        rows={[
          {
            goalId: "g1",
            currentValue: 8,
            status: 1,
            comment: "One rail",
            includeInNarrative: true,
            term: 0,
            functionalTask: "Climb 12 stairs",
            baselineValue: 4,
            targetValue: 12,
            unit: "stairs",
            targetDate: "2026-11-30",
            previousValue: 6,
          },
        ]}
      />,
    );
    expect(
      screen.getByText(/Baseline 4 → previous 6 → this visit/).textContent,
    ).toBe(
      "Baseline 4 → previous 6 → this visit 8 → target 12 stairs by 11/30/2026 · 50% · In progress",
    );
  });
});
