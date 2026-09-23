import { apiRequest } from "../../lib/apiClient";
import type { Patient } from "./types";

export function fetchPatients(): Promise<Patient[]> {
  return apiRequest<Patient[]>("/api/v1/patients");
}
