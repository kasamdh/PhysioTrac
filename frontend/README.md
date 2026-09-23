# PhysioTrac frontend (SPA foundation)

A React + TypeScript + Vite client for `PhysioTrac.Api` — the JSON API
counterpart to the Blazor Server staff UI (`src/PhysioTrac.Web`). This is
the "separate JSON client (mobile app, SPA, etc.)" the root README says
didn't exist yet.

**Scope of this pass**: authentication, routing, protected routes, sidebar
shell, and a working dashboard that proves real API integration end to end
(session cookie → `ITenantAccessService`-scoped `/api/v1/patients` data).
The other nav destinations (Patients, Schedule, Providers, Billing) are
placeholder pages — the backend endpoints for them already exist and work
(see `src/PhysioTrac.Api/Controllers`); building each out is later work,
not part of this foundation.

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

## Project structure

```
src/
  lib/               apiClient (fetch + CSRF + error normalization), TanStack QueryClient
  features/
    auth/            types, api calls, AuthProvider + useAuth()
    patients/        types, api calls (used by the dashboard)
  components/
    ProtectedRoute.tsx     redirects to /login when not authenticated
    layout/AppLayout.tsx   sidebar + topbar shell
  pages/             LoginPage, DashboardPage, PlaceholderPage
  App.tsx            router + provider wiring
```

## Build

```bash
npm run build   # tsc -b && vite build
```
