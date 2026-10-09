import { useEffect, useId, useState, type FormEvent } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { FormRow } from "../admin/FormRow";
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

/** Administration › Patient List › Add Patient: full registration in a
 * dialog. Required fields are marked until filled. Before creating, it looks
 * for an existing chart with the same name and date of birth and shows it
 * instead of silently creating a duplicate. */
export function AddPatientDialog({ onCreated, onCancel }: Props) {
  const titleId = useId();
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

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onCancel();
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [onCancel]);

  const set = (key: keyof typeof empty) => (e: { target: { value: string } }) =>
    setForm((f) => ({ ...f, [key]: e.target.value }));

  const today = new Date().toISOString().slice(0, 10);
  const nameMissing = !form.firstName.trim() || !form.lastName.trim();
  const dobMissing = !form.dateOfBirth;
  const dobInvalid = !dobMissing && (form.dateOfBirth > today || form.dateOfBirth < "1900-01-01");
  const emailInvalid = !!form.email.trim() && !/^[^\s@]+@[^\s@]+$/.test(form.email.trim());
  const valid = !nameMissing && !dobMissing && !dobInvalid && !emailInvalid;

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

  return (
    <div className="modal-overlay" onClick={onCancel}>
      <form
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        onSubmit={onSubmit}
        onClick={(e) => e.stopPropagation()}
        noValidate
        className="flex max-h-[92vh] w-full max-w-2xl flex-col overflow-hidden rounded-lg bg-white shadow-2xl"
      >
        <h2 id={titleId} className="border-b border-border px-6 py-4 text-3xl text-[#333]">
          {duplicates ? "Possible Duplicate" : "Add Patient"}
        </h2>

        {duplicates ? (
          <div className="space-y-3 overflow-y-auto px-6 py-5">
            <p className="rounded-md bg-warning-light px-3 py-2 text-warning">
              A patient named {form.firstName.trim()} {form.lastName.trim()} with this date of birth already exists. Use
              the existing chart unless you’re sure this is a different person.
            </p>
            {duplicates.map((p) => (
              <div key={p.id} className="rounded-md border border-border px-3 py-2">
                <span className="font-bold">{p.fullName}</span>
                <span className="ml-2 text-text-muted">
                  {p.medicalRecordNumber}
                  {p.phone ? ` · ${p.phone}` : ""}
                </span>
              </div>
            ))}
            {error && <p className="alert-error">{error.message}</p>}
          </div>
        ) : (
          <div className="space-y-4 overflow-y-auto px-6 py-5">
            {error && <p className="alert-error">{error.message}</p>}
            <FormRow label="Patient Name" htmlFor="ap-first" missing={nameMissing}>
              <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                <input
                  id="ap-first"
                  aria-label="First name"
                  placeholder="First name"
                  autoFocus
                  autoComplete="off"
                  className="field-input"
                  value={form.firstName}
                  onChange={set("firstName")}
                />
                <input
                  aria-label="Last name"
                  placeholder="Last name"
                  autoComplete="off"
                  className="field-input"
                  value={form.lastName}
                  onChange={set("lastName")}
                />
              </div>
            </FormRow>
            <FormRow label="Date of Birth" htmlFor="ap-dob" missing={dobMissing}>
              <input
                id="ap-dob"
                type="date"
                min="1900-01-01"
                max={today}
                className="field-input"
                value={form.dateOfBirth}
                onChange={set("dateOfBirth")}
              />
              {dobInvalid && <p className="mt-1 text-danger">Date of birth can’t be in the future.</p>}
            </FormRow>
            <FormRow label="Phone" htmlFor="ap-phone">
              <input id="ap-phone" type="tel" className="field-input" value={form.phone} onChange={set("phone")} />
            </FormRow>
            <FormRow label="E-Mail Address" htmlFor="ap-email">
              <input id="ap-email" type="email" className="field-input" value={form.email} onChange={set("email")} />
              {emailInvalid && <p className="mt-1 text-danger">Enter a valid email address.</p>}
            </FormRow>
            <FormRow label="Address" htmlFor="ap-address">
              <input id="ap-address" className="field-input" value={form.address} onChange={set("address")} />
            </FormRow>
            <FormRow label="Emergency Contact" htmlFor="ap-emergency">
              <input
                id="ap-emergency"
                className="field-input"
                placeholder="Name, relationship, phone"
                value={form.emergencyContact}
                onChange={set("emergencyContact")}
              />
            </FormRow>
            <FormRow label="Language" htmlFor="ap-language">
              <select
                id="ap-language"
                className="field-input"
                value={form.preferredLanguage}
                onChange={set("preferredLanguage")}
              >
                <option value="">—</option>
                {LANGUAGES.map((l) => (
                  <option key={l}>{l}</option>
                ))}
              </select>
            </FormRow>
            <FormRow label="Primary Location" htmlFor="ap-location">
              <select
                id="ap-location"
                className="field-input"
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
            </FormRow>
            <FormRow label="Assigned Therapist" htmlFor="ap-therapist">
              <select
                id="ap-therapist"
                className="field-input"
                value={form.assignedTherapistId}
                onChange={set("assignedTherapistId")}
              >
                <option value="">—</option>
                {therapists.map((t) => (
                  <option key={t.id} value={t.userId!}>
                    {t.name}
                    {t.credentials ? `, ${t.credentials}` : ""}
                  </option>
                ))}
              </select>
            </FormRow>
          </div>
        )}

        <div className="flex justify-end gap-3 border-t border-border px-6 py-4">
          <button type="button" className="btn-refresh" onClick={onCancel}>
            Cancel
          </button>
          {duplicates ? (
            <>
              <button type="button" className="btn-refresh" onClick={() => setDuplicates(null)}>
                Back to form
              </button>
              <button type="button" className="btn-primary" disabled={create.isPending} onClick={() => create.mutate()}>
                {create.isPending ? "Adding…" : "Add anyway"}
              </button>
            </>
          ) : (
            <button type="submit" className="btn-primary" disabled={!valid || busy}>
              {busy ? "Saving…" : "Save and Close"}
            </button>
          )}
        </div>
      </form>
    </div>
  );
}
