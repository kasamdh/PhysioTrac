import { useEffect, useId, useState, type FormEvent } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { SegmentedButtons } from "../../components/SegmentedButtons";
import { fetchPatientDetail, updatePatient } from "../admin/api";
import { FormRow } from "../admin/FormRow";
import type { PatientDetail } from "../admin/types";
import { fetchScheduleSettings } from "../schedule/api";
import { PatientStatus } from "./types";

const LANGUAGES = ["English", "Spanish", "Chinese", "Vietnamese", "Arabic", "French", "Korean", "Other"];
const STATUS_CHOICES: { value: string; label: string }[] = [
  { value: String(PatientStatus.Active), label: "Active" },
  { value: String(PatientStatus.Inactive), label: "Inactive" },
  { value: String(PatientStatus.Discharged), label: "Discharged" },
];

/** Administration › Patient List › Edit patient. Loads the full chart
 * header first, so fields this form doesn't show (primary care / referring
 * provider) are sent back unchanged instead of being cleared. */
export function EditPatientDialog({
  patientId,
  onClose,
  onSaved,
}: {
  patientId: string;
  onClose: () => void;
  onSaved: (patient: PatientDetail) => void;
}) {
  const titleId = useId();
  const detail = useQuery({ queryKey: ["admin", "patient", patientId], queryFn: () => fetchPatientDetail(patientId) });

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [onClose]);

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        onClick={(e) => e.stopPropagation()}
        className="flex max-h-[92vh] w-full max-w-2xl flex-col overflow-hidden rounded-lg bg-white shadow-2xl"
      >
        <h2 id={titleId} className="border-b border-border px-6 py-4 text-3xl text-[#333]">
          Edit Patient
          {detail.data && <span className="ml-3 align-middle text-text-muted">{detail.data.medicalRecordNumber}</span>}
        </h2>
        {detail.isLoading && <p className="px-6 py-8 text-text-muted">Loading…</p>}
        {detail.isError && (
          <div className="px-6 py-6">
            <p className="alert-error">{detail.error.message}</p>
            <div className="mt-4 flex justify-end">
              <button type="button" className="btn-refresh" onClick={onClose}>
                Close
              </button>
            </div>
          </div>
        )}
        {detail.data && <EditForm patient={detail.data} onClose={onClose} onSaved={onSaved} />}
      </div>
    </div>
  );
}

