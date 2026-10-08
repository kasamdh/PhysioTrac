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
import { fetchEncounter } from "../encounter/api";
import { recordNoteOutput } from "../printing/api";
import { PageMargins } from "../printing/PageMargins";
import { PrintActions } from "../printing/PrintActions";
import { formatInZone, usePrintOutput } from "../printing/usePrintOutput";

const formatDate = (iso: string) => {
  const [y, m, d] = iso.slice(0, 10).split("-");
  return `${m}/${d}/${y}`;
};

/** A visit note as a clean document for printing or "Save as PDF": the
 * organization's letterhead, patient identifiers, the full note, addenda,
 * and the electronic signature block, with times in the clinic's time zone.
 * Opens the print dialog once loaded; each print or export is audited first. */
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

  // The pain assessment and body chart print with the note; wait for them too.
  const charting = useQuery({
    queryKey: ["encounter", noteId],
    queryFn: () => fetchEncounter(noteId),
  });
  const ready =
    !!note.data &&
    !!record.data &&
    !!patient.data &&
    !!org.data &&
    !charting.isLoading;
  const printing = usePrintOutput(ready, (kind) =>
    recordNoteOutput(noteId, kind),
  );

  const error = note.error ?? record.error ?? patient.error;
  if (error) return <p className="alert-error m-4">{error.message}</p>;
  if (!ready) return <p className="m-4 text-text-muted">Preparing the note…</p>;

  const n = note.data!;
  const p = patient.data!;
  const r = record.data!;
  const tz = org.data!.timezone;
  return (
    <div className="mx-auto max-w-4xl bg-white p-4 text-[#222] sm:p-8 print:max-w-none print:p-0">
      <PrintActions {...printing} />
      <PageMargins
        clinic={org.data!.name}
        patient={p.fullName}
        mrn={p.medicalRecordNumber}
        title={`${NoteTypeLabels[n.noteType] ?? "Visit note"} · ${formatDate(n.serviceDate)}`}
      />

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
              : ""} on {formatInZone(n.signedAt, tz)}.
            {r.cosignedByName && n.cosignedAt
              ? ` Cosigned by ${r.cosignedByName} on ${formatInZone(n.cosignedAt, tz)}.`
              : ""}
          </p>
        ) : (
          <p>
            <strong>Unsigned draft</strong> — not part of the medical record
            until signed.
          </p>
        )}
        <p className="mt-2 text-text-muted">
          Printed {formatInZone(new Date().toISOString(), tz)}. Confidential patient
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
