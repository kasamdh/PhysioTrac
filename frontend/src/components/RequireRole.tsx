import type { ReactNode } from "react";
import { useAuth } from "../features/auth/AuthProvider";
import { hasAnyRole } from "../features/auth/permissions";
import type { UserRole } from "../features/auth/types";

/** Renders `children` only when the signed-in user's role is in `allowed`.
 * Used for the sidebar's role-filtered nav, and reusable anywhere a page
 * needs to hide (not just disable) a control the caller's role can't use --
 * this is a client-side convenience only, never the actual access control,
 * which is enforced server-side on every request regardless. */
export function RequireRole({ allowed, children }: { allowed: ReadonlySet<UserRole>; children: ReactNode }) {
  const { user } = useAuth();
  if (!hasAnyRole(user?.role, allowed)) return null;
  return <>{children}</>;
}
