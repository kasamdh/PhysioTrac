import { useState, type FormEvent } from "react";
import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { RowMenu } from "../../../components/RowMenu";
import { useToast } from "../../../components/Toast";
import { ApiError } from "../../../lib/apiClient";
import { AdminPageHeader } from "../../admin/AdminPageHeader";
import { useAuth } from "../../auth/AuthProvider";
import { RoleSets, canAccess } from "../../auth/permissions";
import { SpecialtyLabels } from "../../templates/types";
import {
  createSpecialTest,
  fetchSpecialTests,
  setSpecialTestActive,
  updateSpecialTest,
  type SaveSpecialTestBody,
} from "./api";
import type { SpecialTestDefinition } from "./types";

const KIND_LABELS: Record<number, string> = {
  0: "Positive / negative",
  1: "Numeric",
  2: "Both",
};

/** The special-test library: built-in tests and the clinic's own, with
 * search, activation and (for the clinic's own) editing. */
export function SpecialTestsAdminPage() {
  const { user } = useAuth();
  const canManage = canAccess(user, RoleSets.OrganizationAdministration);
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const [search, setSearch] = useState("");
  const [specialty, setSpecialty] = useState("");
  const [editing, setEditing] = useState<SpecialTestDefinition | "new" | null>(
    null,
  );
  const filter = {
    search,
    specialty: specialty === "" ? null : Number(specialty),
    includeInactive: true,
  };
  const list = useQuery({
    queryKey: ["special-tests", "admin", filter],
    queryFn: () => fetchSpecialTests(filter),
    placeholderData: keepPreviousData,
  });
  const refresh = () =>
    void queryClient.invalidateQueries({ queryKey: ["special-tests"] });
  const toggle = useMutation({
    mutationFn: (d: SpecialTestDefinition) =>
      setSpecialTestActive(d.id, !d.isActive),
    onSuccess: (d) => {
      showToast(`${d.name} ${d.isActive ? "activated" : "deactivated"}.`);
      refresh();
    },
    onError: (e: Error) => showToast(e.message),
  });

  return (
    <div>
      <AdminPageHeader
        title="Special Tests Library"
        actions={
          canManage && (
            <button
              type="button"
              className="btn-primary"
              onClick={() => setEditing("new")}
            >
              + Add test
            </button>
          )
        }
      />
      <div className="list-toolbar">
        <label className="toolbar-label">
          Search
          <input
            type="search"
            className="toolbar-select"
            value={search}
            placeholder="Name or region"
            onChange={(e) => setSearch(e.target.value)}
          />
        </label>
        <label className="toolbar-label">
          Specialty
          <select
            className="toolbar-select"
            value={specialty}
            onChange={(e) => setSpecialty(e.target.value)}
          >
            <option value="">All</option>
            {Object.entries(SpecialtyLabels).map(([v, l]) => (
              <option key={v} value={v}>
                {l}
              </option>
            ))}
          </select>
        </label>
      </div>
      {editing && (
        <TestForm
          existing={editing === "new" ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={(d) => {
            showToast(`${d.name} saved.`);
            setEditing(null);
            refresh();
          }}
        />
      )}
      <div className="list-wrap">
        {list.isLoading && <p className="p-5 text-text-muted">Loading…</p>}
        {list.isError && (
          <p className="alert-error m-5">{list.error.message}</p>
        )}
        {!!list.data?.length && (
          <table className="data-table">
            <thead>
              <tr>
                <th className="cell-menu">
                  <span className="sr-only">Actions</span>
                </th>
                <th>Test</th>
                <th>Specialty</th>
                <th>Region</th>
                <th>Result</th>
                <th>Source</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {list.data.map((d) => (
                <tr key={d.id} className={d.isActive ? "" : "opacity-60"}>
                  <td className="cell-menu">
                    {canManage && (
                      <RowMenu
                        label={`Actions for ${d.name}`}
                        items={[
                          ...(!d.isSystem
                            ? [{ label: "Edit", onSelect: () => setEditing(d) }]
                            : []),
                          {
                            label: d.isActive ? "Deactivate" : "Activate",
                            onSelect: () => toggle.mutate(d),
                          },
                        ]}
                      />
                    )}
                  </td>
                  <td data-label="Test">
                    {d.name}
                    {d.contraindicationWarning && (
                      <span className="ml-2 text-danger">⚠</span>
                    )}
                  </td>
                  <td data-label="Specialty">{SpecialtyLabels[d.specialty]}</td>
                  <td data-label="Region">{d.bodyRegion ?? "—"}</td>
                  <td data-label="Result">
                    {KIND_LABELS[d.resultKind]}
                    {d.unit ? ` (${d.unit})` : ""}
                  </td>
                  <td data-label="Source">
                    {d.isSystem ? "Built-in" : "Clinic"}
                  </td>
                  <td data-label="Status">
                    {d.isActive ? "Active" : "Inactive"}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}

function TestForm({
  existing,
  onClose,
  onSaved,
}: {
  existing: SpecialTestDefinition | null;
  onClose: () => void;
  onSaved: (d: SpecialTestDefinition) => void;
}) {
  const [f, setF] = useState<SaveSpecialTestBody>({
    name: existing?.name ?? "",
    specialty: existing?.specialty ?? 1,
    resultKind: existing?.resultKind ?? 0,
    bodyRegion: existing?.bodyRegion ?? null,
    description: existing?.description ?? null,
    unit: existing?.unit ?? null,
    interpretationGuide: existing?.interpretationGuide ?? null,
    contraindicationWarning: existing?.contraindicationWarning ?? null,
  });
  const [errors, setErrors] = useState<string[]>([]);
  const save = useMutation({
    mutationFn: () =>
      existing ? updateSpecialTest(existing.id, f) : createSpecialTest(f),
    onSuccess: onSaved,
    onError: (e: Error) => {
      const payload =
        e instanceof ApiError
          ? (e.payload as { errors?: string[] } | undefined)
          : undefined;
      setErrors(payload?.errors?.length ? payload.errors : [e.message]);
    },
  });
  const text = (k: keyof SaveSpecialTestBody, label: string, long = false) => (
    <label className={`block ${long ? "md:col-span-2" : ""}`}>
      <span className="font-bold text-[#333]">{label}</span>
      {long ? (
        <textarea
          className="field-input mt-1 min-h-[4rem]"
          maxLength={1000}
          value={(f[k] as string | null) ?? ""}
          onChange={(e) => setF({ ...f, [k]: e.target.value || null })}
        />
      ) : (
        <input
          className="field-input mt-1"
          maxLength={150}
          value={(f[k] as string | null) ?? ""}
          onChange={(e) => setF({ ...f, [k]: e.target.value || null })}
        />
      )}
    </label>
  );
  return (
    <form
      aria-label={existing ? "Edit special test" : "Add special test"}
      className="mb-4 grid gap-3 rounded-lg border border-border bg-white p-4 md:grid-cols-2"
      onSubmit={(e: FormEvent) => {
        e.preventDefault();
        save.mutate();
      }}
    >
      {errors.length > 0 && (
        <ul role="alert" className="alert-error ml-5 list-disc md:col-span-2">
          {errors.map((x) => (
            <li key={x}>{x}</li>
          ))}
        </ul>
      )}
      <label className="block">
        <span className="font-bold text-[#333]">Test name</span>
        <input
          className="field-input mt-1"
          maxLength={150}
          value={f.name}
          onChange={(e) => setF({ ...f, name: e.target.value })}
        />
      </label>
      <label className="block">
        <span className="font-bold text-[#333]">Specialty</span>
        <select
          className="field-input mt-1"
          value={f.specialty}
          onChange={(e) => setF({ ...f, specialty: Number(e.target.value) })}
        >
          {Object.entries(SpecialtyLabels).map(([v, l]) => (
            <option key={v} value={v}>
              {l}
            </option>
          ))}
        </select>
      </label>
      {text("bodyRegion", "Body region")}
      <label className="block">
        <span className="font-bold text-[#333]">Result</span>
        <select
          className="field-input mt-1"
          value={f.resultKind}
          onChange={(e) => setF({ ...f, resultKind: Number(e.target.value) })}
        >
          {Object.entries(KIND_LABELS).map(([v, l]) => (
            <option key={v} value={v}>
              {l}
            </option>
          ))}
        </select>
      </label>
      {f.resultKind !== 0 && text("unit", "Unit")}
      {text("description", "Description", true)}
      {text(
        "interpretationGuide",
        "Interpretation guide (reference for clinicians)",
        true,
      )}
      {text(
        "contraindicationWarning",
        "Contraindication / precaution warning",
        true,
      )}
      <div className="flex gap-2 md:col-span-2">
        <button
          type="submit"
          className="btn-primary"
          disabled={!f.name.trim() || save.isPending}
        >
          {save.isPending ? "Saving…" : "Save test"}
        </button>
        <button type="button" className="btn-refresh" onClick={onClose}>
          Cancel
        </button>
      </div>
    </form>
  );
}
