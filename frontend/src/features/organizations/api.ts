import { apiRequest } from "../../lib/apiClient";
import type { CurrentOrganization } from "./types";

export function fetchCurrentOrganization(): Promise<CurrentOrganization> {
  return apiRequest<CurrentOrganization>("/api/v1/organizations/current");
}
