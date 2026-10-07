import type { UserRole } from "../auth/types";
import type { PatientStatus } from "../patients/types";

// Matches PhysioTrac.Api.Controllers.LocationDto.
export interface AdminLocation {
  id: string;
  name: string;
  addressLine1: string | null;
  addressLine2: string | null;
  city: string | null;
  state: string | null;
  zipCode: string | null;
  phone: string | null;
  timezone: string;
  npiNumber: string | null;
  taxId: string | null;
  isActive: boolean;
}

export type LocationInput = Omit<AdminLocation, "id" | "isActive">;

// Mirrors PhysioTrac.Domain.Enums.UserStatus.
export const UserStatus = {
  Active: 0,
  Inactive: 1,
  LockedOut: 2,
  Suspended: 3,
  Deleted: 4,
} as const;
export type UserStatus = (typeof UserStatus)[keyof typeof UserStatus];

export const UserStatusLabels: Record<UserStatus, string> = {
  [UserStatus.Active]: "Active",
  [UserStatus.Inactive]: "Inactive",
  [UserStatus.LockedOut]: "Locked out",
  [UserStatus.Suspended]: "Suspended",
  [UserStatus.Deleted]: "Deleted",
};

// Matches PhysioTrac.Application.Users.StaffUserDto.
export interface StaffUser {
  id: string;
  userName: string;
  email: string | null;
  firstName: string;
  lastName: string;
  role: UserRole;
  status: UserStatus;
  mustChangePassword: boolean;
}

export interface InviteUserInput {
  firstName: string;
  lastName: string;
  email: string;
  role: UserRole;
}

// Matches PhysioTrac.Application.Users.UpdateUserRequest.
export interface UpdateUserInput {
  userName: string;
  firstName: string;
  lastName: string;
  email: string | null;
  role: UserRole;
  status: UserStatus;
  /** null keeps the current password. */
  newPassword: string | null;
}

export interface InviteUserResult {
  user: StaffUser;
  activationUrl: string;
}

// Matches PhysioTrac.Application.Messaging.MessageThreadSummary.
export interface MessageThread {
  patientId: string;
  patientName: string;
  medicalRecordNumber: string;
  lastMessageAt: string;
  lastMessagePreview: string;
  messageCount: number;
  unreadCount: number;
}

// Matches PhysioTrac.Application.Messaging.MessageDto.
export interface ThreadMessage {
  id: string;
  patientId: string;
  senderId: string;
  senderRole: UserRole;
  isFromPatient: boolean;
  body: string;
  sentAt: string;
  readAt: string | null;
}

// Matches PhysioTrac.Api.Controllers.PatientDirectoryRowDto.
export interface PatientDirectoryRow {
  id: string;
  medicalRecordNumber: string;
  fullName: string;
  dateOfBirth: string;
  age: number;
  phone: string | null;
  email: string | null;
  status: PatientStatus;
  primaryLocationId: string | null;
  primaryLocationName: string | null;
  lastVisitAt: string | null;
  nextAppointmentAt: string | null;
}

export interface PatientDirectoryPage {
  items: PatientDirectoryRow[];
  total: number;
  page: number;
  pageSize: number;
}

export interface PatientDirectoryFilters {
  search?: string;
  status?: PatientStatus;
  locationId?: string;
  appointmentFrom?: string;
  appointmentTo?: string;
  /** true lists soft-deleted charts (for Restore) instead of live ones. */
  deleted?: boolean;
  page?: number;
  pageSize?: number;
}

// Matches PhysioTrac.Application.Patients.PatientDetailDto.
export interface PatientDetail {
  id: string;
  medicalRecordNumber: string;
  firstName: string;
  lastName: string;
  fullName: string;
  dateOfBirth: string;
  age: number;
  phone: string | null;
  email: string | null;
  address: string | null;
  emergencyContact: string | null;
  preferredLanguage: string | null;
  diagnoses: string | null;
  precautions: string | null;
  assignedTherapistId: string | null;
  primaryLocationId: string | null;
  primaryCareProviderId: string | null;
  referringProviderId: string | null;
  status: PatientStatus;
}

// Matches PhysioTrac.Application.Patients.UpdatePatientRequest.
export interface UpdatePatientInput {
  firstName: string;
  lastName: string;
  phone: string | null;
  email: string | null;
  address: string | null;
  emergencyContact: string | null;
  preferredLanguage: string | null;
  assignedTherapistId: string | null;
  primaryLocationId: string | null;
  primaryCareProviderId: string | null;
  referringProviderId: string | null;
  status: PatientStatus;
  dateOfBirth: string | null;
}
