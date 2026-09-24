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

## Organization administration

Two separate controllers manage organizations, matching a strict rule: a
**Platform Super Admin** manages any organization (provisioning, suspend/
activate/archive) via `SuperAdminClientsController`
(`/api/v1/super-admin/clients`, `RequirePlatformSuperAdmin`-gated); an
**Organization Admin** (or Director) manages only their own via:

- `OrganizationsController` — `GET/PUT /api/v1/organizations/profile` (name,
  timezone, NPI, Tax ID, address, support contact, PTA cosign policy).
- `LocationsController` — full CRUD for clinic locations
  (`/api/v1/locations`), including each location's own NPI/Tax ID (a
  location can bill under its own Type 2 NPI distinct from the
  organization's). Reads are open to any staff role; writes require
  `RoleSets.OrganizationAdministration` (Admin/Director). No hard delete —
  `PATCH /{id}/deactivate` sets `IsActive = false`.
- `UsersController` — staff list/invite/role-change/activate/deactivate
  (`/api/v1/users`), org-scoped and `OrganizationAdministration`-gated.
  Invite reuses the same hashed-token scheme `SuperAdminClientsController`
  uses for a new organization's first admin (`InvitationTokenGenerator`);
  deactivating a user immediately revokes their active sessions (not just a
  status flag) via `ISessionService.RevokeAllForUserAsync`.
- `ProvidersController` — provider profiles (specialty, credentials, NPI)
  and their location assignments now support full update, not just create.
- `ProviderLicensesController` + `ExpiringLicensesController` — PT/PTA
  state licenses with PT Compact privilege tracking; `ProviderLicense.
  ExpirationAlertLevel` buckets a license into `Notice90`/`Notice60`/
  `Notice30`/`Expired`, and `GET /api/v1/providers/licenses/expiring`
  reports every alertable license across the organization, most urgent
  first.

Every mutation here is audited: `Location`/`Provider` changes are picked up
automatically by `EntityChangeAuditInterceptor` (they carry their own
`OrganizationId`); `Organization`/`ProviderLicense`/`ApplicationUser`
changes don't (scoped indirectly, or not `BaseEntity`), so their
controllers/services write an explicit `IAuditService` event instead.

**Not built in this pass**: documentation/consent template assignment by
organization, state, or location. There's currently no reusable "consent
template" concept at all — `Consent.ConsentText` is a free-text snapshot
typed in at signing time, not read from a stored template. State-specific
consent language is a real compliance feature (which template applies —
location-specific, then state-specific, then an org-wide default) that
deserves its own design rather than being bolted onto an already-large
phase; flagging it here as the one item from this phase not attempted yet.

## Patient management

`PatientsController` (`/api/v1/patients`) now supports, beyond the original
CRUD:

- **Search/filter/sort/pagination**: `?search=&status=&sortBy=&descending=&page=&pageSize=`.
  `search` is a simple contains-match on name/MRN (no full-text search
  infra); `sortBy` is a fixed vocabulary (`lastName` default, `dateOfBirth`,
  `createdAt`) switched in code, never a client-supplied column name.
- **Soft delete**: `DELETE /{id}` sets `DeletedAt`, never removes the row
  (billing/audit history must survive it) — `TenantAccessService.PatientsFor`
  excludes soft-deleted charts by default, so a deleted patient reads as
  inaccessible (403), not silently missing. `PATCH /{id}/restore` undoes it.
- **View audit**: opening a chart (`GET /{id}`) writes a `patient.viewed`
  audit event — deliberately only there, not inside the shared
  `RequirePatientAccessAsync` chokepoint every other patient-scoped
  controller also calls, since auditing "viewed" on every one of those
  (Update, documents, consents, messages, insurance...) would just be noise
  on top of the specific event each of those already records.
- **Timeline**: `GET /{id}/timeline` returns appointments/notes/documents/
  invoices/payments, newest first, grouped by module. `forms` is always an
  empty array — there's no patient-facing intake-forms module in this app
  yet, and this keeps the shape consistent rather than omitting the key.
