import { NavLink, Outlet } from "react-router-dom";
import { useAuth } from "../../features/auth/AuthProvider";
import { UserRoleLabels } from "../../features/auth/types";

// Same route set as the Blazor staff UI's nav (src/PhysioTrac.Web/Components/Layout/MainLayout.razor)
// -- these link out to pages this SPA foundation doesn't build yet
// (Phase 2 is the shell + auth + dashboard; each of these becomes a real
// page in a later phase, backed by the same Api endpoints).
const navItems = [
  { to: "/", label: "Dashboard", end: true },
  { to: "/patients", label: "Patients" },
  { to: "/schedule", label: "Schedule" },
  { to: "/providers", label: "Providers" },
  { to: "/billing", label: "Billing" },
];

function navLinkClass({ isActive }: { isActive: boolean }) {
  return [
    "block rounded-md px-3 py-2 text-sm font-medium transition",
    isActive ? "bg-primary-light text-primary-deep" : "text-text-muted hover:bg-surface-muted hover:text-text",
  ].join(" ");
}

export function AppLayout() {
  const { user, signOut } = useAuth();

  return (
    <div className="flex min-h-screen bg-surface-muted">
      <aside className="flex w-60 shrink-0 flex-col border-r border-border bg-surface">
        <div className="flex items-center gap-2 border-b border-border px-4 py-4">
          <span className="flex h-8 w-8 items-center justify-center rounded-md bg-primary text-sm font-bold text-white">
            P
          </span>
          <span className="font-semibold text-text">PhysioTrac</span>
        </div>
        <nav className="flex-1 space-y-1 p-3">
          {navItems.map((item) => (
            <NavLink key={item.to} to={item.to} end={item.end} className={navLinkClass}>
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
      <main className="flex-1 overflow-y-auto p-8">
        <Outlet />
      </main>
    </div>
  );
}
