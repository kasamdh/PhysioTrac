# PhysioTrac

PhysioTrac is a clinical EMR for **Source Motion Physical Therapy** — a .NET
8 / Clean Architecture rewrite of the same product family as
[PhysioTrac360](../PhysioTrac360) (Django + React). This project targets
Blazor Server for the UI and ASP.NET Core Web API for a separate JSON
surface, both backed by one SQL Server database.

## Architecture

```
src/
  PhysioTrac.Domain          Entities, enums — no framework dependencies
  PhysioTrac.Application     Interfaces, DTOs, business rules (services live in Infrastructure)
  PhysioTrac.Infrastructure  EF Core DbContext, migrations, service implementations, Identity, seed data
  PhysioTrac.Api             ASP.NET Core Web API (cookie + CSRF auth, Swagger)
  PhysioTrac.Web             Blazor Server app (the staff-facing UI)
tests/
  PhysioTrac.Tests           xUnit tests against an EF Core in-memory provider
frontend/                    React + TypeScript + Vite SPA, talks to PhysioTrac.Api (see frontend/README.md)
```

Multi-tenancy is scoped by `Organization.Id` (referred to as the "client" in
the platform-admin surfaces) — every patient, appointment, note, and billing
record carries an `OrganizationId`, and `ITenantAccessService` is the single
choke point that scopes every query to the caller's organization and role.

