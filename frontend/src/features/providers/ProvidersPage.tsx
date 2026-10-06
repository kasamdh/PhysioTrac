import { useQuery } from "@tanstack/react-query";
import { Link, useSearchParams } from "react-router-dom";
import { RowMenu } from "../../components/RowMenu";
import { SegmentedButtons } from "../../components/SegmentedButtons";
import { fetchLocations } from "../admin/api";
import { fetchProviders } from "./api";
import { DisciplineLabels, ProviderDiscipline } from "./types";

type DisciplineFilter = "all" | "pt" | "pta" | "other";
type StatusFilter = "active" | "inactive" | "all";

const disciplineOf: Record<Exclude<DisciplineFilter, "all">, ProviderDiscipline> = {
  pt: ProviderDiscipline.PT,
  pta: ProviderDiscipline.PTA,
  other: ProviderDiscipline.Other,
};

/** Every provider in the organization. Filters live in the URL. */
export function ProvidersPage() {
  const [params, setParams] = useSearchParams();
  const discipline = (params.get("discipline") as DisciplineFilter | null) ?? "all";
  const status = (params.get("status") as StatusFilter | null) ?? "active";
  const locationId = params.get("location") ?? "";
  const search = params.get("q") ?? "";

  const setParam = (key: string, value: string, fallback: string) =>
    setParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        if (value && value !== fallback) next.set(key, value);
        else next.delete(key);
        return next;
      },
      { replace: true },
    );

  const providers = useQuery({ queryKey: ["providers"], queryFn: fetchProviders });
  const locations = useQuery({ queryKey: ["admin", "locations", true], queryFn: () => fetchLocations(true) });
  const locationName = new Map((locations.data ?? []).map((l) => [l.id, l.name]));

  const term = search.trim().toLowerCase();
  const rows = (providers.data ?? []).filter(
    (p) =>
      (discipline === "all" || p.discipline === disciplineOf[discipline]) &&
      (status === "all" || p.isActive === (status === "active")) &&
      (!locationId || p.locationIds.includes(locationId)) &&
      (!term || `${p.fullName} ${p.npiNumber ?? ""} ${p.specialty ?? ""}`.toLowerCase().includes(term)),
  );

  return (
    <div>
      <h1 className="mb-4 text-2xl font-bold text-[#1565b8]">Providers</h1>

      <div className="list-toolbar">
        <SegmentedButtons
          label="Discipline"
          value={discipline}
          onChange={(v) => setParam("discipline", v, "all")}
          options={[
            { value: "all", label: "All" },
            { value: "pt", label: "PT" },
            { value: "pta", label: "PTA" },
            { value: "other", label: "Other" },
          ]}
        />
        <SegmentedButtons
          label="Status"
          value={status}
          onChange={(v) => setParam("status", v, "active")}
          options={[
            { value: "active", label: "Active" },
            { value: "inactive", label: "Inactive" },
            { value: "all", label: "All" },
          ]}
        />
        <label className="toolbar-label">
          Location
          <select className="toolbar-select" value={locationId} onChange={(e) => setParam("location", e.target.value, "")}>
            <option value="">...All Locations...</option>
            {locations.data?.map((l) => (
              <option key={l.id} value={l.id}>
                {l.name}
              </option>
            ))}
          </select>
        </label>
        <input
          type="search"
          aria-label="Search providers"
          className="toolbar-select w-56"
          placeholder="Search name, NPI, specialty"
          value={search}
          onChange={(e) => setParam("q", e.target.value, "")}
        />
        <button
          type="button"
          className="btn-refresh ml-auto"
          disabled={providers.isFetching}
          onClick={() => void providers.refetch()}
        >
          {providers.isFetching ? "Refreshing…" : "Refresh"}
        </button>
      </div>

      <div className="list-wrap">
        {providers.isLoading && <p className="p-5 text-text-muted">Loading…</p>}
        {providers.isError && <p className="alert-error m-5">{providers.error.message}</p>}
        {providers.data && rows.length === 0 && <p className="p-5 text-text-muted">No providers match these filters.</p>}
        {rows.length > 0 && (
          <table className="data-table">
            <thead>
              <tr>
                <th className="cell-menu">
                  <span className="sr-only">Actions</span>
                </th>
                <th>Name</th>
                <th>Discipline</th>
                <th>Credentials</th>
                <th>Specialty</th>
                <th>NPI</th>
                <th>Locations</th>
                <th>Login</th>
                <th>Online booking</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((p) => (
                <tr key={p.id}>
                  <td className="cell-menu">
                    <RowMenu
                      label={`Actions for ${p.fullName}`}
                      items={[
                        { label: "View today's schedule", to: `/schedule?view=day&provider=${p.id}` },
                        { label: "Working hours & time off", to: `/schedule/hours?provider=${p.id}` },
                      ]}
                    />
                  </td>
                  <td data-label="Name">
                    <Link to={`/schedule/hours?provider=${p.id}`} className="table-link">
                      {p.fullName}
                    </Link>
                  </td>
                  <td data-label="Discipline">{DisciplineLabels[p.discipline]}</td>
                  <td data-label="Credentials">{p.credentials}</td>
                  <td data-label="Specialty">{p.specialty}</td>
                  <td data-label="NPI">{p.npiNumber}</td>
                  <td data-label="Locations">{p.locationIds.map((id) => locationName.get(id)).filter(Boolean).join(", ") || "—"}</td>
                  <td data-label="Login">{p.hasLogin ? "Yes" : "No"}</td>
                  <td data-label="Online booking">{p.onlineBookingEnabled ? "Yes" : "No"}</td>
                  <td data-label="Status">{p.isActive ? "Active" : "Inactive"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
      {providers.data && (
        <p className="mt-3 text-sm text-text-muted">
          {rows.length} of {providers.data.length} provider{providers.data.length === 1 ? "" : "s"}
        </p>
      )}
    </div>
  );
}
