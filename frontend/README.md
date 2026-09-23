# PhysioTrac frontend (SPA foundation)

A React + TypeScript + Vite client for `PhysioTrac.Api` — the JSON API
counterpart to the Blazor Server staff UI (`src/PhysioTrac.Web`). This is
the "separate JSON client (mobile app, SPA, etc.)" the root README says
didn't exist yet.

**Scope of this pass**: authentication, routing, protected routes, a
role-filtered sidebar shell with an organization/location header, idle-
timeout warning + auto sign-out, global 401 handling, an error boundary,
and a working dashboard that proves real API integration end to end
(session cookie → `ITenantAccessService`-scoped `/api/v1/patients` data).
The other nav destinations (Patients, Schedule, Providers, Billing) are
placeholder pages — the backend endpoints for them already exist and work
(see `src/PhysioTrac.Api/Controllers`); building each out is later work,
not part of this foundation.

**Deliberately not built as literally specified**: an "access-token
handling, silent refresh" API client. This Api is cookie+CSRF, not JWT (see
root README) — there's no access/refresh token pair to manage client-side.
What this client does instead: sends the session cookie automatically on
every request, and on ANY 401 from ANY query or mutation (not just the
initial load), clears the cached user and lets `ProtectedRoute` redirect to
`/login` (`src/lib/queryClient.ts`'s `QueryCache`/`MutationCache` `onError`)
— the practical equivalent for this auth model.

## Stack

- Vite + React 19 + TypeScript
- Tailwind CSS v4 (same brand tokens as `src/PhysioTrac.Web/Styles/app.tailwind.css`)
- React Router v7
- TanStack Query v5
- React Hook Form + Zod

## How auth works here

`PhysioTrac.Api` uses cookie-based ASP.NET Core Identity, not JWT — see the
root README's Architecture section for why. This client:

1. Fetches a CSRF token once (`GET /api/v1/auth/csrf`) and re-sends it as
   `X-CSRF-TOKEN` on every mutating request (`src/lib/apiClient.ts`).
2. Sends every request with `credentials: "include"` so the session cookie
   round-trips.
3. Tracks the current user via a TanStack Query cache entry
   (`src/features/auth/AuthProvider.tsx`), refetched on load; `/login` and
   `/logout` mutations update that cache directly instead of refetching.

**Known gap, not fixed here**: `PhysioTrac.Api` issues CSRF tokens but no
controller actually validates them yet (no `[ValidateAntiForgeryToken]` /
`IAntiforgery.ValidateRequestAsync` anywhere). This client sends the header
correctly regardless, so nothing breaks the day server-side validation is
added — it's a backend gap, not a frontend one.

## Local setup

```bash
npm install
npm run dev       # http://localhost:5173
```

Requires `PhysioTrac.Api` running at `http://localhost:5080` (see the root
README) — its CORS policy already allows `http://localhost:5173` with
credentials. Override the API URL via `VITE_API_BASE_URL` (see
`.env.development`).

Log in with any of the seeded demo accounts (root README has the full
table), e.g. `admin` / `DemoPass123!`.

## Idle timeout

`src/hooks/useIdleTimeout.ts` mirrors `Security:IdleTimeoutMinutes` on the
Api (default 15 minutes) as a client-side convenience: a warning modal
appears a minute before the timeout, and signs the user out automatically
if they don't respond. This is a UX nicety only, not the real enforcement —
`SessionValidationMiddleware` rejects a stale session server-side
regardless, so closing the laptop still ends the session for real even if
this warning is never seen.

## Role-based navigation

The sidebar filters nav items against `src/features/auth/permissions.ts`'s
`RoleSets`, which mirrors `PhysioTrac.Application.Tenancy.RoleSets`
byte-for-byte (there's no shared source of truth between the two projects —
keep them in sync by hand). `src/components/RequireRole.tsx` is the same
check as a reusable component for hiding any UI element by role; this is a
client-side convenience only; the actual access control is enforced
server-side on every request regardless.

## Project structure

```
src/
  lib/               apiClient (fetch + CSRF + error normalization), TanStack QueryClient (incl. global 401 handling)
  features/
    auth/            types, api calls, AuthProvider + useAuth(), permissions (RoleSets mirror)
    organizations/   current org + locations (header/location switcher)
    patients/        types, api calls (used by the dashboard)
  components/
    ProtectedRoute.tsx      redirects to /login when not authenticated
    RequireRole.tsx         hides children unless the user's role is allowed
    ErrorBoundary.tsx       catches render-time crashes
    Toast.tsx               minimal toast notifications
    IdleTimeoutWarning.tsx  the idle-timeout warning modal
    layout/AppLayout.tsx    sidebar + org/location header shell
  hooks/useIdleTimeout.ts   idle warning + auto sign-out timer
  pages/             LoginPage, DashboardPage, PlaceholderPage
  App.tsx            router + provider wiring
```

## Testing

```bash
npm run test   # vitest run
```

Vitest + React Testing Library, covering login (validation, submit, error
display), route protection (redirect/loading/authenticated states), and
role-based navigation (which nav items each role sees).

## Build

```bash
npm run build   # tsc -b && vite build
```

## Docker

```bash
docker build -t physiotrac-frontend .
```

Multi-stage: `npm ci && npm run build` in a Node image, then the static
`dist/` output served by nginx (`nginx.conf` — SPA fallback routing to
`index.html` so a hard refresh on e.g. `/patients` doesn't 404). See the
root `docker-compose.yml`'s `frontend` service, which builds this
automatically as part of `docker compose up`.
