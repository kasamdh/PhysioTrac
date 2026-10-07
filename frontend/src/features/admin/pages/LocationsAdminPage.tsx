import { useEffect, useId, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { RowMenu } from "../../../components/RowMenu";
import { SegmentedButtons } from "../../../components/SegmentedButtons";
import { useToast } from "../../../components/Toast";
import { AdminPageHeader } from "../AdminPageHeader";
import { FormRow } from "../FormRow";
import { createLocation, fetchLocations, setLocationActive, updateLocation } from "../api";
import type { AdminLocation, LocationInput } from "../types";

// Same list the Blazor Locations page offers.
const TIME_ZONES: [string, string][] = [
  ["America/New_York", "Eastern"],
  ["America/Chicago", "Central"],
  ["America/Denver", "Mountain"],
  ["America/Phoenix", "Arizona (no DST)"],
  ["America/Los_Angeles", "Pacific"],
  ["America/Anchorage", "Alaska"],
  ["Pacific/Honolulu", "Hawaii"],
];
const timeZoneLabel = (id: string) => TIME_ZONES.find(([tz]) => tz === id)?.[1] ?? id;

const EMPTY: LocationInput = {
  name: "",
  addressLine1: "",
  addressLine2: "",
  city: "",
  state: "",
  zipCode: "",
  phone: "",
  timezone: "America/New_York",
  npiNumber: "",
  taxId: "",
};

function toInput(l: AdminLocation): LocationInput {
  return {
    name: l.name,
    addressLine1: l.addressLine1 ?? "",
    addressLine2: l.addressLine2 ?? "",
    city: l.city ?? "",
    state: l.state ?? "",
    zipCode: l.zipCode ?? "",
    phone: l.phone ?? "",
    timezone: l.timezone,
    npiNumber: l.npiNumber ?? "",
    taxId: l.taxId ?? "",
  };
}

/** Blank strings go to the API as null so optional columns stay empty. */
function toRequest(input: LocationInput): LocationInput {
  return Object.fromEntries(
    Object.entries(input).map(([k, v]) => [k, typeof v === "string" && v.trim() === "" ? null : v]),
  ) as LocationInput;
}

export function LocationsAdminPage() {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const [showInactive, setShowInactive] = useState(false);
  // null = closed, "new" = adding, otherwise the location being edited.
  const [editing, setEditing] = useState<AdminLocation | "new" | null>(null);

  const locations = useQuery({
    queryKey: ["admin", "locations", showInactive],
    queryFn: () => fetchLocations(showInactive),
  });

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ["admin", "locations"] });
    // The header/home location picker reads the organization's location list.
    void queryClient.invalidateQueries({ queryKey: ["organizations", "current"] });
  };

  const toggleActive = useMutation({
    mutationFn: (l: AdminLocation) => setLocationActive(l.id, !l.isActive),
    onSuccess: (l) => {
      showToast(`${l.name} ${l.isActive ? "reactivated" : "deactivated"}.`);
      refresh();
    },
    onError: (e: Error) => showToast(e.message),
  });

  return (
    <div>
      <AdminPageHeader
        title="Locations"
        actions={
          <button type="button" className="btn-primary" onClick={() => setEditing("new")}>
            + Add location
          </button>
        }
      />

      <div className="list-toolbar">
        <SegmentedButtons
          label="Show"
          value={showInactive ? "all" : "active"}
          onChange={(v) => setShowInactive(v === "all")}
          options={[
            { value: "active", label: "Active" },
            { value: "all", label: "All (incl. inactive)" },
          ]}
        />
        <button
          type="button"
          className="btn-refresh ml-auto"
          disabled={locations.isFetching}
          onClick={() => void locations.refetch()}
        >
          {locations.isFetching ? "Refreshing…" : "Refresh"}
        </button>
      </div>

      {editing && (
        <LocationDialog
          key={editing === "new" ? "new" : editing.id}
          existing={editing === "new" ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={(l, isNew) => {
            showToast(`${l.name} ${isNew ? "added" : "saved"}.`);
            setEditing(null);
            refresh();
          }}
        />
      )}

      <div className="list-wrap">
        {locations.isLoading && <p className="p-5 text-text-muted">Loading…</p>}
        {locations.isError && <p className="alert-error m-5">{locations.error.message}</p>}
        {locations.data && locations.data.length === 0 && <p className="p-5 text-text-muted">No locations yet.</p>}
        {locations.data && locations.data.length > 0 && (
          <table className="data-table">
            <thead>
              <tr>
                <th className="cell-menu">
                  <span className="sr-only">Actions</span>
                </th>
                <th>Name</th>
                <th>Address</th>
                <th>Phone</th>
                <th>Time zone</th>
                <th>NPI</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {locations.data.map((l) => (
                <tr key={l.id} className={l.isActive ? "" : "opacity-60"}>
                  <td className="cell-menu">
                    <RowMenu
                      label={`Actions for ${l.name}`}
                      items={[
                        { label: "Edit", onSelect: () => setEditing(l) },
                        {
                          label: l.isActive ? "Deactivate" : "Reactivate",
                          danger: l.isActive,
                          disabled: toggleActive.isPending,
                          onSelect: () => {
                            if (l.isActive && !window.confirm(`Deactivate ${l.name}? It will no longer be offered for scheduling.`))
                              return;
                            toggleActive.mutate(l);
                          },
                        },
                      ]}
                    />
                  </td>
                  <td data-label="Name">
                    <button type="button" className="table-link text-left" onClick={() => setEditing(l)}>
                      {l.name}
                    </button>
                  </td>
                  <td data-label="Address" className="text-text-muted">
                    <div>
                      {[l.addressLine1, l.addressLine2].filter(Boolean).join(", ")}
                      {(l.city || l.state) && (
                        <span className="block">
                          {[l.city, [l.state, l.zipCode].filter(Boolean).join(" ")].filter(Boolean).join(", ")}
                        </span>
                      )}
                    </div>
                  </td>
                  <td data-label="Phone">{l.phone}</td>
                  <td data-label="Time zone">{timeZoneLabel(l.timezone)}</td>
                  <td data-label="NPI">{l.npiNumber}</td>
                  <td data-label="Status">{l.isActive ? "Active" : "Inactive"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}

/** Add / Edit Location dialog: label-left rows, Name marked "Required"
 * until filled, Cancel / Save and Close (same format as Edit User / Patient). */
function LocationDialog({
  existing,
  onClose,
  onSaved,
}: {
  existing: AdminLocation | null;
  onClose: () => void;
  onSaved: (location: AdminLocation, isNew: boolean) => void;
}) {
  const titleId = useId();
  const initial = existing ? toInput(existing) : EMPTY;
  const [form, setForm] = useState<LocationInput>(initial);
  const save = useMutation({
    mutationFn: () => (existing ? updateLocation(existing.id, toRequest(form)) : createLocation(toRequest(form))),
    onSuccess: (l) => onSaved(l, !existing),
  });

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [onClose]);

  const nameMissing = !(form.name ?? "").trim();
  // Checked only when edited, so an existing odd value never blocks saving other fields.
  const npiChanged = (form.npiNumber ?? "").trim() !== (initial.npiNumber ?? "").trim();
  const npiInvalid = npiChanged && !!form.npiNumber?.trim() && !/^\d{10}$/.test(form.npiNumber.trim());
  const dirty = (Object.keys(initial) as (keyof LocationInput)[]).some(
    (k) => (form[k] ?? "").trim() !== (initial[k] ?? "").trim(),
  );
  const canSave = !nameMissing && !npiInvalid && (dirty || !existing) && !save.isPending;

  const field = (key: keyof LocationInput, props: Record<string, unknown> = {}) => (
    <input
      id={`loc-${key}`}
      className="field-input"
      value={form[key] ?? ""}
      onChange={(e) => setForm({ ...form, [key]: e.target.value })}
      {...props}
    />
  );

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (canSave) save.mutate();
  };

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
        <h2 id={titleId} className="border-b border-border px-6 py-4 text-3xl text-[#333]">
          {existing ? "Edit Location" : "Add Location"}
        </h2>
        <div className="space-y-4 overflow-y-auto px-6 py-5">
          {save.isError && <p className="alert-error">{save.error.message}</p>}
          <FormRow label="Location Name" htmlFor="loc-name" missing={nameMissing}>
            {field("name", { autoFocus: true, autoComplete: "off" })}
          </FormRow>
          <FormRow label="Time Zone" htmlFor="loc-timezone">
            <select
              id="loc-timezone"
              className="field-input"
              value={form.timezone}
              onChange={(e) => setForm({ ...form, timezone: e.target.value })}
            >
              {!TIME_ZONES.some(([id]) => id === form.timezone) && <option value={form.timezone}>{form.timezone}</option>}
              {TIME_ZONES.map(([id, label]) => (
                <option key={id} value={id}>
                  {label}
                </option>
              ))}
            </select>
            <p className="mt-1 text-text-muted">The schedule shows this clinic’s appointments in this time zone.</p>
          </FormRow>
          <FormRow label="Phone" htmlFor="loc-phone">
            {field("phone", { type: "tel" })}
          </FormRow>
          <FormRow label="Address" htmlFor="loc-addressLine1">
            <div className="space-y-2">
              {field("addressLine1", { placeholder: "Street address", "aria-label": "Address line 1" })}
              {field("addressLine2", { placeholder: "Suite, unit (optional)", "aria-label": "Address line 2" })}
            </div>
          </FormRow>
          <FormRow label="City / State / ZIP" htmlFor="loc-city">
            <div className="grid grid-cols-[1fr_5rem_8rem] gap-2">
              {field("city", { placeholder: "City", "aria-label": "City" })}
              {field("state", { placeholder: "NC", maxLength: 2, "aria-label": "State" })}
              {field("zipCode", { placeholder: "ZIP", maxLength: 10, "aria-label": "ZIP code" })}
            </div>
          </FormRow>
          <FormRow label="NPI" htmlFor="loc-npiNumber">
            {field("npiNumber", { maxLength: 10, inputMode: "numeric" })}
            {npiInvalid && <p className="mt-1 text-danger">An NPI is 10 digits.</p>}
          </FormRow>
          <FormRow label="Tax ID" htmlFor="loc-taxId">
            {field("taxId")}
          </FormRow>
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
