import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "../features/auth/AuthProvider";
import { canAccess } from "../features/auth/permissions";
import { fetchCurrentOrganization } from "../features/organizations/api";
import { useSelectedLocation } from "../features/organizations/useSelectedLocation";
import { appModules } from "../components/layout/modules";

// Home: organization name, location picker, and one large tile per module
// the signed-in user can open.
export function DashboardPage() {
  const { user } = useAuth();
  const organizationQuery = useQuery({
    queryKey: ["organizations", "current"],
    queryFn: fetchCurrentOrganization,
  });
  const locations = organizationQuery.data?.locations;
  const { selectedId, selectLocation } = useSelectedLocation(locations);

  const tiles = appModules.filter((m) => m.tile && (!m.allowed || canAccess(user, m.allowed)));

  return (
    <div className="flex flex-col items-center py-8 md:py-16">
      <h1 className="text-center text-2xl font-bold text-[#1565b8] md:text-3xl">
        {organizationQuery.isLoading ? "Loading…" : (organizationQuery.data?.name ?? "")}
      </h1>
      {locations && locations.length > 0 && (
        <select
          aria-label="Location"
          className="mt-4 rounded border border-slate-300 bg-white px-5 py-2 text-base text-text shadow-sm focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/30"
          value={selectedId ?? ""}
          onChange={(e) => selectLocation(e.target.value)}
        >
          {locations.map((location) => (
            <option key={location.id} value={location.id}>
              {location.name}
            </option>
          ))}
        </select>
      )}

      <div className="mt-12 grid grid-cols-1 gap-x-16 gap-y-12 sm:grid-cols-2">
        {tiles.map((m) => (
          <Link key={m.to} to={m.to} className="group flex items-center gap-4">
            <span
              className={`relative flex h-24 w-24 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-gradient-to-br ${m.tileGradient} text-white shadow-[3px_4px_8px_rgba(0,0,0,0.35)] transition group-hover:scale-105 md:h-28 md:w-28`}
            >
              {/* Glossy highlight across the top half. */}
              <span className="absolute inset-x-0 top-0 h-1/2 bg-gradient-to-b from-white/45 to-white/0" aria-hidden="true" />
              <span className="relative h-14 w-14 drop-shadow-md md:h-16 md:w-16 [&>svg]:h-full [&>svg]:w-full">{m.icon}</span>
            </span>
            <span className="text-2xl font-bold text-[#1565b8] group-hover:underline md:text-[28px]">{m.label}</span>
          </Link>
        ))}
      </div>
    </div>
  );
}
