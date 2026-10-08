import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState } from "react";
import { AiDraftAssist } from "./AiDraftAssist";
import * as api from "./api";

vi.mock("./api", () => ({
  fetchAiStatus: vi.fn(),
  draftWithAi: vi.fn(),
  recordAiInserted: vi.fn(),
}));

const draft = {
  section: 0,
  text: "Pain decreased from 6/10 to 3/10. [Clinician: state skilled need.]",
  provider: "Mock",
  generatedAt: "2026-10-08T15:00:00Z",
  notice: "AI never signs or finalizes documentation.",
};

function Host({ initial }: { initial: string }) {
  const [text, setText] = useState(initial);
  return (
    <>
      <label>
        Assessment
        <textarea value={text} onChange={(e) => setText(e.target.value)} />
      </label>
      <AiDraftAssist
        noteId="n1"
        section="assessment"
        currentText={text}
        onInsert={setText}
      />
    </>
  );
}

function setup(initial = "", enabled = true) {
  vi.mocked(api.fetchAiStatus).mockResolvedValue({
    enabled,
    provider: enabled ? "Mock" : "None",
  });
  vi.mocked(api.draftWithAi).mockResolvedValue(draft);
  vi.mocked(api.recordAiInserted).mockResolvedValue(undefined);
  render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <Host initial={initial} />
    </QueryClientProvider>,
  );
}

describe("AiDraftAssist", () => {
  afterEach(() => vi.clearAllMocks());

  it("shows a suggestion apart from the note until the clinician inserts it", async () => {
    setup();
    await userEvent.click(
      await screen.findByRole("button", {
        name: "Draft assessment with AI",
      }),
    );
    const panel = await screen.findByRole("region", {
      name: "AI suggestion for the assessment",
    });
    expect(panel).toHaveTextContent("AI suggestion (Mock)");
    expect(panel).toHaveTextContent("AI never signs");
    expect(screen.getByLabelText("Assessment")).toHaveValue("");
    expect(api.recordAiInserted).not.toHaveBeenCalled();

    await userEvent.click(
      screen.getByRole("button", { name: "Insert into note" }),
    );
    expect(screen.getByLabelText("Assessment")).toHaveValue(draft.text);
    expect(api.recordAiInserted).toHaveBeenCalledWith("n1", "assessment");
    expect(screen.getByRole("status")).toHaveTextContent(
      "review and edit it before signing",
    );
  });

  it("adds below the clinician's own text, or discards", async () => {
    setup("My own words.");
    await userEvent.click(
      await screen.findByRole("button", { name: "Draft assessment with AI" }),
    );
    await userEvent.click(
      await screen.findByRole("button", { name: "Discard" }),
    );
    expect(screen.queryByRole("region")).not.toBeInTheDocument();
    expect(screen.getByLabelText("Assessment")).toHaveValue("My own words.");

    await userEvent.click(
      screen.getByRole("button", { name: "Draft assessment with AI" }),
    );
    await userEvent.click(
      await screen.findByRole("button", { name: "Add below my text" }),
    );
    expect(screen.getByLabelText("Assessment")).toHaveValue(
      `My own words.\n${draft.text}`,
    );
  });

  it("is hidden when AI is turned off", async () => {
    setup("", false);
    await waitFor(() => expect(api.fetchAiStatus).toHaveBeenCalled());
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });
});
