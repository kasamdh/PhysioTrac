import { useState, type FormEvent } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useSelectedLocation } from "../organizations/useSelectedLocation";
import { createPatient, fetchScheduleSettings, searchPatients } from "../schedule/api";
import type { SchedulePatient } from "../schedule/types";

interface Props {
  onCreated: (patient: SchedulePatient) => void;
  onCancel: () => void;
}

const LANGUAGES = ["English", "Spanish", "Chinese", "Vietnamese", "Arabic", "French", "Korean", "Other"];

const empty = {
  firstName: "",
  lastName: "",
  dateOfBirth: "",
  phone: "",
  email: "",
  address: "",
  emergencyContact: "",
  preferredLanguage: "",
  primaryLocationId: "",
  assignedTherapistId: "",
};

/** Full patient registration from the Patients page. Like the schedule's
 * quick registration, it first looks for an existing chart with the same
 * name and date of birth and shows it before creating a duplicate. */
export function NewPatientForm({ onCreated, onCancel }: Props) {
  const settings = useQuery({ queryKey: ["schedule", "settings"], queryFn: fetchScheduleSettings });
  const locations = settings.data?.locations ?? [];
  // Clinicians who can own a caseload: active providers with a login.
  const therapists = (settings.data?.providers ?? []).filter((p) => p.isActive && p.userId);
  const { selectedId } = useSelectedLocation(locations);

  const [form, setForm] = useState(empty);
  // Default the clinic to the one picked on Home, until the user chooses.
  const [locationTouched, setLocationTouched] = useState(false);
  const primaryLocationId = locationTouched ? form.primaryLocationId : (selectedId ?? "");
  const [duplicates, setDuplicates] = useState<SchedulePatient[] | null>(null);

  const set = (key: keyof typeof empty) => (e: { target: { value: string } }) =>
    setForm((f) => ({ ...f, [key]: e.target.value }));

  const today = new Date().toISOString().slice(0, 10);
  const dobInvalid = !!form.dateOfBirth && (form.dateOfBirth > today || form.dateOfBirth < "1900-01-01");
  const emailInvalid = !!form.email.trim() && !/^\S+@\S+\.\S+$/.test(form.email.trim());
  const valid = form.firstName.trim() && form.lastName.trim() && form.dateOfBirth && !dobInvalid && !emailInvalid;

  const create = useMutation({
    mutationFn: () => {
      const text = (v: string) => v.trim() || null;
      return createPatient({
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        dateOfBirth: form.dateOfBirth,
        phone: text(form.phone),
        email: text(form.email),
        address: text(form.address),
        emergencyContact: text(form.emergencyContact),
        preferredLanguage: text(form.preferredLanguage),
        assignedTherapistId: form.assignedTherapistId || null,
        primaryLocationId: primaryLocationId || null,
        primaryCareProviderId: null,
        referringProviderId: null,
      });
    },
    onSuccess: (patient) => onCreated(patient),
  });

  const check = useMutation({
    mutationFn: () => searchPatients(form.lastName.trim()),
    onSuccess: (found) => {
      const first = form.firstName.trim().toLowerCase();
      const matches = found.filter((p) => p.dateOfBirth === form.dateOfBirth && p.fullName.toLowerCase().startsWith(first));
      if (matches.length > 0) setDuplicates(matches);
      else create.mutate();
    },
  });

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (valid) check.mutate();
  };

  const error = create.error ?? check.error;
  const busy = check.isPending || create.isPending;

  if (duplicates) {
    return (
      <div className="card mb-5 space-y-3">
        <h2 className="text-lg font-semibold text-text">Possible duplicate</h2>
        <p className="rounded-md bg-warning-light px-3 py-2 text-sm text-warning">
          A patient named {form.firstName.trim()} {form.lastName.trim()} with this date of birth already exists. Use the
          existing chart unless you're sure this is a different person.
        </p>
        {duplicates.map((p) => (
          <div key={p.id} className="rounded-md border border-border px-3 py-2 text-sm">
            <span className="font-medium">{p.fullName}</span>
            <span className="ml-2 text-text-muted">
              {p.medicalRecordNumber}
              {p.phone ? ` · ${p.phone}` : ""}
            </span>
          </div>
        ))}
        {error && <p className="alert-error">{error.message}</p>}
        <div className="flex justify-end gap-2">
          <button type="button" className="btn-secondary" onClick={onCancel}>
            Cancel
          </button>
          <button type="button" className="btn-secondary" onClick={() => setDuplicates(null)}>
            Back to form
          </button>
          <button type="button" className="btn-primary" disabled={create.isPending} onClick={() => create.mutate()}>
            {create.isPending ? "Adding…" : "Add anyway"}
          </button>
        </div>
      </div>
    );
  }

  return (
    <form onSubmit={onSubmit} className="card mb-5" noValidate>
      <h2 className="mb-4 text-lg font-semibold text-text">Add patient</h2>
      {error && <p className="alert-error mb-4">{error.message}</p>}
      <div className="grid grid-cols-1 gap-x-4 gap-y-3 sm:grid-cols-2 lg:grid-cols-3">
        <label className="field-label">
          First name *
          <input className="field-input mt-1" autoFocus autoComplete="off" value={form.firstName} onChange={set("firstName")} />
        </label>
        <label className="field-label">
          Last name *
          <input className="field-input mt-1" autoComplete="off" value={form.lastName} onChange={set("lastName")} />
        </label>
        <label className="field-label">
          Date of birth *
          <input
            type="date"
            className="field-input mt-1"
            max={today}
            min="1900-01-01"
            value={form.dateOfBirth}
            onChange={set("dateOfBirth")}
          />
          {dobInvalid && <span className="mt-1 block text-xs text-danger">Date of birth can't be in the future.</span>}
        </label>
        <label className="field-label">
          Phone
          <input type="tel" className="field-input mt-1" value={form.phone} onChange={set("phone")} />
        </label>
        <label className="field-label">
          Email
          <input type="email" className="field-input mt-1" value={form.email} onChange={set("email")} />
          {emailInvalid && <span className="mt-1 block text-xs text-danger">Enter a valid email address.</span>}
        </label>
        <label className="field-label">
          Preferred language
          <select className="field-input mt-1" value={form.preferredLanguage} onChange={set("preferredLanguage")}>
            <option value="">—</option>
            {LANGUAGES.map((l) => (
              <option key={l}>{l}</option>
            ))}
          </select>
        </label>
        <label className="field-label sm:col-span-2">
          Address
          <input className="field-input mt-1" value={form.address} onChange={set("address")} />
        </label>
        <label className="field-label">
          Emergency contact
          <input
            className="field-input mt-1"
            placeholder="Name, relationship, phone"
            value={form.emergencyContact}
            onChange={set("emergencyContact")}
          />
        </label>
        <label className="field-label">
          Primary location
          <select
            className="field-input mt-1"
            value={primaryLocationId}
            onChange={(e) => {
              setLocationTouched(true);
              set("primaryLocationId")(e);
            }}
          >
            <option value="">—</option>
            {locations.map((l) => (
              <option key={l.id} value={l.id}>
                {l.name}
              </option>
            ))}
          </select>
        </label>
        <label className="field-label">
          Assigned therapist
          <select className="field-input mt-1" value={form.assignedTherapistId} onChange={set("assignedTherapistId")}>
            <option value="">—</option>
            {therapists.map((t) => (
              <option key={t.id} value={t.userId!}>
                {t.name}
                {t.credentials ? `, ${t.credentials}` : ""}
              </option>
            ))}
          </select>
        </label>
      </div>
      <div className="mt-5 flex justify-end gap-2">
        <button type="button" className="btn-secondary" onClick={onCancel}>
          Cancel
        </button>
        <button type="submit" className="btn-primary" disabled={!valid || busy}>
          {busy ? "Adding…" : "Add patient"}
        </button>
      </div>
    </form>
  );
}
