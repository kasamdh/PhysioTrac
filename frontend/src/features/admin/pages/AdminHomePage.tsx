import { Link } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import { RoleSets, canAccess } from "../../auth/permissions";
import type { UserRole } from "../../auth/types";

// `allowed` undefined = every signed-in user (changing your own password).
const commands: { to: string; label: string; allowed?: ReadonlySet<UserRole> }[] = [
  { to: "/admin/change-password", label: "Change Password" },
  { to: "/admin/messages", label: "Messages", allowed: RoleSets.DocumentManagement },
  { to: "/admin/users", label: "Users", allowed: RoleSets.OrganizationAdministration },
  { to: "/admin/locations", label: "Locations", allowed: RoleSets.OrganizationAdministration },
  { to: "/admin/patients", label: "Patient List", allowed: RoleSets.Clinical },
  { to: "/admin/logs", label: "Logs", allowed: RoleSets.AuditLogReview },
];

export function AdminHomePage() {
  const { user } = useAuth();
  const visible = commands.filter((c) => !c.allowed || canAccess(user, c.allowed));

  return (
    <div className="flex flex-col items-center py-8 md:py-14">
      <h1 className="text-2xl font-bold text-[#1565b8] md:text-3xl">Administration</h1>
      <div className="mt-10 grid w-full max-w-[840px] grid-cols-1 gap-x-11 gap-y-11 sm:grid-cols-2">
        {visible.map((c) => (
          <Link key={c.to} to={c.to} className="admin-button">
            {c.label}
          </Link>
        ))}
      </div>
    </div>
  );
}
