import { UserRole, type CurrentUser } from "./types";

// Mirrors PhysioTrac.Application.Tenancy.RoleSets exactly -- keep these two
// in sync by hand; there's no shared source of truth between the .NET and
// TypeScript projects.
export const RoleSets = {
  Clinical: new Set<UserRole>([UserRole.Admin, UserRole.Director, UserRole.Therapist, UserRole.Assistant, UserRole.Compliance]),
  Scheduling: new Set<UserRole>([UserRole.Admin, UserRole.Director, UserRole.Therapist, UserRole.Assistant, UserRole.Scheduler]),
  Billing: new Set<UserRole>([UserRole.Admin, UserRole.Biller, UserRole.Director]),
  DocumentManagement: new Set<UserRole>([
    UserRole.Admin,
    UserRole.Director,
    UserRole.Therapist,
    UserRole.Assistant,
    UserRole.Compliance,
    UserRole.Scheduler,
    UserRole.Biller,
  ]),
  OrganizationAdministration: new Set<UserRole>([UserRole.Admin, UserRole.Director]),
} as const;

export function hasAnyRole(role: UserRole | undefined, allowed: ReadonlySet<UserRole>): boolean {
  return role !== undefined && allowed.has(role);
}

/** Whether the signed-in user may see something gated to `allowed`. While
 * the server reports access control switched off, everyone but a Patient
 * portal account may -- mirroring PhysioTrac.Application.Tenancy.AccessControl. */
export function canAccess(user: CurrentUser | null | undefined, allowed: ReadonlySet<UserRole>): boolean {
  if (!user) return false;
  if (user.accessControlEnabled === false && user.role !== UserRole.Patient) return true;
  return allowed.has(user.role);
}
