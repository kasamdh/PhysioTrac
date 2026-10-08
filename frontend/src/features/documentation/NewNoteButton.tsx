import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { createPatientNote } from "../encounter/api";
import { NoteTypeLabels, TEMPLATE_NOTE_TYPES } from "../workflow/types";

/** Starts a note for the patient outside a scheduled visit (a call, a
 * consult, a missed-visit or addendum note...) and opens it. */
export function NewNoteButton({ patientId }: { patientId: string }) {
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [noteType, setNoteType] = useState("13");
  const [date, setDate] = useState(() =>
    new Date().toLocaleDateString("en-CA"),
  );
  const create = useMutation({
    mutationFn: () => createPatientNote(patientId, Number(noteType), date),
    onSuccess: (n) => navigate(`/chart/${n.id}`),
  });
  if (!open)
    return (
      <button
        type="button"
        className="btn-primary"
        onClick={() => setOpen(true)}
      >
        + New note
      </button>
    );
  return (
    <form
      aria-label="New note"
      className="flex flex-wrap items-end gap-2 rounded-md border border-border bg-white p-3"
      onSubmit={(e) => {
        e.preventDefault();
        create.mutate();
      }}
    >
      <label className="block">
        <span className="font-bold text-[#333]">Note type</span>
        <select
          className="field-input mt-1"
          value={noteType}
          onChange={(e) => setNoteType(e.target.value)}
        >
          {TEMPLATE_NOTE_TYPES.map((t) => (
            <option key={t} value={t}>
              {NoteTypeLabels[t]}
            </option>
          ))}
        </select>
      </label>
      <label className="block">
        <span className="font-bold text-[#333]">Date</span>
        <input
          type="date"
          className="field-input mt-1"
          value={date}
          onChange={(e) => setDate(e.target.value)}
        />
      </label>
      <button
        type="submit"
        className="btn-primary"
        disabled={!date || create.isPending}
      >
        {create.isPending ? "Starting…" : "Start note"}
      </button>
      <button
        type="button"
        className="btn-refresh"
        onClick={() => setOpen(false)}
      >
        Cancel
      </button>
      {create.isError && (
        <p className="alert-error w-full">{create.error.message}</p>
      )}
    </form>
  );
}
