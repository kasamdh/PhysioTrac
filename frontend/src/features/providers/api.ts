import { apiRequest } from "../../lib/apiClient";
import type { Provider } from "./types";

export const fetchProviders = () => apiRequest<Provider[]>("/api/v1/providers");
