import { apiRequest, clearCsrfToken } from "../../lib/apiClient";
import type { CurrentUser } from "./types";

export interface LoginRequest {
  username: string;
  password: string;
}

export function login(request: LoginRequest): Promise<CurrentUser> {
  return apiRequest<CurrentUser>("/api/v1/auth/login", { method: "POST", body: request });
}

export async function logout(): Promise<void> {
  await apiRequest<void>("/api/v1/auth/logout", { method: "POST" });
  clearCsrfToken();
}

/** Rejects with ApiError(401) when there's no active session -- callers
 * treat that as "not logged in", not an unexpected error. */
export function fetchCurrentUser(): Promise<CurrentUser> {
  return apiRequest<CurrentUser>("/api/v1/auth/me");
}
