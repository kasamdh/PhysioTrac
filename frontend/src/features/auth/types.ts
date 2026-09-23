// Mirrors PhysioTrac.Domain.Enums.UserRole exactly (numeric values matter --
// the Api serializes the enum by ordinal, e.g. Admin -> 1). A plain `enum`
// isn't used here since it isn't erasable-syntax-safe under this project's
// TypeScript config; an `as const` object + derived union type is the
// erasable equivalent.
export const UserRole = {
  SuperAdmin: 0,
  Admin: 1,
  Director: 2,
  Therapist: 3,
  Assistant: 4,
  Scheduler: 5,
  Biller: 6,
  Compliance: 7,
  Patient: 8,
} as const;

export type UserRole = (typeof UserRole)[keyof typeof UserRole];

export const UserRoleLabels: Record<UserRole, string> = {
  [UserRole.SuperAdmin]: "Super Admin",
  [UserRole.Admin]: "Admin",
  [UserRole.Director]: "Director",
  [UserRole.Therapist]: "Therapist",
  [UserRole.Assistant]: "Assistant",
  [UserRole.Scheduler]: "Scheduler",
  [UserRole.Biller]: "Biller",
  [UserRole.Compliance]: "Compliance",
  [UserRole.Patient]: "Patient",
};

// Matches PhysioTrac.Api.Contracts.MeResponse (System.Text.Json default
// camelCase output).
export interface CurrentUser {
  id: string;
  username: string;
  email: string | null;
  role: UserRole;
  organizationId: string | null;
  isPlatformSuperAdmin: boolean;
  mustChangePassword: boolean;
}
