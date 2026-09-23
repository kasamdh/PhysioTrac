import { UserRole } from "./types";

// Mirrors PhysioTrac.Application.Tenancy.RoleSets exactly -- keep these two
// in sync by hand; there's no shared source of truth between the .NET and
// TypeScript projects.
export const RoleSets = {
  Clinical: new Set<UserRole>([UserRole.Admin, UserRole.Director, UserRole.Therapist, UserRole.Assistant, UserRole.Compliance]),
  Scheduling: new Set<UserRole>([UserRole.Admin, UserRole.Director, UserRole.Therapist, UserRole.Assistant, UserRole.Scheduler]),
  Billing: new Set<UserRole>([UserRole.Admin, UserRole.Biller, UserRole.Director]),
} as const;

export function hasAnyRole(role: UserRole | undefined, allowed: ReadonlySet<UserRole>): boolean {
  return role !== undefined && allowed.has(role);
}
