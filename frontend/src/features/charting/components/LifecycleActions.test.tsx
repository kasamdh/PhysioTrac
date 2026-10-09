import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ToastProvider } from "../../../components/Toast";
import * as api from "../api";
import type { NoteActions } from "../types";
import { ReviewActions, VoidNoteForm } from "./LifecycleActions";

vi.mock("../api", () => ({
  startNoteReview: vi.fn(),
  returnNote: vi.fn(),
  voidNote: vi.fn(),
}));

const none: NoteActions = {
  canEdit: false,
  canSign: false,
  canCosign: false,
  canAddAddendum: false,
  canAmend: false,
  canLock: false,
};

function renderWith(ui: React.ReactNode) {
  render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <ToastProvider>{ui}</ToastProvider>
    </QueryClientProvider>,
  );
}

describe("Review and void actions", () => {
  beforeEach(() => vi.clearAllMocks());

  it("lets a supervising PT start the review and return a note with a reason", async () => {
    vi.mocked(api.startNoteReview).mockResolvedValue({} as never);
    vi.mocked(api.returnNote).mockResolvedValue({} as never);
    renderWith(
      <ReviewActions
        noteId="n1"
        actions={{ ...none, canStartReview: true, canReturn: true }}
      />,
    );
    await userEvent.click(screen.getByRole("button", { name: "Start review" }));
    await waitFor(() => expect(api.startNoteReview).toHaveBeenCalledWith("n1"));

    await userEvent.click(
      screen.getByRole("button", { name: "Return for correction…" }),
    );
    const send = screen.getByRole("button", { name: "Return to author" });
    expect(send).toBeDisabled();
    await userEvent.type(
      screen.getByLabelText(/What needs correcting/),
      "Add the patient's response to gait training.",
    );
    await userEvent.click(send);
    await waitFor(() =>
      expect(api.returnNote).toHaveBeenCalledWith(
        "n1",
        "Add the patient's response to gait training.",
      ),
    );
  });

  it("shows nothing to people who can't review", () => {
    renderWith(<ReviewActions noteId="n1" actions={none} />);
    expect(screen.queryByText("Supervising PT review")).toBeNull();
  });

  it("voids a draft with a reason and no password", async () => {
    vi.mocked(api.voidNote).mockResolvedValue({} as never);
    renderWith(
      <VoidNoteForm noteId="n1" actions={{ ...none, canVoid: true }} />,
    );
    await userEvent.click(
      screen.getByRole("button", { name: "Void this note…" }),
    );
    expect(screen.queryByLabelText(/Your password/)).toBeNull();
    const submit = screen.getByRole("button", { name: "Void note" });
    expect(submit).toBeDisabled();
    await userEvent.type(screen.getByLabelText("Reason"), "Wrong patient.");
    await userEvent.click(submit);
    await waitFor(() =>
      expect(api.voidNote).toHaveBeenCalledWith("n1", "Wrong patient.", null),
    );
  });

  it("asks for the password to void a signed note", async () => {
    vi.mocked(api.voidNote).mockRejectedValue(
      new Error("The password is incorrect."),
    );
    renderWith(
      <VoidNoteForm
        noteId="n1"
        actions={{ ...none, canVoid: true, voidNeedsPassword: true }}
      />,
    );
    await userEvent.click(
      screen.getByRole("button", { name: "Void this note…" }),
    );
    expect(
      screen.getByText(/A plan of care it created is voided too/),
    ).toBeInTheDocument();
    await userEvent.type(screen.getByLabelText("Reason"), "Duplicate note.");
    const submit = screen.getByRole("button", { name: "Void note" });
    expect(submit).toBeDisabled();
    await userEvent.type(screen.getByLabelText(/Your password/), "wrong");
    await userEvent.click(submit);
    expect(
      await screen.findByText("The password is incorrect."),
    ).toBeInTheDocument();
    expect(api.voidNote).toHaveBeenCalledWith("n1", "Duplicate note.", "wrong");
    expect(screen.getByLabelText(/Your password/)).toHaveValue("");
  });

  it("is hidden when the note can't be voided", () => {
    renderWith(<VoidNoteForm noteId="n1" actions={none} />);
    expect(
      screen.queryByRole("button", { name: "Void this note…" }),
    ).toBeNull();
  });
});