function EditForm({
  patient,
  onClose,
  onSaved,
}: {
  patient: PatientDetail;
  onClose: () => void;
  onSaved: (patient: PatientDetail) => void;
}) {
  const settings = useQuery({ queryKey: ["schedule", "settings"], queryFn: fetchScheduleSettings });
  const locations = settings.data?.locations ?? [];
  const therapists = (settings.data?.providers ?? []).filter((p) => p.userId);

  const initial = {
    firstName: patient.firstName,
    lastName: patient.lastName,
    dateOfBirth: patient.dateOfBirth,
    phone: patient.phone ?? "",
    email: patient.email ?? "",
    address: patient.address ?? "",
    emergencyContact: patient.emergencyContact ?? "",
    preferredLanguage: patient.preferredLanguage ?? "",
    primaryLocationId: patient.primaryLocationId ?? "",
    assignedTherapistId: patient.assignedTherapistId ?? "",
    status: String(patient.status),
  };
  const [form, setForm] = useState(initial);
  const set = (key: keyof typeof initial) => (e: { target: { value: string } }) =>
    setForm((f) => ({ ...f, [key]: e.target.value }));

  const today = new Date().toISOString().slice(0, 10);
  const dobInvalid = !form.dateOfBirth || form.dateOfBirth > today || form.dateOfBirth < "1900-01-01";
  const emailInvalid = !!form.email.trim() && !/^[^\s@]+@[^\s@]+$/.test(form.email.trim());
  const valid = form.firstName.trim() && form.lastName.trim() && !dobInvalid && !emailInvalid;
  const dirty = (Object.keys(initial) as (keyof typeof initial)[]).some((k) => form[k].trim() !== initial[k].trim());

  const save = useMutation({
    mutationFn: () => {
      const text = (v: string) => v.trim() || null;
      return updatePatient(patient.id, {
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        dateOfBirth: form.dateOfBirth,
        phone: text(form.phone),
        email: text(form.email),
        address: text(form.address),
        emergencyContact: text(form.emergencyContact),
        preferredLanguage: text(form.preferredLanguage),
        assignedTherapistId: form.assignedTherapistId || null,
        primaryLocationId: form.primaryLocationId || null,
        // Not on this form: sent back as they were.
        primaryCareProviderId: patient.primaryCareProviderId,
        referringProviderId: patient.referringProviderId,
        status: Number(form.status) as PatientStatus,
      });
    },
    onSuccess: (saved) => onSaved(saved),
  });

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (valid && dirty) save.mutate();
  };

  return (
    <form onSubmit={onSubmit} noValidate className="flex min-h-0 flex-col">
      <div className="space-y-4 overflow-y-auto px-6 py-5">
        {save.isError && <p className="alert-error">{save.error.message}</p>}
        <FormRow label="Name" htmlFor="ep-first" missing={!form.firstName.trim() || !form.lastName.trim()}>
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
            <input
              id="ep-first"
              aria-label="First name"
              placeholder="First name"
              autoFocus
              className="field-input"
              value={form.firstName}
              onChange={set("firstName")}
            />
            <input
              aria-label="Last name"
              placeholder="Last name"
              className="field-input"
              value={form.lastName}
              onChange={set("lastName")}
            />
          </div>
        </FormRow>
        <FormRow label="Date of Birth" htmlFor="ep-dob" missing={!form.dateOfBirth}>
          <input
            id="ep-dob"
            type="date"
            min="1900-01-01"
            max={today}
            className="field-input"
            value={form.dateOfBirth}
            onChange={set("dateOfBirth")}
          />
          {form.dateOfBirth && dobInvalid && <p className="mt-1 text-danger">Enter a valid date of birth (not in the future).</p>}
        </FormRow>
        <FormRow label="Status">
          <SegmentedButtons
            label="Patient status"
            value={form.status}
            onChange={(v) => setForm((f) => ({ ...f, status: v }))}
            options={STATUS_CHOICES}
          />
        </FormRow>
        <FormRow label="Phone" htmlFor="ep-phone">
          <input id="ep-phone" type="tel" className="field-input" value={form.phone} onChange={set("phone")} />
        </FormRow>
        <FormRow label="E-Mail Address" htmlFor="ep-email">
          <input id="ep-email" type="email" className="field-input" value={form.email} onChange={set("email")} />
          {emailInvalid && <p className="mt-1 text-danger">Enter a valid email address.</p>}
        </FormRow>
        <FormRow label="Address" htmlFor="ep-address">
          <input id="ep-address" className="field-input" value={form.address} onChange={set("address")} />
        </FormRow>
        <FormRow label="Emergency Contact" htmlFor="ep-emergency">
          <input
            id="ep-emergency"
            className="field-input"
            placeholder="Name, relationship, phone"
            value={form.emergencyContact}
            onChange={set("emergencyContact")}
          />
        </FormRow>
        <FormRow label="Language" htmlFor="ep-language">
          <select
            id="ep-language"
            className="field-input"
            value={form.preferredLanguage}
            onChange={set("preferredLanguage")}
          >
            <option value="">—</option>
            {form.preferredLanguage && !LANGUAGES.includes(form.preferredLanguage) && (
              <option>{form.preferredLanguage}</option>
            )}
            {LANGUAGES.map((l) => (
              <option key={l}>{l}</option>
            ))}
          </select>
        </FormRow>
        <FormRow label="Primary Location" htmlFor="ep-location">
          <select
            id="ep-location"
            className="field-input"
            value={form.primaryLocationId}
            onChange={set("primaryLocationId")}
          >
            <option value="">—</option>
            {locations.map((l) => (
              <option key={l.id} value={l.id}>
                {l.name}
              </option>
            ))}
          </select>
        </FormRow>
        <FormRow label="Assigned Therapist" htmlFor="ep-therapist">
          <select
            id="ep-therapist"
            className="field-input"
            value={form.assignedTherapistId}
            onChange={set("assignedTherapistId")}
          >
            <option value="">—</option>
            {therapists.map((t) => (
              <option key={t.id} value={t.userId!}>
                {t.name}
                {t.credentials ? `, ${t.credentials}` : ""}
                {t.isActive ? "" : " (inactive)"}
              </option>
            ))}
          </select>
        </FormRow>
      </div>
      <div className="flex justify-end gap-3 border-t border-border px-6 py-4">
        <button type="button" className="btn-refresh" onClick={onClose}>
          Cancel
        </button>
        <button type="submit" className="btn-primary" disabled={!valid || !dirty || save.isPending}>
          {save.isPending ? "Saving…" : "Save and Close"}
        </button>
      </div>
    </form>
  );
}
