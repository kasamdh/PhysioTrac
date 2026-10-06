import { useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "../features/auth/AuthProvider";
import { BrandMark, BrandWordmark, HeaderLogo, LegalFooter } from "../components/brand/Brand";

const loginSchema = z.object({
  username: z.string().min(1, "Login User Id is required"),
  password: z.string().min(1, "Login Password is required"),
});

type LoginFormValues = z.infer<typeof loginSchema>;

export function LoginPage() {
  const { user, signIn, isLoggingIn, loginError } = useAuth();
  const [showPassword, setShowPassword] = useState(false);
  const location = useLocation();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginFormValues>({ resolver: zodResolver(loginSchema) });

  if (user) {
    // Keep the query string too, so a deep link like
    // /schedule?view=week&date=... lands on that exact view after login.
    const from = (location.state as { from?: { pathname: string; search?: string } } | null)?.from;
    return <Navigate to={from ? `${from.pathname}${from.search ?? ""}` : "/"} replace />;
  }

  const onSubmit = handleSubmit(async (values) => {
    try {
      await signIn(values);
    } catch {
      // Surfaced via loginError below -- the mutation already recorded it.
    }
  });

  return (
    <div className="flex min-h-screen flex-col bg-surface">
      <header className="app-header-bar flex h-14 shrink-0 items-center px-6 shadow-sm">
        <HeaderLogo />
      </header>

      <div className="flex flex-1 flex-col lg:flex-row">
        {/* Hero -- only beside the sign-in panel from 1024px up; phones and
            portrait iPads get the sign-in panel alone. */}
        <section className="hidden flex-1 flex-col items-center justify-center gap-4 lg:flex">
          <BrandMark className="h-40 w-40" />
          <BrandWordmark className="text-7xl font-bold text-primary-deep lg:text-8xl" />
          <p className="text-sm tracking-wide text-text-muted">Source Motion Physical Therapy</p>
        </section>

        <section className="flex w-full flex-col items-center border-border px-4 py-10 lg:w-[34%] lg:min-w-[400px] lg:justify-center lg:border-l lg:py-8">
          <h1 className="mb-3 text-center">
            <BrandWordmark className="text-6xl font-bold text-brand-accent" />
          </h1>

          <form
            onSubmit={onSubmit}
            noValidate
            className="w-full max-w-[440px] space-y-4 rounded-sm border border-slate-500/60 bg-panel px-10 pt-14 pb-12 shadow-[2px_3px_6px_rgba(0,0,0,0.25)] sm:px-14"
          >
            {loginError && <p className="alert-error">{loginError}</p>}
            <div>
              <label className="sr-only" htmlFor="username">
                Login User Id
              </label>
              <input
                id="username"
                type="text"
                autoComplete="username"
                placeholder="Login User Id"
                className="login-input"
                {...register("username")}
              />
              {errors.username && <p className="mt-1 text-xs font-medium text-red-900">{errors.username.message}</p>}
            </div>
            <div>
              <label className="sr-only" htmlFor="password">
                Login Password
              </label>
              <div className="relative">
                <input
                  id="password"
                  type={showPassword ? "text" : "password"}
                  autoComplete="current-password"
                  placeholder="Login Password"
                  className="login-input pr-11"
                  {...register("password")}
                />
                <button
                  type="button"
                  onClick={() => setShowPassword((v) => !v)}
                  aria-label={showPassword ? "Hide password" : "Show password"}
                  aria-pressed={showPassword}
                  className="absolute inset-y-0 right-0 flex w-11 items-center justify-center text-slate-500 hover:text-primary focus:text-primary focus:outline-none"
                >
                  <EyeIcon open={!showPassword} />
                </button>
              </div>
              {errors.password && <p className="mt-1 text-xs font-medium text-red-900">{errors.password.message}</p>}
            </div>
            <div className="flex justify-center pt-3">
              <button type="submit" disabled={isLoggingIn} className="btn-primary w-48 py-3 text-lg">
                {isLoggingIn ? "Signing in…" : "Login"}
              </button>
            </div>
          </form>
        </section>
      </div>

      <LegalFooter className="px-4 py-3" />
    </div>
  );
}

// Eye = "show password"; eye with a slash = "hide password".
function EyeIcon({ open }: { open: boolean }) {
  return (
    <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z" />
      <circle cx="12" cy="12" r="3" />
      {!open && <path d="M3 3l18 18" />}
    </svg>
  );
}
