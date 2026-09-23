import { createContext, useContext, type ReactNode } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError } from "../../lib/apiClient";
import { fetchCurrentUser, login, logout, type LoginRequest } from "./api";
import type { CurrentUser } from "./types";

const ME_QUERY_KEY = ["auth", "me"] as const;

interface AuthContextValue {
  user: CurrentUser | null;
  isLoading: boolean;
  loginError: string | null;
  isLoggingIn: boolean;
  signIn: (request: LoginRequest) => Promise<void>;
  signOut: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();

  const meQuery = useQuery({
    queryKey: ME_QUERY_KEY,
    queryFn: fetchCurrentUser,
    retry: false,
    // A 401 here just means "not logged in" -- not a fetch failure worth
    // surfacing as an error state to the rest of the app.
    throwOnError: false,
  });

  const loginMutation = useMutation({
    mutationFn: login,
    onSuccess: (user) => {
      queryClient.setQueryData(ME_QUERY_KEY, user);
    },
  });

  const logoutMutation = useMutation({
    mutationFn: logout,
    onSuccess: () => {
      queryClient.setQueryData(ME_QUERY_KEY, null);
      queryClient.clear();
    },
  });

  const value: AuthContextValue = {
    user: meQuery.data ?? null,
    isLoading: meQuery.isLoading,
    loginError:
      loginMutation.error instanceof ApiError ? loginMutation.error.message : loginMutation.error ? "Sign in failed." : null,
    isLoggingIn: loginMutation.isPending,
    signIn: async (request) => {
      await loginMutation.mutateAsync(request);
    },
    signOut: async () => {
      await logoutMutation.mutateAsync();
    },
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
}
