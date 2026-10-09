import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Navigate, useSearchParams } from "react-router-dom";
import { BrandWordmark, HeaderLogo, LegalFooter } from "../../../components/brand/Brand";
import { ME_QUERY_KEY } from "../../auth/AuthProvider";
import { activateInvitation } from "../../auth/api";
import { fetchInvitation } from "../api";
import { PasswordField, passwordProblems } from "../PasswordField";

const MIN_LENGTH = 12; // AuthController.ActivateInvitation's own minimum.

/** Public page behind the one-time link from Administration › Users
 * (`/{org-slug}/activate?token=...`): the new user sets their first password
 * and is signed straight in. */
export function ActivateAccountPage() {
  const [params] = useSearchParams();
  const token = params.get("token") ?? "";
  const queryClient = useQueryClient();
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");

  const invitation = useQuery({
    queryKey: ["invitation", token],
    queryFn: () => fetchInvitation(token),
    enabled: !!token,
    retry: false,
  });
  const activate = useMutation({
    mutationFn: () => activateInvitation(token, password),
    onSuccess: (user) => queryClient.setQueryData(ME_QUERY_KEY, user),
  });

  if (activate.isSuccess) return <Navigate to="/" replace />;

  const problems = password ? passwordProblems(password, MIN_LENGTH) : [];
  const mismatch = confirm.length > 0 && confirm !== password;

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    activate.mutate();
  };

  return (
    <div className="flex min-h-screen flex-col bg-surface">
      <header className="app-header-bar flex h-14 shrink-0 items-center px-6 shadow-sm">
        <HeaderLogo />
      </header>
      <main className="flex flex-1 items-center justify-center px-4 py-10">
        <div className="w-full max-w-[440px]">
          <h1 className="mb-3 text-center">
            <BrandWordmark className="text-5xl font-bold text-brand-accent" />
          </h1>
          <div className="rounded-sm border border-slate-500/60 bg-panel px-8 py-8 shadow-[2px_3px_6px_rgba(0,0,0,0.25)]">
            {!token || invitation.isError ? (
              <p className="rounded bg-white px-4 py-3 text-sm text-text">
                {token ? invitation.error?.message : "This link is missing its invitation token."} Ask your administrator
                for a new link.
              </p>
            ) : invitation.isLoading ? (
              <p className="text-center text-white">Checking your invitation…</p>
            ) : (
              <form onSubmit={onSubmit} className="space-y-4">
                <p className="rounded bg-white/90 px-3 py-2 text-sm text-text">
                  Welcome to <strong>{invitation.data?.organizationName}</strong>. Set a password to activate{" "}
                  {invitation.data?.email ? <strong>{invitation.data.email}</strong> : "your account"}.
                </p>
                {activate.isError && <p className="alert-error">{activate.error.message}</p>}
                <div className="rounded bg-white/90 p-3">
                  <PasswordField
                    id="activate-password"
                    label="New password"
                    autoComplete="new-password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                  />
                  <p className={`mt-1 text-xs ${problems.length ? "text-danger" : "text-text-muted"}`}>
                    {problems.length
                      ? `Needs ${problems.join(", ")}.`
                      : `${MIN_LENGTH}+ characters with an uppercase letter, a lowercase letter, a number and a symbol.`}
                  </p>
                  <div className="mt-3">
                    <PasswordField
                      id="activate-confirm"
                      label="Confirm password"
                      autoComplete="new-password"
                      value={confirm}
                      onChange={(e) => setConfirm(e.target.value)}
                    />
                    {mismatch && <p className="mt-1 text-xs text-danger">Passwords don't match.</p>}
                  </div>
                </div>
                <div className="flex justify-center">
                  <button
                    type="submit"
                    className="btn-primary w-48 py-3 text-lg"
                    disabled={!password || problems.length > 0 || confirm !== password || activate.isPending}
                  >
                    {activate.isPending ? "Activating…" : "Activate"}
                  </button>
                </div>
              </form>
            )}
          </div>
        </div>
      </main>
      <LegalFooter className="px-4 py-3" />
    </div>
  );
}
