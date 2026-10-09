import { useState, type FormEvent } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useToast } from "../../../components/Toast";
import { returnNote, startNoteReview, voidNote } from "../api";
import type { NoteActions } from "../types";

const useRefresh = () => {
  const queryClient = useQueryClient();
  return () => {
    for (const key of ["chart", "encounter", "workflow", "documentation"])
      void queryClient.invalidateQueries({ queryKey: [key] });
  };
};

/** The supervising PT's side of an assistant's submitted note: start the
 * review, then cosign it (NoteRecordPanel) or return it for correction. */
export function ReviewActions({
  noteId,
  actions,
}: {
  noteId: string;
  actions: NoteActions;
}) {
  const { showToast } = useToast();
  const refresh = useRefresh();
  const [returning, setReturning] = useState(false);
  const [reason, setReason] = useState("");
  const start = useMutation({
    mutationFn: () => startNoteReview(noteId),
    onSuccess: () => {
      showToast("Review started.");
      refresh();
    },
    onError: (e: Error) => showToast(e.message),
  });
  const send = useMutation({
    mutationFn: () => returnNote(noteId, reason.trim()),
    onSuccess: () => {
      showToast("Note returned to its author for correction.");
      setReturning(false);
      setReason("");
      refresh();
    },
  });
  const onReturn = (e: FormEvent) => {
    e.preventDefault();
    if (reason.trim()) send.mutate();
  };
  if (!actions.canStartReview && !actions.canReturn) return null;

  return (
    <div className="space-y-3 rounded-md border border-border p-3">
      <p className="font-bold text-[#333]">Supervising PT review</p>
      <div className="flex flex-wrap gap-2">
        {actions.canStartReview && (
          <button
            type="button"
            className="btn-primary"
            disabled={start.isPending}
            onClick={() => start.mutate()}
          >
            {start.isPending ? "Starting…" : "Start review"}
          </button>
        )}
        {actions.canReturn && !returning && (
          <button
            type="button"
            className="btn-refresh"
            onClick={() => setReturning(true)}
          >
            Return for correction…
          </button>
        )}
      </div>
      {returning && (
        <form
          onSubmit={onReturn}
          aria-label="Return for correction"
          className="space-y-2"
        >
          {send.isError && <p className="alert-error">{send.error.message}</p>}
          <label className="block" htmlFor={`return-${noteId}`}>
            <span className="font-bold text-[#333]">What needs correcting</span>
            <span className="ml-2 text-text-muted">— shown to the author</span>
            <textarea
              id={`return-${noteId}`}
              className="field-input mt-1 min-h-[5rem]"
              maxLength={1000}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
            />
          </label>
          <div className="flex flex-wrap gap-2">
            <button
              type="submit"
              className="btn-primary"
              disabled={!reason.trim() || send.isPending}
            >
              {send.isPending ? "Returning…" : "Return to author"}
            </button>
            <button
              type="button"
              className="btn-refresh"
              onClick={() => setReturning(false)}
            >
              Cancel
            </button>
          </div>
        </form>
      )}
    </div>
  );
}

/** Voids a note: a reason is always required; a signed note also needs
 * the voider's password. The note stays in the record, marked Voided. */
export function VoidNoteForm({
  noteId,
  actions,
}: {
  noteId: string;
  actions: NoteActions;
}) {
  const { showToast } = useToast();
  const refresh = useRefresh();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [password, setPassword] = useState("");
  const needsPassword = !!actions.voidNeedsPassword;
  const run = useMutation({
    mutationFn: () =>
      voidNote(noteId, reason.trim(), needsPassword ? password : null),
    onSuccess: () => {
      showToast("Note voided.");
      setOpen(false);
      refresh();
    },
    onError: () => setPassword(""),
  });
  if (!actions.canVoid) return null;
  if (!open)
    return (
      <button
        type="button"
        className="text-danger underline"
        onClick={() => setOpen(true)}
      >
        Void this note…
      </button>
    );
  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (reason.trim() && (!needsPassword || password)) run.mutate();
  };
  return (
    <form
      onSubmit={onSubmit}
      aria-label="Void note"
      className="space-y-2 rounded-md border border-danger p-3"
    >
      <p className="font-bold text-danger">Void this note</p>
      <p className="text-text-muted">
        Use this for a note that should not be part of the record (for example,
        written for the wrong patient or visit). It is kept and marked Voided
        with your reason; it can’t be undone.
        {needsPassword
          ? " A plan of care it created is voided too, and the plan it replaced becomes active again."
          : ""}
      </p>
      {run.isError && <p className="alert-error">{run.error.message}</p>}
      <label className="block" htmlFor={`void-reason-${noteId}`}>
        <span className="font-bold text-[#333]">Reason</span>
        <textarea
          id={`void-reason-${noteId}`}
          className="field-input mt-1 min-h-[4rem]"
          maxLength={1000}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
        />
      </label>
      {needsPassword && (
        <label className="block max-w-md" htmlFor={`void-password-${noteId}`}>
          <span className="font-bold text-[#333]">Your password</span>
          <span className="ml-2 text-text-muted">— re-enter it to void</span>
          <input
            id={`void-password-${noteId}`}
            type="password"
            autoComplete="current-password"
            className="field-input mt-1"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
        </label>
      )}
      <div className="flex flex-wrap gap-2">
        <button
          type="submit"
          className="btn-primary"
          disabled={
            !reason.trim() || (needsPassword && !password) || run.isPending
          }
        >
          {run.isPending ? "Voiding…" : "Void note"}
        </button>
        <button
          type="button"
          className="btn-refresh"
          onClick={() => setOpen(false)}
        >
          Cancel
        </button>
      </div>
    </form>
  );
}