Authentication is ASP.NET Core Identity with cookies (not JWT) — a
deliberate choice, not an oversight: `PhysioTrac.Web` is server-rendered
(cookies are the natural fit), and `PhysioTrac.Api` uses the same cookie
plus a CSRF double-submit token (`GET /api/v1/auth/csrf` →
`X-CSRF-TOKEN` header) for any JSON client. `PhysioTrac.Web` never calls
`PhysioTrac.Api` over HTTP — it talks to the Application-layer services
directly via dependency injection in the same process; `PhysioTrac.Api`
exists for a separate JSON client — `frontend/`, a React SPA, is that
client (see `frontend/README.md`).

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server (2019+), or Docker (see [Run with Docker](#run-with-docker) below)
- The [standalone Tailwind CSS CLI](https://github.com/tailwindlabs/tailwindcss/releases/latest)
  (only needed to build `PhysioTrac.Web`'s stylesheet locally — Docker fetches
  it automatically). See `src/PhysioTrac.Web/Styles/README.md`.

## Local setup (bare metal)

1. **Restore and build**

   ```powershell
   dotnet restore
   dotnet build
   ```

2. **Point the app at a SQL Server instance.** The default connection
   string in `appsettings.json` targets LocalDB
   (`(localdb)\MSSQLLocalDB`). If you're using a named instance instead
   (e.g. SQL Server Express), override it in
   `src/PhysioTrac.Api/appsettings.Development.json` and
   `src/PhysioTrac.Web/appsettings.Development.json` (gitignored-friendly —
   edit your local copies):

   ```json
   {
     "ConnectionStrings": {
       "Default": "Server=.\\SQLEXPRESS;Database=PhysioTrac;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
     }
   }
   ```

3. **Apply migrations.** `PhysioTrac.Api` carries the EF Core design-time
   package, so it's the startup project for tooling:

   ```powershell
   dotnet tool restore
   dotnet ef database update --project src/PhysioTrac.Infrastructure --startup-project src/PhysioTrac.Api
   ```

4. **Fetch the Tailwind CLI** (one-time, per machine) so `PhysioTrac.Web`
   builds its stylesheet — see `src/PhysioTrac.Web/Styles/README.md`.

5. **Set the demo seed password.** `appsettings.Development.json` is
   committed to git, so the seeded users' password is never hardcoded there
   or in source — it's read from config at startup and the app refuses to
   start the seeder if it's missing. Set it once via .NET User Secrets:

   ```powershell
   dotnet user-secrets set "Seed:DemoPassword" "DemoPass123!" --project src/PhysioTrac.Api
   ```

   (or set the `Seed__DemoPassword` environment variable instead, if you
   prefer). Use whatever value you like locally — it only ever touches your
   own dev database.

6. **Run it.** In Development, `PhysioTrac.Api` also auto-migrates and
   seeds demo data on startup (see [Demo data](#demo-data)) — so running the
   API once is enough to get a ready-to-use database.

   ```powershell
   dotnet run --project src/PhysioTrac.Api    # http://localhost:5080, Swagger at /swagger
   dotnet run --project src/PhysioTrac.Web    # http://localhost:5073
   ```

7. **Optional: run the React SPA** (a second, separate client of the same
   API — see [Architecture](#architecture)):

   ```powershell
   cd frontend
   npm install
   npm run dev    # http://localhost:5173
   ```

   See `frontend/README.md` for details.

## Run with Docker

No local .NET SDK, SQL Server, or Tailwind CLI needed — everything is built
inside the containers.

```bash
docker compose up --build
```

This starts SQL Server, then `PhysioTrac.Api` (which applies migrations and
seeds demo data on first boot), then `PhysioTrac.Web` once the API reports
healthy. Once it's up:

- Web UI: http://localhost:5073
- API + Swagger: http://localhost:5080/swagger

Override the SQL `sa` password by setting `SQL_SA_PASSWORD` in your
environment or a `.env` file before running `docker compose up` — never
commit a real password to source control.

You must also set `SEED_DEMO_PASSWORD` (same way — environment or `.env`);
`docker compose up` refuses to start the `api` service without it, since
the demo users' password is never hardcoded in `docker-compose.yml` or in
source:

```bash
echo "SEED_DEMO_PASSWORD=DemoPass123!" >> .env
```

## Demo data

`PhysioTrac.Infrastructure.Seed.DemoDataSeeder` seeds two independent
tenants — Source Motion Physical Therapy (org 1000) and Total Motion PT
(org 1001) — the first time the API starts in Development, if they aren't
already present. It's idempotent — safe to restart the app as many times as
you like. It never runs when `ASPNETCORE_ENVIRONMENT` isn't `Development`,
so a production database is never auto-seeded with known credentials.

Every role except SuperAdmin is seeded in **both** organizations (`tm.`-
prefixed usernames for Total Motion); SuperAdmin is a platform-level account
seeded once, with no organization at all, since a real `SuperAdmin` account
can never have a standing `OrganizationId`.

| Username        | Org           | Password        | Role       |
|-----------------|---------------|-----------------|------------|
| `superadmin`    | *(platform)*  | `DemoPass123!`  | SuperAdmin |
| `admin`         | Source Motion | `DemoPass123!`  | Admin      |
| `director`      | Source Motion | `DemoPass123!`  | Director   |
| `therapist`     | Source Motion | `DemoPass123!`  | Therapist  |
| `assistant`     | Source Motion | `DemoPass123!`  | Assistant  |
| `scheduler`     | Source Motion | `DemoPass123!`  | Scheduler  |
| `biller`        | Source Motion | `DemoPass123!`  | Biller     |
| `compliance`    | Source Motion | `DemoPass123!`  | Compliance |
| `patient`       | Source Motion | `DemoPass123!`  | Patient (portal login linked to Taylor Brooks' chart) |
| `tm.admin`      | Total Motion  | `DemoPass123!`  | Admin      |
| `tm.director`   | Total Motion  | `DemoPass123!`  | Director   |
| `tm.therapist`  | Total Motion  | `DemoPass123!`  | Therapist  |
| `tm.assistant`  | Total Motion  | `DemoPass123!`  | Assistant  |
| `tm.scheduler`  | Total Motion  | `DemoPass123!`  | Scheduler  |
| `tm.biller`     | Total Motion  | `DemoPass123!`  | Biller     |
| `tm.compliance` | Total Motion  | `DemoPass123!`  | Compliance |
| `tm.patient`    | Total Motion  | `DemoPass123!`  | Patient (portal login linked to Jordan Ellis' chart) |

Also seeded per organization: patients, appointment types, service prices,
and (Source Motion only) an insurance payer — enough that every page in the
app has real data to show immediately.

## Authorization, and audit

Role checks happen at two layers that deliberately read the same "role"
claim, so they can never disagree: `ITenantAccessService.RequireRole` inside
an action body (the original, still-primary mechanism across all
controllers), and a small ASP.NET Core policy layer
(`PhysioTrac.Api.Authorization.RoleSetAuthorizationHandler` +
`PermissionPolicies`) usable via `[Authorize(Policy = ...)]` for a
declarative, request-never-reaches-the-action check. See
`ReferringProvidersController.Create` for the two used together.

Two audit mechanisms write to the same append-only `AuditEvents` table:
- `ITenantAccessService`/services call `IAuditService` directly for
  specific security-relevant events (e.g. `access.denied` on a cross-tenant
  access attempt; `auth.login.success`/`auth.login.failed`/`auth.logout` in
  `AuthController`).
- `EntityChangeAuditInterceptor` (an EF Core `SaveChangesInterceptor`)
  automatically writes `entity.created`/`entity.updated`/`entity.deleted`
  events for any entity with its own `OrganizationId` property, with no
  per-service opt-in call. Metadata never includes field values — only the
  names of changed properties — to keep PHI out of the audit log itself.
  Entities scoped only indirectly through a relation (e.g. `Appointment`,
  `ClinicalNote` via `PatientId`) aren't covered by this automatic
  mechanism yet.

## Running tests

```powershell
dotnet test tests/PhysioTrac.Tests/PhysioTrac.Tests.csproj
```

Tests run against EF Core's in-memory provider, not a real database — fast,
but this means schema-level issues (constraint violations, cascade-path
conflicts, missing migrations) only surface against a real SQL Server
instance. If you're changing the data model, apply the migration and
exercise it against a real database before considering the change verified.

## Logging

`PhysioTrac.Api` logs through Serilog (console + a rolling daily file under
`src/PhysioTrac.Api/logs/`, gitignored) instead of the default
`Microsoft.Extensions.Logging` console formatter — structured, and
consistent between `dotnet run` and Docker.

A custom `PhiRedactingDestructuringPolicy` masks likely-PHI property values
(name, DOB, email, phone, SSN, address, diagnosis, etc., matched by property
name) whenever an object is logged with structured (`{@Thing}`) destructuring.
This is a best-effort safety net, not a compliance guarantee: it only
catches structured destructuring, not PHI interpolated directly into a log
message string — that still requires callers not to do it. See
`src/PhysioTrac.Api/Logging/PhiRedactingDestructuringPolicy.cs` and its
tests in `tests/PhysioTrac.Tests/PhiRedactingDestructuringPolicyTests.cs`.

Any exception that escapes a controller's own handling (most already catch
`ForbiddenException`/`NotFoundException`) is caught by a global
`ExceptionHandlingMiddleware`, logged in full server-side, and returned to
the client as a fixed generic `{ "detail": "An unexpected error occurred." }`
— never the exception's own message or stack trace, since either could
echo PHI back to the client.

## Production considerations

This is a HIPAA-oriented application **foundation**, not a certified-
compliant product as shipped. Before any real patient data goes into a
production deployment of this app, you still need: a signed BAA with your
hosting provider, database encryption at rest and in transit, MFA/SSO for
staff accounts, audit-log retention policy, PHI-safe logging (verify no
identifiers leak into application logs or error trackers), and a real
secrets-management story for connection strings and the SQL `sa`/app
credentials (never the defaults in this README or in `docker-compose.yml`).
