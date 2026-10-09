import { useEffect, useId, useState, type FormEvent } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { FormRow } from "../admin/FormRow";
import type { AdminLocation } from "../admin/types";
import { createProvider, fetchLinkableUsers, updateProvider } from "./api";
import {
  DisciplineLabels,
  ProviderDiscipline,
  type Provider,
  type ProviderInput,
} from "./types";

const blank = (s: string) => (s.trim() === "" ? null : s.trim());

/** Add / Edit Provider: same label-left layout as Add / Edit Location. */
export function ProviderDialog({
  existing,
  locations,
  onClose,
  onSaved,
}: {
  existing: Provider | null;
  locations: AdminLocation[];
  onClose: () => void;
  onSaved: (provider: Provider, isNew: boolean) => void;
}) {
  const titleId = useId();
  const [firstName, setFirstName] = useState(existing?.firstName ?? "");
  const [lastName, setLastName] = useState(existing?.lastName ?? "");
  const [credentials, setCredentials] = useState(existing?.credentials ?? "");
  const [specialty, setSpecialty] = useState(existing?.specialty ?? "");
  const [npi, setNpi] = useState(existing?.npiNumber ?? "");
  const [discipline, setDiscipline] = useState<ProviderDiscipline>(
    existing?.discipline ?? ProviderDiscipline.PT,
  );
  const [online, setOnline] = useState(existing?.onlineBookingEnabled ?? true);
  const [active, setActive] = useState(existing?.isActive ?? true);
  const [locationIds, setLocationIds] = useState<string[]>(
    existing?.locationIds ?? [],
  );
  const [userId, setUserId] = useState(existing?.userId ?? "");

  const logins = useQuery({
    queryKey: ["providers", "linkable-users", existing?.id ?? "new"],
    queryFn: () => fetchLinkableUsers(existing?.id),
  });

  const input = (): ProviderInput => ({
    firstName: firstName.trim(),
    lastName: lastName.trim(),
    credentials: blank(credentials),
    specialty: blank(specialty),
    npiNumber: blank(npi),
    discipline,
    onlineBookingEnabled: online,
    locationIds,
    userId: userId || null,
  });
  const save = useMutation({
    mutationFn: () =>
      existing
        ? updateProvider(existing.id, { ...input(), isActive: active })
        : createProvider(input()),
    onSuccess: (p) => onSaved(p, !existing),
  });

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [onClose]);

  const firstMissing = !firstName.trim();
  const lastMissing = !lastName.trim();
  const npiInvalid = !!npi.trim() && !/^\d{10}$/.test(npi.trim());
  const canSave = !firstMissing && !lastMissing && !npiInvalid && !save.isPending;

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (canSave) save.mutate();
  };
  const toggleLocation = (id: string) =>
    setLocationIds((ids) =>
      ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id],
    );

  // Locations: active ones, plus any inactive one the provider already has.
  const shownLocations = locations.filter(
    (l) => l.isActive || locationIds.includes(l.id),
  );

  return (
    <div className="modal-overlay" onClick={onClose}>
      <form
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        onSubmit={onSubmit}
        onClick={(e) => e.stopPropagation()}
        noValidate
        className="flex max-h-[92vh] w-full max-w-2xl flex-col overflow-hidden rounded-lg bg-white shadow-2xl"
      >
        <h2
          id={titleId}
          className="border-b border-border px-6 py-4 text-3xl text-[#333]"
        >
          {existing ? "Edit Provider" : "Add Provider"}
        </h2>
        <div className="space-y-4 overflow-y-auto px-6 py-5">
          {save.isError && (
            <p role="alert" className="alert-error">
              {save.error.message}
            </p>
          )}
          <FormRow label="First Name" htmlFor="prov-first" missing={firstMissing}>
            <input
              id="prov-first"
              className="field-input"
              autoFocus
              autoComplete="off"
              value={firstName}
              onChange={(e) => setFirstName(e.target.value)}
            />
          </FormRow>
          <FormRow label="Last Name" htmlFor="prov-last" missing={lastMissing}>
            <input
              id="prov-last"
              className="field-input"
              autoComplete="off"
              value={lastName}
              onChange={(e) => setLastName(e.target.value)}
            />
          </FormRow>
          <FormRow label="Discipline" htmlFor="prov-discipline">
            <select
              id="prov-discipline"
              className="field-input"
              value={discipline}
              onChange={(e) =>
                setDiscipline(Number(e.target.value) as ProviderDiscipline)
              }
            >
              {[ProviderDiscipline.PT, ProviderDiscipline.PTA, ProviderDiscipline.Other].map(
                (d) => (
                  <option key={d} value={d}>
                    {DisciplineLabels[d]}
                  </option>
                ),
              )}
            </select>
            <p className="mt-1 text-text-muted">
              PTAs can’t be booked for evaluations, re-evaluations or
              discharges.
            </p>
          </FormRow>
          <FormRow label="Credentials" htmlFor="prov-credentials">
            <input
              id="prov-credentials"
              className="field-input"
              placeholder="e.g. PT, DPT"
              value={credentials}
              onChange={(e) => setCredentials(e.target.value)}
            />
          </FormRow>
          <FormRow label="Specialty" htmlFor="prov-specialty">
            <input
              id="prov-specialty"
              className="field-input"
              placeholder="e.g. Orthopedics"
              value={specialty}
              onChange={(e) => setSpecialty(e.target.value)}
            />
          </FormRow>
          <FormRow label="NPI" htmlFor="prov-npi">
            <input
              id="prov-npi"
              className="field-input"
              inputMode="numeric"
              maxLength={10}
              value={npi}
              aria-invalid={npiInvalid}
              onChange={(e) => setNpi(e.target.value)}
            />
            {npiInvalid && <p className="mt-1 text-danger">An NPI is 10 digits.</p>}
          </FormRow>
          <FormRow label="Locations">
            <fieldset>
              <legend className="sr-only">Locations</legend>
              {shownLocations.length === 0 && (
                <p className="text-text-muted">No locations yet.</p>
              )}
              <div className="flex flex-wrap gap-x-5 gap-y-1">
                {shownLocations.map((l) => (
                  <label key={l.id} className="flex min-h-11 items-center gap-2">
                    <input
                      type="checkbox"
                      className="h-5 w-5"
                      checked={locationIds.includes(l.id)}
                      onChange={() => toggleLocation(l.id)}
                    />
                    {l.name}
                    {!l.isActive && " (inactive)"}
                  </label>
                ))}
              </div>
            </fieldset>
          </FormRow>
          <FormRow label="Login" htmlFor="prov-login">
            <select
              id="prov-login"
              className="field-input"
              value={userId}
              onChange={(e) => setUserId(e.target.value)}
            >
              <option value="">No login</option>
              {logins.data?.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.name} ({u.userName}, {u.role})
                </option>
              ))}
            </select>
            <p className="mt-1 text-text-muted">
              The staff account this provider signs in with. Their own
              schedule and notes follow this login.
            </p>
          </FormRow>
          <FormRow label="Online Booking">
            <label className="flex min-h-11 items-center gap-2">
              <input
                type="checkbox"
                className="h-5 w-5"
                checked={online}
                onChange={(e) => setOnline(e.target.checked)}
              />
              Patients can book this provider online
            </label>
          </FormRow>
          {existing && (
            <FormRow label="Status">
              <label className="flex min-h-11 items-center gap-2">
                <input
                  type="checkbox"
                  className="h-5 w-5"
                  checked={active}
                  onChange={(e) => setActive(e.target.checked)}
                />
                Active (offered for scheduling)
              </label>
            </FormRow>
          )}
        </div>
        <div className="flex justify-end gap-3 border-t border-border px-6 py-4">
          <button type="button" className="btn-refresh" onClick={onClose}>
            Cancel
          </button>
          <button type="submit" className="btn-primary" disabled={!canSave}>
            {save.isPending ? "Saving…" : "Save and Close"}
          </button>
        </div>
      </form>
    </div>
  );
}
