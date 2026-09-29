import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { createPatient, searchPatients } from "../api";
import type { ScheduleLocation, SchedulePatient } from "../types";

interface Props {
  initialName: string;
  locations: ScheduleLocation[];
  defaultLocationId: string;
  /** Assign the new chart to the clinician being booked, so a Therapist/
   * Assistant can open it afterwards (their chart list is caseload-only). */
  assignedTherapistId: string | null;
  onCreated: (patient: SchedulePatient) => void;
  onCancel: () => void;
}

/** Minimal registration so a brand-new patient can be booked without
 * leaving the schedule. Before creating, it looks for an existing chart with
 * the same name and date of birth and offers that instead -- duplicates are
 * the classic front-desk mistake. */
export function QuickPatientForm({ initialName, locations, defaultLocationId, assignedTherapistId, onCreated, onCancel }: Props) {
  const [first, ...rest] = initialName.trim().split(/\s+/);
  const looksLikeName = !/\d/.test(initialName);
  const [firstName, setFirstName] = useState(looksLikeName ? (first ?? "") : "");
  const [lastName, setLastName] = useState(looksLikeName ? rest.join(" ") : "");
  const [dateOfBirth, setDateOfBirth] = useState("");
  const [phone, setPhone] = useState("");
  const [email, setEmail] = useState("");
  const [locationId, setLocationId] = useState(defaultLocationId);
  const [duplicates, setDuplicates] = useState<SchedulePatient[] | null>(null);

  const today = new Date().toISOString().slice(0, 10);
  const valid = firstName.trim() && lastName.trim() && dateOfBirth && dateOfBirth <= today;

  const create = useMutation({
    mutationFn: () =>
      createPatient({
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        dateOfBirth,
        phone: phone.trim() || null,
        email: email.trim() || null,
        address: null,
        emergencyContact: null,
        preferredLanguage: null,
        assignedTherapistId,
        primaryLocationId: locationId || null,
        primaryCareProviderId: null,
        referringProviderId: null,
      }),
    onSuccess: onCreated,
  });

  const check = useMutation({
    mutationFn: () => searchPatients(lastName.trim()),
    onSuccess: (found) => {
      const matches = found.filter(
        (p) => p.dateOfBirth === dateOfBirth && p.fullName.toLowerCase().startsWith(firstName.trim().toLowerCase()),
      );
      if (matches.length > 0) setDuplicates(matches);
      else create.mutate();
    },
  });

  const error = create.error ?? check.error;

  return (
    <div>
      <h3 className="mb-3 text-base font-semibold text-text">Quick patient registration</h3>
      {duplicates ? (
        <div className="space-y-3">
          <p className="rounded-md bg-warning-light px-3 py-2 text-sm text-warning">
            A patient with this name and date of birth already exists. Use the existing chart unless you're sure this is someone else.
          </p>
          {duplicates.map((p) => (
            <div key={p.id} className="flex items-center justify-between rounded-md border border-border px-3 py-2 text-sm">
              <span>
                <span className="font-medium">{p.fullName}</span>
                <span className="ml-2 text-xs text-text-muted">{p.medicalRecordNumber}{p.phone ? ` · ${p.phone}` : ""}</span>
              </span>
              <button type="button" className="btn-primary" onClick={() => onCreated(p)}>
                Use this patient
              </button>
            </div>
          ))}
          <div className="flex justify-end gap-2">
            <button type="button" className="btn-secondary" onClick={() => setDuplicates(null)}>Back</button>
            <button type="button" className="btn-secondary" disabled={create.isPending} onClick={() => create.mutate()}>
              Create a new chart anyway
            </button>
          </div>
        </div>
      ) : (
        <>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <label>
              <span className="field-label">First name *</span>
              <input className="field-input" autoFocus value={firstName} onChange={(e) => setFirstName(e.target.value)} />
            </label>
            <label>
              <span className="field-label">Last name *</span>
              <input className="field-input" value={lastName} onChange={(e) => setLastName(e.target.value)} />
            </label>
            <label>
              <span className="field-label">Date of birth *</span>
              <input type="date" max={today} className="field-input" value={dateOfBirth} onChange={(e) => setDateOfBirth(e.target.value)} />
            </label>
            <label>
              <span className="field-label">Phone</span>
              <input type="tel" className="field-input" value={phone} onChange={(e) => setPhone(e.target.value)} />
            </label>
            <label>
              <span className="field-label">Email</span>
              <input type="email" className="field-input" value={email} onChange={(e) => setEmail(e.target.value)} />
            </label>
            <label>
              <span className="field-label">Location</span>
              <select className="field-input" value={locationId} onChange={(e) => setLocationId(e.target.value)}>
                <option value="">None</option>
                {locations.map((l) => (
                  <option key={l.id} value={l.id}>{l.name}</option>
                ))}
              </select>
            </label>
          </div>
          {error && <p className="alert-error mt-3">{error.message}</p>}
          <div className="mt-4 flex justify-end gap-2">
            <button type="button" className="btn-secondary" onClick={onCancel}>Back to appointment</button>
            <button type="button" className="btn-primary" disabled={!valid || check.isPending || create.isPending} onClick={() => check.mutate()}>
              {create.isPending || check.isPending ? "Saving…" : "Create patient"}
            </button>
          </div>
        </>
      )}
    </div>
  );
}
