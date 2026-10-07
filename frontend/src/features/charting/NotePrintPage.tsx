import { useEffect, useRef } from "react";
import { useQuery } from "@tanstack/react-query";
import { useParams } from "react-router-dom";
import { fetchPatientDetail } from "../admin/api";
import { NoteBody } from "../documentation/NoteBody";
import { fetchCurrentOrganization } from "../organizations/api";
import {
  NoteStatus,
  NoteStatusLabels,
  NoteTypeLabels,
} from "../workflow/types";
import { fetchChartNote, fetchNoteRecord } from "./api";

const formatDate = (iso: string) => {
  const [y, m, d] = iso.slice(0, 10).split("-");
  return `${m}/${d}/${y}`;
};

/** A visit note as a clean document for printing or "Save as PDF": the
 * organization's letterhead, patient identifiers, the full note, addenda,
 * and the electronic signature block. Opens the print dialog once loaded. */
export function NotePrintPage() {
  const { noteId = "" } = useParams();
  const note = useQuery({
    queryKey: ["chart", "note", noteId],
    queryFn: () => fetchChartNote(noteId),
  });
  const record = useQuery({
    queryKey: ["chart", "record", noteId],
    queryFn: () => fetchNoteRecord(noteId),
  });
  const patientId = note.data?.patientId;
  const patient = useQuery({
    queryKey: ["admin", "patient", patientId],
    queryFn: () => fetchPatientDetail(patientId!),
    enabled: !!patientId,
  });
  const org = useQuery({
    queryKey: ["organizations", "current"],
    queryFn: fetchCurrentOrganization,
  });

  const ready = !!note.data && !!record.data && !!patient.data && !!org.data;
  const printed = useRef(false);
  useEffect(() => {
    if (!ready || printed.current) return;
    printed.current = true;
    // Give the interventions table a moment to load before printing.
    const t = window.setTimeout(() => window.print(), 800);
    return () => window.clearTimeout(t);
  }, [ready]);

  const error = note.error ?? record.error ?? patient.error;
  if (error) return <p className="alert-error m-4">{error.message}</p>;
  if (!ready) return <p className="m-4 text-text-muted">Preparing the note…</p>;

  const n = note.data!;
  const p = patient.data!;
  const r = record.data!;
  return (
    <div className="mx-auto max-w-4xl bg-white p-4 text-[#222] sm:p-8 print:max-w-none print:p-0">
      <div className="mb-4 flex flex-wrap gap-2 print:hidden">
        <button
          type="button"
          className="btn-primary"
          onClick={() => window.print()}
        >
          Print / Save as PDF
        </button>
        <button
          type="button"
          className="btn-refresh"
          onClick={() => window.close()}
        >
          Close
        </button>
      </div>

      <header className="border-b-2 border-[#1565b8] pb-3">
        <p className="text-2xl font-bold text-[#1565b8]">{org.data!.name}</p>
        <p className="text-xl font-bold">
          {NoteTypeLabels[n.noteType] ?? "Visit note"}
          {n.amendsNoteId ? " — Amendment" : ""}
        </p>
      </header>

      <dl className="my-4 grid grid-cols-2 gap-x-6 gap-y-1 sm:grid-cols-4">
        <Item label="Patient" value={p.fullName} />
        <Item label="MRN" value={p.medicalRecordNumber} />
        <Item label="Date of birth" value={formatDate(p.dateOfBirth)} />
        <Item label="Date of service" value={formatDate(n.serviceDate)} />
        <Item label="Clinician" value={r.authorName || "—"} />
        <Item label="Status" value={NoteStatusLabels[n.status] ?? ""} />
      </dl>

      {n.status === NoteStatus.Amended && (
        <p className="mb-4 border border-[#b26a00] px-3 py-2">
          This note was amended. It is shown as originally signed; the signed
          amendment is the current version.
        </p>
      )}
      {p.precautions && (
        <p className="mb-4">
          <strong>Precautions:</strong> {p.precautions}
        </p>
      )}

      <NoteBody note={n} addenda={r.addenda} />

      <footer className="mt-6 border-t border-[#999] pt-3">
        {n.signedAt ? (
          <p>
            Electronically signed by <strong>{n.signatureName}</strong>
            {n.signatureCredentials
              ? `, ${n.signatureCredentials}`
              : ""} on {new Date(n.signedAt).toLocaleString("en-US")}.
            {r.cosignedByName && n.cosignedAt
              ? ` Cosigned by ${r.cosignedByName} on ${new Date(n.cosignedAt).toLocaleString("en-US")}.`
              : ""}
          </p>
        ) : (
          <p>
            <strong>Unsigned draft</strong> — not part of the medical record
            until signed.
          </p>
        )}
        <p className="mt-2 text-text-muted">
          Printed {new Date().toLocaleString("en-US")}. Confidential patient
          information.
        </p>
      </footer>
    </div>
  );
}

function Item({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-text-muted">{label}</dt>
      <dd className="font-bold">{value}</dd>
    </div>
  );
}
