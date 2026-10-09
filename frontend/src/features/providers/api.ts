import { apiRequest } from "../../lib/apiClient";
import type { LinkableUser, Provider, ProviderInput } from "./types";

export const fetchProviders = () => apiRequest<Provider[]>("/api/v1/providers");

export const createProvider = (input: ProviderInput) =>
  apiRequest<Provider>("/api/v1/providers", { method: "POST", body: input });

/** Sends the login every time (updateLogin), so clearing it unlinks it. */
export const updateProvider = (id: string, input: ProviderInput & { isActive: boolean }) =>
  apiRequest<Provider>(`/api/v1/providers/${id}`, {
    method: "PUT",
    body: { ...input, updateLogin: true },
  });

/** Deactivate / reactivate without touching anything else. */
export const setProviderActive = (p: Provider, isActive: boolean) =>
  apiRequest<Provider>(`/api/v1/providers/${p.id}`, {
    method: "PUT",
    body: {
      firstName: p.firstName,
      lastName: p.lastName,
      specialty: p.specialty,
      credentials: p.credentials,
      npiNumber: p.npiNumber,
      isActive,
      locationIds: null,
    },
  });

/** Refused (409) for a provider with appointments, notes or charges. */
export const deleteProvider = (id: string) =>
  apiRequest<void>(`/api/v1/providers/${id}`, { method: "DELETE" });

export const fetchLinkableUsers = (providerId?: string) =>
  apiRequest<LinkableUser[]>(
    `/api/v1/providers/linkable-users${providerId ? `?providerId=${providerId}` : ""}`,
  );
