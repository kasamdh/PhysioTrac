import { useState } from "react";
import { NavLink, Outlet } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "../../features/auth/AuthProvider";
import { UserRoleLabels } from "../../features/auth/types";
import { RoleSets, canAccess } from "../../features/auth/permissions";
import { fetchCurrentOrganization } from "../../features/organizations/api";
import { useSelectedLocation } from "../../features/organizations/useSelectedLocation";
import { useIdleTimeout } from "../../hooks/useIdleTimeout";
import { useToast } from "../Toast";
import { IdleTimeoutWarning } from "../IdleTimeoutWarning";
import { RequireRole } from "../RequireRole";

// Same route set as the Blazor staff UI's nav (src/PhysioTrac.Web/Components/Layout/MainLayout.razor)
// -- these link out to pages this SPA foundation doesn't build yet
// (Phase 2 is the shell + auth + dashboard; each of these becomes a real
// page in a later phase, backed by the same Api endpoints). `allowed` is
// undefined for items every authenticated role can see (Dashboard).
const navItems = [
  { to: "/", label: "Dashboard", end: true, allowed: undefined },
  { to: "/patients", label: "Patients", end: false, allowed: RoleSets.Clinical },
  { to: "/schedule", label: "Schedule", end: false, allowed: RoleSets.Scheduling },
  { to: "/providers", label: "Providers", end: false, allowed: RoleSets.Clinical },
  { to: "/billing", label: "Billing", end: false, allowed: RoleSets.Billing },
] as const;

function navLinkClass({ isActive }: { isActive: boolean }) {
  return [
    "block rounded-md px-3 py-2 text-sm font-medium transition",
    isActive ? "bg-primary-light text-primary-deep" : "text-text-muted hover:bg-surface-muted hover:text-text",
  ].join(" ");
}

export function AppLayout() {
  const { user, signOut } = useAuth();
  const { showToast } = useToast();

  const organizationQuery = useQuery({
    queryKey: ["organizations", "current"],
    queryFn: fetchCurrentOrganization,
  });
  const { selectedId, selectLocation } = useSelectedLocation(organizationQuery.data?.locations);

  const { warning, stayActive } = useIdleTimeout(() => {
    void signOut();
    showToast("You were signed out after a period of inactivity.");
  });

  // Below md the sidebar becomes a slide-over drawer behind a menu button,
  // so phone-width pages get the full screen instead of 240px less.
  const [menuOpen, setMenuOpen] = useState(false);

  const visibleNavItems = navItems.filter((item) => !item.allowed || canAccess(user, item.allowed));

  return (
    <div className="flex min-h-screen bg-surface-muted">
      {menuOpen && (
        <div className="fixed inset-0 z-30 bg-slate-900/40 md:hidden" onClick={() => setMenuOpen(false)} aria-hidden="true" />
      )}
      <aside
        id="app-sidebar"
        className={`${
          menuOpen ? "fixed inset-y-0 left-0 z-40 flex shadow-xl" : "hidden"
        } w-60 shrink-0 flex-col border-r border-border bg-surface md:static md:flex md:shadow-none`}
      >
        <div className="flex items-center gap-2 border-b border-border px-4 py-4">
          <span className="flex h-8 w-8 items-center justify-center rounded-md bg-primary text-sm font-bold text-white">
            P
          </span>
          <span className="font-semibold text-text">PhysioTrac</span>
        </div>
        <nav className="flex-1 space-y-1 p-3">
          {visibleNavItems.map((item) => (
            <NavLink key={item.to} to={item.to} end={item.end} className={navLinkClass} onClick={() => setMenuOpen(false)}>
              {item.label}
            </NavLink>
          ))}
        </nav>
        <div className="border-t border-border p-3">
          <div className="mb-2 truncate text-sm text-text">
            {user?.username}
            <span className="block text-xs text-text-muted">{user ? UserRoleLabels[user.role] : ""}</span>
          </div>
          <button type="button" onClick={() => void signOut()} className="btn-secondary w-full justify-center">
            Sign out
          </button>
        </div>
      </aside>
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex flex-wrap items-center justify-between gap-2 border-b border-border bg-surface px-4 py-3 md:px-8">
          <button
            type="button"
            className="btn-secondary md:hidden"
            aria-label="Open menu"
            aria-controls="app-sidebar"
            aria-expanded={menuOpen}
            onClick={() => setMenuOpen(true)}
          >
            ☰ Menu
          </button>
          <div className="min-w-0 flex-1 truncate text-sm font-medium text-text">
            {organizationQuery.isLoading ? "Loading organization…" : (organizationQuery.data?.name ?? "—")}
          </div>
          {/* A patient-portal login has no reason to switch clinic
              locations -- everyone else who might work across more than one
              location does. */}
          <RequireRole allowed={RoleSets.Scheduling}>
            {organizationQuery.data && organizationQuery.data.locations.length > 0 && (
              <label className="flex items-center gap-2 text-sm text-text-muted">
                Location
                <select
                  className="field-input w-auto py-1"
                  value={selectedId ?? ""}
                  onChange={(e) => selectLocation(e.target.value)}
                >
                  {organizationQuery.data.locations.map((location) => (
                    <option key={location.id} value={location.id}>
                      {location.name}
                    </option>
                  ))}
                </select>
              </label>
            )}
          </RequireRole>
        </header>
        <main className="flex-1 overflow-y-auto p-4 md:p-8">
          <Outlet />
        </main>
      </div>
      {warning && (
        <IdleTimeoutWarning
          onStayActive={stayActive}
          onSignOut={() => {
            void signOut();
          }}
        />
      )}
    </div>
  );
}
