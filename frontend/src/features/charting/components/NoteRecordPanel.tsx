import { useState, type FormEvent } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { useToast } from "../../../components/Toast";
import { AddendaList } from "../../documentation/NoteBody";
import { NoteStatus } from "../../workflow/types";
import { addAddendum, amendNote, cosignChartNote, lockNote } from "../api";
import type { ChartNote, NoteRecord } from "../types";
import { ReviewActions, VoidNoteForm } from "./LifecycleActions";
import { VersionHistory } from "./VersionHistory";

/** The legal record of a note that is no longer a draft: its signature,
 * cosignature, addenda, formal amendment, lock, print and version history.
 * Every button follows the service's own rules (NoteRecord.actions). */
export function NoteRecordPanel({
  note,
  record,
}: {
  note: ChartNote;
  record: NoteRecord | undefined;
}) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const navigate = useNavigate();
  const actions = record?.actions;
  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ["chart"] });
    void queryClient.invalidateQueries({ queryKey: ["workflow"] });
    void queryClient.invalidateQueries({ queryKey: ["documentation"] });
  };

  // ---- cosign -------------------------------------------------------------
  const [cosignPassword, setCosignPassword] = useState("");
  const cosign = useMutation({
    mutationFn: () => cosignChartNote(note.id, cosignPassword),
    onSuccess: () => {
      showToast("Note cosigned.");
      refresh();
    },
    onError: () => setCosignPassword(""),
  });

  // ---- addendum -----------------------------------------------------------
  const [addendumOpen, setAddendumOpen] = useState(false);
  const [addendum, setAddendum] = useState({ reason: "", body: "" });
  const saveAddendum = useMutation({
    mutationFn: () => addAddendum(note.id, addendum.reason, addendum.body),
    onSuccess: () => {
      showToast("Addendum added.");
      setAddendum({ reason: "", body: "" });
      setAddendumOpen(false);
      refresh();
    },
  });
  const submitAddendum = (e: FormEvent) => {
    e.preventDefault();
    saveAddendum.mutate();
  };

  // ---- amendment ----------------------------------------------------------
  const [amendOpen, setAmendOpen] = useState(false);
  const [amendReason, setAmendReason] = useState("");
  const amend = useMutation({
    mutationFn: () => amendNote(note.id, amendReason),
    onSuccess: (draft) => {
      refresh();
      navigate(`/chart/${draft.id}`);
    },
  });
  const submitAmend = (e: FormEvent) => {
    e.preventDefault();
    amend.mutate();
  };

  // ---- lock ---------------------------------------------------------------
  const [confirmLock, setConfirmLock] = useState(false);
  const lock = useMutation({
    mutationFn: () => lockNote(note.id),
    onSuccess: () => {
      showToast("Note locked.");
      setConfirmLock(false);
      refresh();
    },
  });

  const openAmendment =
    record?.amendmentNoteId &&
    (record.amendmentStatus === NoteStatus.Draft ||
      record.amendmentStatus === NoteStatus.ReviewRequired ||
      record.amendmentStatus === NoteStatus.InReview ||
      record.amendmentStatus === NoteStatus.ReturnedForCorrection);
  const when = (iso: string | null | undefined) =>
    iso ? new Date(iso).toLocaleString("en-US") : "";

  if (note.status === NoteStatus.Voided)
    return (
      <div className="space-y-4">
        <p role="status" className="alert-error">
          <strong>Voided</strong>
          {note.voidedAt ? ` on ${when(note.voidedAt)}` : ""}
          {note.voidReason ? ` — ${note.voidReason.replace(/\.$/, "")}` : ""}.
          It is kept in the record but is not part of the patient’s active
          documentation.
        </p>
        {!!record?.addenda.length && <AddendaList addenda={record.addenda} />}
        <VersionHistory noteId={note.id} />
      </div>
    );

  const underReview =
    note.status === NoteStatus.ReviewRequired ||
    note.status === NoteStatus.InReview;
  return (
    <div className="space-y-4">
      <p
        className={`rounded-md px-3 py-2 ${underReview ? "border border-warning bg-warning-light text-[#333]" : "bg-success-light text-success"}`}
      >
        {note.status === NoteStatus.InReview
          ? "In review by a supervising PT — submitted"
          : note.status === NoteStatus.ReviewRequired
            ? "Submitted for a supervising PT’s review and cosign"
            : note.status === NoteStatus.Amended
              ? "Signed, then amended"
              : "Signed"}
        {note.signatureName ? ` by ${note.signatureName}` : ""}
        {note.signatureCredentials ? `, ${note.signatureCredentials}` : ""}
        {note.signedAt
          ? ` on ${new Date(note.signedAt).toLocaleString("en-US")}`
          : ""}
        .
        {record?.cosignedByName && note.cosignedAt
          ? ` Cosigned by ${record.cosignedByName} on ${new Date(note.cosignedAt).toLocaleString("en-US")}.`
          : ""}
        {note.status === NoteStatus.Locked ? " Locked." : ""}{" "}
        {underReview ? "Submitted notes" : "Signed notes"} can’t be changed.
      </p>

      {actions && <ReviewActions noteId={note.id} actions={actions} />}

      {actions?.canCosign && (
        <form
          className="space-y-3 rounded-md border border-border p-3"
          onSubmit={(e) => {
            e.preventDefault();
            cosign.mutate();
          }}
        >
          <p className="font-bold text-[#333]">Cosign this note</p>
          <p className="text-text-muted">
            Review the note above. Cosigning confirms your supervision and
            completes the visit.
          </p>
          {cosign.isError && (
            <p className="alert-error">{cosign.error.message}</p>
          )}
          <label className="block max-w-md" htmlFor="chart-cosign-password">
            <span className="font-bold text-[#333]">Your password</span>
            <span className="ml-2 text-text-muted">
              — re-enter it to cosign
            </span>
            <input
              id="chart-cosign-password"
              type="password"
              autoComplete="current-password"
              className="field-input mt-1"
              value={cosignPassword}
              onChange={(e) => setCosignPassword(e.target.value)}
            />
          </label>
          <button
            type="submit"
            className="btn-primary"
            disabled={!cosignPassword || cosign.isPending}
          >
            {cosign.isPending ? "Cosigning…" : "Cosign note"}
          </button>
        </form>
      )}

      {!!record?.addenda.length && <AddendaList addenda={record.addenda} />}

      <div className="flex flex-wrap gap-2">
        {actions?.canAddAddendum && !addendumOpen && (
          <button
            type="button"
            className="btn-refresh"
            onClick={() => setAddendumOpen(true)}
          >
            + Add addendum
          </button>
        )}
        {actions?.canAmend && !amendOpen && (
          <button
            type="button"
            className="btn-refresh"
            onClick={() =>
              openAmendment ? amend.mutate() : setAmendOpen(true)
            }
          >
            {openAmendment ? "Continue amendment" : "Amend note"}
          </button>
        )}
        {actions?.canLock &&
          (confirmLock ? (
            <>
              <button
                type="button"
                className="btn-primary"
                disabled={lock.isPending}
                onClick={() => lock.mutate()}
              >
                {lock.isPending ? "Locking…" : "Yes, lock this note"}
              </button>
              <button
                type="button"
                className="btn-refresh"
                onClick={() => setConfirmLock(false)}
              >
                Cancel
              </button>
            </>
          ) : (
            <button
              type="button"
              className="btn-refresh"
              onClick={() => setConfirmLock(true)}
            >
              Lock note
            </button>
          ))}
        <a
          href={`/notes/${note.id}/print`}
          target="_blank"
          rel="noopener"
          className="btn-refresh"
        >
          Print / PDF
        </a>
      </div>
      {confirmLock && (
        <p className="text-text-muted">
          A locked note can no longer receive addenda or amendments (for
          example, once billing has closed).
        </p>
      )}
      {lock.isError && <p className="alert-error">{lock.error.message}</p>}

      {addendumOpen && (
        <form
          aria-label="Add addendum"
          className="space-y-3 rounded-md border border-border p-3"
          onSubmit={submitAddendum}
        >
          <p className="text-text-muted">
            An addendum adds information to the signed note — a late entry or
            clarification. The signed note itself doesn’t change.
          </p>
          {saveAddendum.isError && (
            <p className="alert-error">{saveAddendum.error.message}</p>
          )}
          <label className="block">
            <span className="font-bold text-[#333]">Reason</span>
            <input
              className="field-input mt-1"
              maxLength={500}
              placeholder="e.g. Late entry, clarification"
              value={addendum.reason}
              onChange={(e) =>
                setAddendum({ ...addendum, reason: e.target.value })
              }
            />
          </label>
          <label className="block">
            <span className="font-bold text-[#333]">Addendum</span>
            <textarea
              className="field-input mt-1 min-h-[6rem] resize-y"
              maxLength={4000}
              value={addendum.body}
              onChange={(e) =>
                setAddendum({ ...addendum, body: e.target.value })
              }
            />
          </label>
          <div className="flex flex-wrap gap-2">
            <button
              type="submit"
              className="btn-primary"
              disabled={
                !addendum.reason.trim() ||
                !addendum.body.trim() ||
                saveAddendum.isPending
              }
            >
              {saveAddendum.isPending ? "Saving…" : "Save addendum"}
            </button>
            <button
              type="button"
              className="btn-refresh"
              onClick={() => setAddendumOpen(false)}
            >
              Cancel
            </button>
          </div>
        </form>
      )}

      {amendOpen && (
        <form
          aria-label="Amend note"
          className="space-y-3 rounded-md border border-border p-3"
          onSubmit={submitAmend}
        >
          <p className="text-text-muted">
            An amendment corrects the signed note: you get a copy of it to edit
            and sign. Once signed, the amendment replaces this note in the
            record; this note stays viewable, marked Amended.
          </p>
          {amend.isError && (
            <p className="alert-error">{amend.error.message}</p>
          )}
          <label className="block">
            <span className="font-bold text-[#333]">Reason for amendment</span>
            <input
              className="field-input mt-1"
              maxLength={1000}
              placeholder="e.g. Incorrect knee flexion measurement recorded"
              value={amendReason}
              onChange={(e) => setAmendReason(e.target.value)}
            />
          </label>
          <div className="flex flex-wrap gap-2">
            <button
              type="submit"
              className="btn-primary"
              disabled={!amendReason.trim() || amend.isPending}
            >
              {amend.isPending ? "Starting…" : "Start amendment"}
            </button>
            <button
              type="button"
              className="btn-refresh"
              onClick={() => setAmendOpen(false)}
            >
              Cancel
            </button>
          </div>
        </form>
      )}

      {actions && <VoidNoteForm noteId={note.id} actions={actions} />}
      <VersionHistory noteId={note.id} />
    </div>
  );
}