- **Registration fields**: preferred language, primary location, and a
  primary care provider distinct from the referring provider (both
  reference `ReferringProvider` — an outside physician, not this clinic's
  own staff).

New child-record controllers, each resolving the patient via
`RequirePatientAccessAsync` first (same tenant/caseload scoping as the
chart itself):

- `PatientAllergiesController` (`/api/v1/patients/{id}/allergies`) — severity-
  tiered, never hard-deleted (a corrected allergy is deactivated with a
  reason, not erased).
- `PatientMedicationsController` (`/api/v1/patients/{id}/medications`) —
  active/discontinued med list.
- `PatientDiagnosesController` (`/api/v1/patients/{id}/diagnoses`) +
  `DiagnosisCodesController` (`/api/v1/diagnosis-codes`, read-only ICD-10-CM
  catalog search) — structured, billing-grade diagnoses additive to (not a
  replacement for) the existing free-text `Patient.Diagnoses` chart-header
  summary.

Secure patient documents (`PatientDocumentsController`,
`DocumentService`) already existed with everything this phase asked for:
a storage abstraction (`IFileStorage` — local disk today via
`LocalFileStorage`, swappable for blob storage without touching any
controller), file-type/size validation (`StorageOptions`), and audit events
on both upload and download.

## Scheduling and calendar

Backend for "Phase 4"; the day/week/month React calendar UI itself
(filtering, drag-and-drop) is the natural next phase, same sequencing as
the last three. What already existed from earlier phases: `Appointment`,
`AppointmentType` (org-configurable), `ProviderAvailability`,
`ProviderTimeOff` (blocked time), `Waitlist` (the entity), and a basic
therapist-only double-booking check. Built the rest:

- **Conflict detection for all three named resources** — provider, patient,
  and room — not just the therapist check that existed before.
  `AppointmentService.FindConflictAsync` checks all four fields
  (Therapist/Provider/Patient/Room) against every overlapping, non-
  cancelled/non-no-show appointment; used by both `CreateAsync` and the new
  `RescheduleAsync`. "Room" is a new, minimal `Room` entity
  (`/api/v1/locations/{id}/rooms`) — a location has many rooms; there was
  no resource to conflict-check against before this.
- **Status transitions + history** — `Confirmed` added to `AppointmentStatus`
  (was missing); `Confirm`/`CheckIn`/`Complete`/`MarkNoShow` actions each
  validate legal prior states (e.g. `Complete` requires `CheckedIn` first)
  and write to a new append-only `AppointmentStatusHistory` table
  (`GET /{id}/history`).
- **Recurring appointments** — `AppointmentSeries` (same weekday, every N
  weeks, fixed occurrence count — not a general rrule engine) +
  `POST /appointments/series`. All-or-nothing: every occurrence is conflict-
  checked before any of them are created, so a series either books cleanly
  or the caller gets back exactly which dates collided. "Edit one" is the
  existing per-appointment actions; "edit series"
  (`PATCH /series/{id}/cancel`) cancels every still-future, still-open
  occurrence, leaving past/completed ones untouched.
- **Drag-and-drop rescheduling** — `PATCH /{id}/reschedule`, the server-side
  half: re-runs the exact same conflict check as create, minus the
  appointment being moved.
- **Reminder service interface** — `IReminderService` +
  `NoOpReminderService` (logs what it would do). Called from
  create/reschedule/cancel/no-show. A real SMS/email provider becomes a new
  registered implementation; nothing else changes.
- **Timezone/DST correctness** — `Appointment.StartsAt`/`EndsAt` are
  `DateTimeOffset` (already absolute-time, not naive wall-clock), and
  `Location.Timezone` is exposed via `LocationsController` for the
  frontend's own conversion/display. Covered by tests proving conflict
  detection is correct across differing UTC offsets and across a daylight-
  saving transition.
- **Every appointment belongs to one organization, patient, provider, and
  location**: organization and patient always did (via `PatientId` ->
  `Patient.OrganizationId`); `ProviderId`/`LocationDetailId` remain
  nullable (a home-visit or telehealth appointment may have neither) —
  loosening that to a hard requirement would be a real, separate schema
  decision, not something to fold into this phase silently.

