const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5080";

export class ApiError extends Error {
  readonly status: number;
  readonly code?: string;

  constructor(message: string, status: number, code?: string) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.code = code;
  }
}

/**
 * Server-issued CSRF token (double-submit pattern) -- fetched once via
 * GET /api/v1/auth/csrf and re-sent as X-CSRF-TOKEN on every mutating
 * request, per the pattern PhysioTrac.Api's AuthController comments
 * describe. Note: as of this writing the Api registers antiforgery
 * services and issues tokens but has no controller actually validating
 * them yet (no [ValidateAntiForgeryToken] / IAntiforgery.ValidateRequestAsync
 * call anywhere) -- this client sends the header regardless, so nothing
 * breaks the day that validation is wired up server-side.
 */
let csrfToken: string | null = null;

async function ensureCsrfToken(): Promise<string | null> {
  if (csrfToken) return csrfToken;
  const response = await fetch(`${API_BASE_URL}/api/v1/auth/csrf`, {
    credentials: "include",
  });
  if (!response.ok) return null;
  const data = (await response.json()) as { token: string };
  csrfToken = data.token;
  return csrfToken;
}

/** Call after logout so a stale token from the previous session is never reused. */
export function clearCsrfToken(): void {
  csrfToken = null;
}

export interface RequestOptions {
  method?: "GET" | "POST" | "PATCH" | "PUT" | "DELETE";
  body?: unknown;
  signal?: AbortSignal;
}

/**
 * Thin fetch wrapper for PhysioTrac.Api: always sends the session cookie
 * (credentials: "include"), attaches the CSRF header on mutating requests,
 * and normalizes error responses (the Api returns `{ detail: string }` or
 * `{ errors: string[] }`) into a single ApiError.
 */
export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const method = options.method ?? "GET";
  const headers: Record<string, string> = {};

  let body: string | undefined;
  if (options.body !== undefined) {
    headers["Content-Type"] = "application/json";
    body = JSON.stringify(options.body);
  }

  if (method !== "GET") {
    const token = await ensureCsrfToken();
    if (token) headers["X-CSRF-TOKEN"] = token;
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    method,
    headers,
    body,
    credentials: "include",
    signal: options.signal,
  });

  if (response.status === 204) {
    return undefined as T;
  }

  const isJson = response.headers.get("content-type")?.includes("application/json");
  const payload = isJson ? await response.json() : undefined;

  if (!response.ok) {
    const detail =
      payload?.detail ??
      (Array.isArray(payload?.errors) ? payload.errors.join(", ") : null) ??
      response.statusText;
    throw new ApiError(detail, response.status, payload?.code);
  }

  return payload as T;
}
