import { MutationCache, QueryCache, QueryClient } from "@tanstack/react-query";
import { ApiError, clearCsrfToken } from "./apiClient";

const AUTH_ME_KEY = "auth";

/** Any query/mutation anywhere in the app that comes back 401 means the
 * session ended server-side (idle/absolute timeout, revoked elsewhere,
 * password changed, etc.) -- not just the initial /me check on load. This
 * forces the same "signed out" state /me itself would report, so
 * ProtectedRoute redirects to /login on its next render without every
 * feature having to special-case 401 itself. */
function handlePossibleSessionExpiry(error: unknown) {
  if (error instanceof ApiError && error.status === 401) {
    queryClient.setQueryData([AUTH_ME_KEY, "me"], null);
    clearCsrfToken();
  }
}

export const queryClient = new QueryClient({
  queryCache: new QueryCache({
    onError: handlePossibleSessionExpiry,
  }),
  mutationCache: new MutationCache({
    onError: handlePossibleSessionExpiry,
  }),
  defaultOptions: {
    queries: {
      retry: (failureCount, error) => {
        // Never retry auth/permission failures -- retrying a 401/403
        // just delays the redirect to /login for no benefit.
        if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
          return false;
        }
        return failureCount < 2;
      },
      staleTime: 30_000,
    },
  },
});