## Clinical documentation engine

Backend for "Phase 5A"; the note-taking UI itself (rendering a template's
schema into an actual form, a printable/PDF-ready layout) is the natural
next phase, same sequencing as the last four. The Draft -> Signed lifecycle,
PTA cosign workflow, per-note interventions, structured functional goals,
and outcome measures already existed and were already thoroughly tested
(`ClinicalNoteServiceTests`: signed-note immutability, addendum-only-after-
signing, cross-org isolation). Built the rest:

- **Configurable template engine** (`ClinicalTemplatesController`,
  `/api/v1/clinical-templates`) — didn't exist at all before this. A
  `ClinicalNoteTemplate` is a JSON schema of sections/fields per `NoteType`,
  with real versioning: creating a new one for the same scope deactivates
  the previous version rather than overwriting it, so the full history
  stays queryable. Resolution is most-specific-wins: Location -> State ->
  Organization -> Platform default (`GET .../resolve`). The backend
  validates SchemaJson is well-formed JSON and owns resolution/versioning;
  it doesn't interpret section contents -- see `ClinicalNoteTemplate`'s own
  doc comment for the documented (not enforced field-by-field) schema shape
  naming the reusable components this phase asks for (pain scale, ROM/MMT
  tables, goals, outcome measures, functional limitations, ICD-10/CPT
  pickers) -- each of which already has a real structured backend
  counterpart (`PatientDiagnosis`, `NoteIntervention`/`ServicePrice`,
  `OutcomeScore`, `FunctionalGoal`) rather than the template being the only
  place that data lives.
- **`NoteStatus.Locked`** — a further, manual step past `Signed`
  (Admin/Director-only, via `POST /notes/{id}/lock`). `Signed` already
  blocked direct edits and still allowed an addendum; `Locked` additionally
  blocks new addenda too. `EnforceSignedNoteImmutability` (the DB-level
  guard) now recognizes exactly one legitimate write to an already-signed
  row — the Signed -> Locked transition itself, touching only `Status`/
  `UpdatedAt` — and still rejects everything else.
- **E-signature detail**: `SignatureCredentials` (the signer's own
  Credential field, snapshotted — never re-read live later),
  `SignatureIpAddress`, and `SignatureHash` (a SHA-256 digest over the
  note's clinical content, computed the moment every other signature field
  is set) were all missing before; only `SignatureName`/`SignedAt` existed.
  The hash is a best-effort integrity check alongside the DB-level
  immutability enforcement, not a cryptographic signature meant to resist a
  determined attacker with DB access.
- **Immutable version history** (`ClinicalNoteVersion`,
  `GET /notes/{id}/versions`) — an append-only snapshot written on every
  draft save (autosave is just calling the existing `PUT` as often as the
  frontend likes; each call gets its own version row) and once more at
  signing (`IsSignedVersion = true`, the final snapshot). This is what
  "immutable version history" means here, distinct from
  `EnforceSignedNoteImmutability`: not a second enforcement of "can't edit
  a signed note," but a real, queryable record of what the note looked like
  at each save, including every draft revision before it was ever signed.
- **Goals gained a Term** (`GoalTerm.ShortTerm`/`LongTerm`) — the short/
  long-term distinction this phase asks for didn't exist as a field before.

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
seeds demo data on first boot), then `PhysioTrac.Web` and the React
`frontend/` SPA once the API reports healthy. Once it's up:

- Web UI (Blazor): http://localhost:5073
- React SPA: http://localhost:5173
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

Also seeded per organization: 4 fictional patients each (Source Motion:
Taylor Brooks, Riley Simmons, Harper Ellison, Quinn Alvarez; Total Motion:
Jordan Ellis, Reese Whitfield) with realistic allergies, medications,
structured ICD-10 diagnoses, a primary care provider, and insurance
(including one patient with primary + secondary policies with different
subscribers, to exercise that specifically); a small ~18-row shared ICD-10
catalog (`DiagnosisCode`, org-independent reference data); appointment
types; service prices; and a payer per organization — enough that every
page in the app has real data to show immediately.

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
