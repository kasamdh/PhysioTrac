import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { RowMenu } from "../../../components/RowMenu";
import { SegmentedButtons } from "../../../components/SegmentedButtons";
import { useToast } from "../../../components/Toast";
import { AdminPageHeader } from "../AdminPageHeader";
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
    <div className="mx-auto max-w-6xl">
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
        <LocationForm
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
                  <td>
                    <button type="button" className="table-link text-left" onClick={() => setEditing(l)}>
                      {l.name}
                    </button>
                  </td>
                  <td className="text-text-muted">
                    {[l.addressLine1, l.addressLine2].filter(Boolean).join(", ")}
                    {(l.city || l.state) && (
                      <span className="block">{[l.city, [l.state, l.zipCode].filter(Boolean).join(" ")].filter(Boolean).join(", ")}</span>
                    )}
                  </td>
                  <td>{l.phone}</td>
                  <td>{timeZoneLabel(l.timezone)}</td>
                  <td>{l.npiNumber}</td>
                  <td>{l.isActive ? "Active" : "Inactive"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}

function LocationForm({
  existing,
  onClose,
  onSaved,
}: {
  existing: AdminLocation | null;
  onClose: () => void;
  onSaved: (location: AdminLocation, isNew: boolean) => void;
}) {
  const [form, setForm] = useState<LocationInput>(existing ? toInput(existing) : EMPTY);
  const save = useMutation({
    mutationFn: () => (existing ? updateLocation(existing.id, toRequest(form)) : createLocation(toRequest(form))),
    onSuccess: (l) => onSaved(l, !existing),
  });

  const field = (key: keyof LocationInput, label: string, props: Record<string, unknown> = {}) => (
    <label className="field-label">
      {label}
      <input
        className="field-input mt-1"
        value={form[key] ?? ""}
        onChange={(e) => setForm({ ...form, [key]: e.target.value })}
        {...props}
      />
    </label>
  );

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    save.mutate();
  };

  return (
    <form onSubmit={onSubmit} className="card mb-5">
      <h2 className="mb-4 text-lg font-semibold text-text">{existing ? `Edit ${existing.name}` : "Add location"}</h2>
      {save.isError && <p className="alert-error mb-4">{save.error.message}</p>}
      <div className="grid grid-cols-1 gap-x-4 gap-y-3 sm:grid-cols-2 lg:grid-cols-3">
        {field("name", "Name *", { required: true, autoFocus: true })}
        {field("phone", "Phone", { type: "tel" })}
        <label className="field-label">
          Time zone *
          <select
            className="field-input mt-1"
            value={form.timezone}
            onChange={(e) => setForm({ ...form, timezone: e.target.value })}
          >
            {TIME_ZONES.map(([id, label]) => (
              <option key={id} value={id}>
                {label}
              </option>
            ))}
          </select>
        </label>
        {field("addressLine1", "Address line 1")}
        {field("addressLine2", "Address line 2")}
        {field("city", "City")}
        {field("state", "State", { maxLength: 2, placeholder: "NC" })}
        {field("zipCode", "ZIP code", { maxLength: 10 })}
        {field("npiNumber", "NPI", { maxLength: 10, inputMode: "numeric" })}
        {field("taxId", "Tax ID")}
      </div>
      <div className="mt-5 flex justify-end gap-2">
        <button type="button" className="btn-secondary" onClick={onClose}>
          Cancel
        </button>
        <button type="submit" className="btn-primary" disabled={save.isPending || !form.name.trim()}>
          {save.isPending ? "Saving…" : existing ? "Save changes" : "Add location"}
        </button>
      </div>
    </form>
  );
}
