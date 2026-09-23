// Mirrors PhysioTrac.Domain.Enums.PatientStatus. An `as const` object, not a
// plain `enum` -- see the same note in features/auth/types.ts.
export const PatientStatus = {
  Active: 0,
  Inactive: 1,
  Discharged: 2,
} as const;

export type PatientStatus = (typeof PatientStatus)[keyof typeof PatientStatus];

// Matches PhysioTrac.Application.Patients.PatientDto.
export interface Patient {
  id: string;
  medicalRecordNumber: string;
  firstName: string;
  lastName: string;
  fullName: string;
  dateOfBirth: string;
  age: number;
  phone: string | null;
  email: string | null;
  assignedTherapistId: string | null;
  status: PatientStatus;
}
