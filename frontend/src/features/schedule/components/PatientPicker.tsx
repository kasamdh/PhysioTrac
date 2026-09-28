import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { searchPatients } from "../api";
import type { SchedulePatient } from "../types";

interface Props {
  selected: SchedulePatient | null;
  onSelect: (patient: SchedulePatient | null) => void;
  onCreateNew: (typed: string) => void;
}

function formatDob(iso: string): string {
  const [y, m, d] = iso.split("-");
  return `${m}/${d}/${y}`;
}

/** Patient search for booking: name, MRN, phone, or date of birth, debounced,
 * scoped server-side to the caller's own organization. Existing patients are
 * always searched first; "Create new patient" is offered once a search has
 * come back, so a duplicate chart isn't the easiest path. */
export function PatientPicker({ selected, onSelect, onCreateNew }: Props) {
  const [text, setText] = useState("");
  const [term, setTerm] = useState("");

  useEffect(() => {
    const timer = window.setTimeout(() => setTerm(text.trim()), 300);
    return () => window.clearTimeout(timer);
  }, [text]);

  const results = useQuery({
    queryKey: ["schedule", "patient-search", term],
    queryFn: ({ signal }) => searchPatients(term, signal),
    enabled: term.length >= 2 && !selected,
  });

  if (selected) {
    return (
      <div className="flex items-center justify-between gap-3 rounded-md border border-primary bg-primary-light/40 px-3 py-2 text-sm">
        <div>
          <div className="font-semibold text-text">{selected.fullName}</div>
          <div className="text-xs text-text-muted">
            {selected.medicalRecordNumber} · DOB {formatDob(selected.dateOfBirth)}
            {selected.phone ? ` · ${selected.phone}` : ""}
          </div>
        </div>
        <button type="button" className="btn-secondary" onClick={() => onSelect(null)}>
          Change
        </button>
      </div>
    );
  }

  return (
    <div>
      <input
        type="search"
        autoFocus
        className="field-input"
        placeholder="Search name, MRN, phone, or DOB (MM/DD/YYYY)"
        aria-label="Search patient"
        value={text}
        onChange={(e) => setText(e.target.value)}
      />
      {term.length >= 2 && (
        <div className="mt-1 max-h-56 overflow-y-auto rounded-md border border-border bg-surface text-sm">
          {results.isLoading && <p className="px-3 py-2 text-text-muted">Searching…</p>}
          {results.isError && <p className="px-3 py-2 text-danger">{results.error.message}</p>}
          {results.data?.map((p) => (
            <button
              key={p.id}
              type="button"
              className="block w-full border-b border-border px-3 py-2 text-left last:border-0 hover:bg-surface-muted"
              onClick={() => onSelect(p)}
            >
              <span className="font-medium text-text">{p.fullName}</span>
              <span className="ml-2 text-xs text-text-muted">
                {p.medicalRecordNumber} · DOB {formatDob(p.dateOfBirth)}
                {p.phone ? ` · ${p.phone}` : ""}
              </span>
            </button>
          ))}
          {results.data && (
            <div className="flex items-center justify-between gap-2 bg-surface-muted px-3 py-2">
              <span className="text-text-muted">{results.data.length === 0 ? "No patient found." : "Not listed?"}</span>
              <button type="button" className="btn-secondary" onClick={() => onCreateNew(term)}>
                + Create new patient
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
