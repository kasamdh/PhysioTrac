import { apiRequest } from "../../lib/apiClient";
import type { UserRole } from "../auth/types";
import type {
  AdminLocation,
  InviteUserInput,
  InviteUserResult,
  LocationInput,
  MessageThread,
  PatientDirectoryFilters,
  PatientDirectoryPage,
  StaffUser,
  ThreadMessage,
} from "./types";

function query(params: Record<string, string | number | boolean | undefined>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== "") search.set(key, String(value));
  }
  const text = search.toString();
  return text ? `?${text}` : "";
}

// Locations
export const fetchLocations = (includeInactive: boolean) =>
  apiRequest<AdminLocation[]>(`/api/v1/locations${query({ includeInactive })}`);
export const createLocation = (input: LocationInput) =>
  apiRequest<AdminLocation>("/api/v1/locations", { method: "POST", body: input });
export const updateLocation = (id: string, input: LocationInput) =>
  apiRequest<AdminLocation>(`/api/v1/locations/${id}`, { method: "PUT", body: input });
export const setLocationActive = (id: string, active: boolean) =>
  apiRequest<AdminLocation>(`/api/v1/locations/${id}/${active ? "activate" : "deactivate"}`, { method: "PATCH" });

// Users
export const fetchUsers = () => apiRequest<StaffUser[]>("/api/v1/users");
export const inviteUser = (input: InviteUserInput) =>
  apiRequest<InviteUserResult>("/api/v1/users/invite", { method: "POST", body: input });
export const changeUserRole = (id: string, role: UserRole) =>
  apiRequest<StaffUser>(`/api/v1/users/${id}/role`, { method: "PATCH", body: { role } });
export const setUserActive = (id: string, active: boolean) =>
  apiRequest<StaffUser>(`/api/v1/users/${id}/${active ? "activate" : "deactivate"}`, {
    method: "PATCH",
    body: active ? undefined : { reason: null },
  });

// Messages
export const fetchMessageThreads = () => apiRequest<MessageThread[]>("/api/v1/messages/threads");
export const fetchThread = (patientId: string) =>
  apiRequest<ThreadMessage[]>(`/api/v1/patients/${patientId}/messages`);
export const sendMessage = (patientId: string, body: string) =>
  apiRequest<ThreadMessage>(`/api/v1/patients/${patientId}/messages`, { method: "POST", body: { body } });
export const markThreadRead = (patientId: string) =>
  apiRequest<void>(`/api/v1/patients/${patientId}/messages/mark-read`, { method: "POST" });

// Patients
export const fetchPatientDirectory = (filters: PatientDirectoryFilters, signal?: AbortSignal) =>
  apiRequest<PatientDirectoryPage>(`/api/v1/patients/directory${query({ ...filters })}`, { signal });

// Account
export const changePassword = (currentPassword: string, newPassword: string) =>
  apiRequest<void>("/api/v1/auth/change-password", { method: "POST", body: { currentPassword, newPassword } });

// Invitation activation (anonymous)
export const fetchInvitation = (token: string) =>
  apiRequest<{ organizationName: string; email: string | null }>(
    `/api/v1/auth/activate-invitation${query({ token })}`,
  );
