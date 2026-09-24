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

## PT note types

Backend for "Phase 5B" — default templates and supporting logic for the nine
PT note types on top of the Phase 5A engine; the per-type note-taking screens
themselves stay deferred to the same future frontend phase as the engine
itself. The engine (template resolution/versioning, Draft -> Signed -> Locked,
e-signature, version history) is unchanged; this phase is templates, three
new `ClinicalNote` fields, and two new read-only aggregation endpoints.

- **Three new `NoteType` values** — `PlanOfCare`, `DryNeedlingTreatment`,
  `PelvicHealthEvaluation` — joining the six that already existed
  (Evaluation, Daily, SOAP, Progress, Re-evaluation, Discharge), covering all
  nine of the types this phase asks for. Dry Needling *Consent* deliberately
  isn't a `NoteType` — it's already `ConsentType.DryNeedlingConsent` in the
  existing consent subsystem (a signed attestation, not a clinical note);
  only the treatment note itself is new here. Widened the `NoteType` column
  from `nvarchar(20)` to `nvarchar(30)` after noticing
  `"PelvicHealthEvaluation"` (22 characters) would have been silently
  truncated otherwise.
- **Nine default Platform-scope templates** (`ClinicalNoteTemplate`, one per
  `NoteType` this phase asks for), seeded so `ClinicalTemplateService
  .ResolveAsync`'s most-specific-wins search always finds at least this
  fallback for every tenant, before any org customizes anything more
  specific.
- **Plan-of-care certification** (`PlanOfCareCertifiedDate` /
  `PlanOfCareCertifyingProviderId` on `ClinicalNote`,
  `POST /notes/{id}/certify-poc`) — the Medicare-style physician
  certification of a plan of care, deliberately modeled as separate from the
  therapist's own `SignedAt`/`SignatureName`: in practice certification often
  happens days later, by an outside physician, not the treating therapist.
  `EnforceSignedNoteImmutability` (the DB-level guard from Phase 5A) gained a
  second, narrowly-scoped exception for this — only these two fields plus
  `UpdatedAt` may change on an already-Signed/Locked note, never the clinical
  narrative itself.
- **Pull-forward** (`GET /notes/patient/{id}/pull-forward`) — a pure read
  returning what a new note for a patient should pre-populate: active
  `FunctionalGoal`s, the most recently *signed* note's
  `ObjectiveMeasurementsJson`, and unresolved `PatientDiagnosis` rows. The
  note-creation UI (not built in this pass) decides what to actually copy
  into the new note's fields; the backend just answers "what's current."
- **Progress-note-due** (`Organization.ProgressNoteDueDays` /
  `ProgressNoteDueVisitCount`, `GET /notes/patient/{id}/progress-note-status`)
  — two independently configurable triggers, since "day count" and "visit
  count" aren't the same kind of value: day-count auto-computes
  `ReassessmentDue` as a calendar date the moment an Evaluation/Progress/
  Re-evaluation note is created (`ComputeAutoReassessmentDue`), so it can be
  compared against today; visit-count instead counts signed Daily/SOAP/
  home-visit notes since the last progress-triggering note, computed live on
  every call since "N visits since X" isn't a fixed date that can be
  precomputed. Both can be configured at once; either or neither firing is
  reported independently in the response.
- **Linking notes to appointments** — `ClinicalNote.AppointmentId` already
  existed; this phase's seed data is the first to actually populate it for
  both organizations (a completed evaluation and a still-scheduled one),
  since nothing previously exercised that link with real data.
- Found and fixed a real bug while building this: `GetPullForwardDataAsync`
  and `GetProgressNoteStatusAsync` initially filtered on `ClinicalNote
  .IsSigned` (a computed C# property) directly inside an EF Core query —
  that doesn't translate to SQL on any provider (caught by the in-memory
  test suite, not by inspection) and was changed to compare `Status`
  directly against `NoteStatus.Signed`/`Locked`.

## Digital intake, consents, and patient portal

Backend for "Phase 6." A gap analysis before writing any code found this
codebase's patient-portal surface was already far more built out than the
spec's phrasing suggested — appointments (`PortalBookingController`), online
payments (`PatientPaymentsController`), and read access to home exercise
programs, documents, consents, superbills, payment records, and billing
statements were *already* patient-reachable, because every one of those
list/read methods was already gated only by `ITenantAccessService
.RequirePatientAccessAsync` (no staff-only role check), and that method's
`PatientsFor` already resolves a Patient-role caller to their own chart via
`PortalUserId` — first and unconditionally, before any other role logic. A
patient calling with another patient's id — even in the same organization —
was already rejected. So "invoices" needed no new Invoice entity at all
(`PatientStatement` already *is* that, patient-readable already); it needed
tests proving it, not new code. What genuinely didn't exist:

- **Consent templates** (`ConsentTemplate`, `/api/v1/consent-templates`) —
  a real, versioned, scoped config surface for consent language, structurally
  identical to `ClinicalNoteTemplate`/`ClinicalTemplateService` (Platform ->
  Organization -> State -> Location, most-specific-wins, new-version-
  deactivates-old). `ConsentService.RecordAsync`/`RecordOwnAsync` now resolve
  the org's current template and snapshot both its text *and* version onto
  the `Consent` row (`Consent.TemplateVersion`, new); `ConsentTypeText`'s
  static strings remain only as the fallback for a type/org with no template
  configured yet (seeded as the Platform default for all five types, so a
  fresh install resolves a real versioned template immediately).
- **Patient self-service consent signing** (`POST /api/v1/portal/consents`,
  `ConsentService.RecordOwnAsync`) — `RecordAsync` requires
  `RoleSets.DocumentManagement`, so a patient could never sign their own
  consent before this; the new method resolves "which patient" via
  `RequirePortalPatientAsync` instead, with no staff role requirement and no
  client-supplied patient id anywhere in its request shape.
- **Digital intake forms** (`IntakeFormTemplate`/`IntakeFormSubmission`,
  `/api/v1/intake-form-templates`, `/api/v1/portal/intake-form-submissions`)
  — fully new. The template half mirrors the clinical/consent template
  engines exactly (versioned, scoped, most-specific-wins), keyed by a string
  `Key` rather than a fixed enum since an organization can define any number
  of differently-purposed forms. The submission half is patient-portal-only,
  snapshotting the exact template version a patient answered.
- **Secure document sharing with expiring access** (`DocumentShareLink`,
  `POST .../documents/{id}/share-links`, `GET /api/v1/shared-documents/{token}`)
  — for handing a document to someone with no portal login at all (a
  referring physician, e.g.). Only the token's SHA-256 hash is ever
  persisted (reusing `InvitationTokenGenerator`, the same scheme staff
  invitations already use) — a database read alone can never produce a
  working link. `SharedDocumentsController` deliberately carries no
  `[Authorize]`; the token, checked against `DocumentShareLink.IsUsable`
  (not revoked, not expired), is the entire access control, and an unknown/
  expired/revoked token all produce the identical 404 so a prober can't
  distinguish them.
- **`GET /api/v1/portal/me`** — the one missing piece for everything above:
  nothing previously told a logged-in patient their own linked patient id,
  which every existing patient-scoped route needs in its URL. Not a new
  login flow (see below) — just the portal's bootstrap call.
- **`HomeExerciseItem.MediaUrl`** — a link to a demonstration image/video;
  actual media upload/hosting stays a frontend/CDN concern.

**On "a separate login flow":** there isn't a technically distinct portal
login endpoint, by design — Patient accounts sign in through the exact same
cookie+CSRF `POST /api/v1/auth/login` as every other role, matching this
session's standing architecture decisions (no JWT, no second auth stack).
The separation is enforced entirely by authorization (`RequirePatientAccessAsync`
/`RequirePortalPatientAsync`), which is what a "patient can only reach their
own records" requirement actually needs; the frontend's portal being a
visually/behaviorally distinct experience from the staff app is a screens
question, deferred like every other screen this session.

Tests added: `ConsentTemplateServiceTests`, `IntakeFormServiceTests`,
new `ConsentServiceTests`/`DocumentServiceTests` cases for self-signing and
share links, and a new `PatientPortalIsolationTests` suite that is Phase 6's
explicit ask made concrete — for home exercise programs, documents,
consents, and intake form submissions, one test per resource proving a
patient portal account cannot reach another patient's records even within
the *same* organization (the harder case than cross-org, since
`OrganizationId` alone can't distinguish them there).

Deferred, same as every prior phase: the actual React/Blazor portal screens
(intake-form renderer, consent-signing UI, document/HEP/statement views) and
a real form-builder UI for staff to author `IntakeFormTemplate`/
`ConsentTemplate` `SchemaJson`/`BodyText` — both currently authored via the
API directly.

## Billing and revenue cycle

Backend for "Phase 7." Another gap analysis before writing code, and another
large surprise: this codebase already had a strikingly mature billing/
revenue-cycle model committed well before this session's numbered phases --
`Charge`, `Claim` (with CMS-1500-shaped `DiagnosisCodeListJson`/
`DiagnosisPointersFor` box 21/24E logic and a full `ClaimStatus` workflow:
Draft → Ready → Submitted → Accepted/Rejected → Processing →
Paid/Denied/Appealed → Closed), `ClaimTransaction` (payments, adjustments,
write-offs, refunds, balance transfers between claims), `ClaimDenial` (a
denial work queue with appeal status and overdue tracking), `Superbill` +
`PaymentRecord` (the cash-pay path), `PatientPayment` (patient-initiated
online payments), and `PatientStatement` (a generated balance snapshot) --
plus an existing Medicare 8-minute-rule calculator and CPT/modifier capture
validation on `Charge`. All of it already patient-portal-safe: every list/
read method on these was already gated only by `RequirePatientAccessAsync`,
no staff-only role check, so "patient balances" and "payments" needed no new
access-control work, only confirming it (see the new tests below).

What genuinely didn't exist, and is what this phase builds:

- **CPT code reference catalog** (`CptCode`, `/api/v1/cpt-codes`) -- the
  CPT-side twin of the existing ICD-10 `DiagnosisCode` catalog, same
  treatment (shared, read-only, seed-maintained). CPT codes existed
  everywhere only as unvalidated strings before this.
- **Organization/location fee schedules** -- `ServicePrice` gained a
  nullable `LocationId`: a location-specific row overrides the
  organization-wide default for the same CPT code. This needed two
  *separate* filtered unique indexes, not one combined index, after
  discovering that EF Core's SQL Server provider auto-filters a unique
  index on any nullable column (`WHERE LocationId IS NOT NULL`) -- a single
  combined index would have silently stopped enforcing "at most one
  org-wide default per CPT code," letting duplicates back in. Caught by
  reading the generated migration before applying it, not by a test.
- **Payer fee schedules** (`PayerFeeScheduleItem`,
  `/api/v1/payers/{id}/fee-schedule`) -- a payer's contracted/allowed
  amount per CPT code, informational/reporting data alongside (not
  enforced against) what the clinic actually bills.
- **Configurable 8-minute rule** (`Organization.EightMinuteRuleVariant`,
  `EightMinuteRuleCalculator`'s new variant-aware overload) -- the existing
  calculator was hard-coded to the Medicare table; added a second,
  explicitly-labeled-as-a-simplification `RoundedFifteenMinute` variant
  for organizations that use a simpler quarter-hour rounding scheme, kept
  the original single-argument overload so every existing caller is
  unaffected.
- **Charges generated from completed appointments and signed notes**
  (`IChargeService.GenerateFromNoteAsync`/`GenerateFromAppointmentAsync`) --
  previously `Charge` creation was entirely manual. The note-based path
  groups a signed note's `NoteIntervention` items by their (deliberately
  clinical-only) `InterventionCategory`, resolves each group's CPT code via
  a new org-configurable `CptCodeMapping`, sums each code's own timed
  minutes independently through the configured 8-minute-rule variant (not
  implementing cross-code remainder pooling -- a documented, further
  wrinkle real multi-code 8-minute-rule billing sometimes applies), and
  prices from the fee schedule. The appointment-based path is the simpler
  cash-pay alternative: one flat charge from `AppointmentType
  .DefaultCptCode`, a new field -- discovered mid-verification that it
  had no way to actually be set through the API at all (`AppointmentTypesController`
  only ever exposed `Create`, never an update), so added
  `PATCH /appointment-types/{id}/billing` to close that gap before calling
  the feature done. Both paths refuse to duplicate charges for the same
  note/appointment.
- **Aging report** (0-30/31-60/61-90/90+, `GET /billing-reports/aging`) and
  **revenue reports by provider, location, or service**
  (`GET /billing-reports/revenue`) -- both fully new, computed live from
  the existing `Charge`/`Claim`/`ClaimTransaction`/`Superbill`/
  `PaymentRecord` ledger, no new persisted state. Aging buckets by days
  since the earliest date of service on a claim's/superbill's own charges
  (not submission date, which can be null for a still-Draft claim).
  Revenue draws a deliberate accrual-vs-cash split: billed amount is
  grouped by the requested dimension and filtered to the date range;
  collected amount is total cash actually received in that range,
  independent of which visit's charge it happens to pay down.
- **Patient balance** (`GET /billing-reports/patient-balance/{id}`) and
  **statement line items** (`GET /patient-statements/{id}/line-items`) --
  the latter is the exact "rebuilt live... not ported yet" gap
  `PatientStatement`'s own pre-existing doc comment flagged, now filled in:
  reconstructs a previously generated statement's charges and payment/
  adjustment activity dated on or before its own `StatementDate`, so a
  statement stays reproducible after later activity accrues.
- **`PaymentPlan`** added to `ClaimTransactionMethod` (reused for
  `PaymentRecord.Method`, new, rather than a duplicate enum) -- completing
  the phase's cash/card/check/insurance/payment-plan method list. No
  payment processing of any kind is implemented, per the phase's explicit
  "recorded only" instruction -- exactly like the pre-existing
  `PatientPayment`/`PaymentRecord` already worked.

Tests added: `EightMinuteRuleCalculatorTests` (both variants against known
minute/unit tables), new `ChargeServiceTests` cases for both generation
paths (mapped/unmapped categories, minute summing, location-price
precedence, untimed-intervention unit counting, duplicate-generation
guards), and a new `BillingReportServiceTests` covering aging bucketing,
patient balance aggregation, revenue filtering/grouping, and statement
line-item reconstruction (including that a refund correctly *increases* the
shown balance while payments/adjustments reduce it -- caught and fixed a
real sign-flip bug in that logic before it shipped, via a test that
constructed the exact refund scenario).

Verified: dotnet build clean, full xUnit suite green (311/311, 39 new),
migration applied to local SQL Server with no cascade-path conflicts, and
live-verified end-to-end against the running API + real SQL Server: fee
schedule resolution (org-wide vs. location override), charge generation
from a two-intervention signed note (25 summed minutes -> 2 units -> priced
at the location override), charge generation from a completed appointment
(flat rate from `AppointmentType.Price`), duplicate-generation rejected on
both paths, the aging/revenue/patient-balance reports, and CPT code search
-- then cleaned up all test data.

Deferred: real payer-specific claim edits/837P file generation (the data
model is aligned for it; an actual clearinghouse integration is explicitly
out of scope per the phase spec), and the billing screens themselves
(charge review queue, claims worklist, aging dashboard) -- same as every
prior phase's frontend deferral.

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

Also seeded: nine Platform-scope `ClinicalNoteTemplate` rows, one per PT note
type from the "PT note types" phase above; a handful of sample
`ClinicalNote`s per organization spanning those types (Source Motion:
signed Evaluation + Plan of Care — the latter with its plan-of-care
certification already recorded — + Daily + Dry Needling Treatment notes, a
Discharge Summary, and one still-Draft Pelvic Health evaluation; Total
Motion: the existing Draft evaluation now linked to its appointment, plus
signed Progress, Daily x2, Re-evaluation, and SOAP notes), with
`FunctionalGoal`s and a `Consent` (dry-needling) attached where relevant.
Source Motion's org configures the day-count progress-note-due trigger
(`ProgressNoteDueDays = 30`) and Total Motion the visit-count trigger
(`ProgressNoteDueVisitCount = 2`), so both are demonstrated somewhere in the
seed data. These sample notes are constructed already at `Signed` directly
against the DbContext rather than through `SignNoteAsync` — there's no
version-history snapshot or a "real" computed `SignatureHash` for seed data,
the same shortcut this seeder already takes elsewhere (see its own doc
comment) for bypassing the real provisioning flow.

Also seeded: one Platform-scope `ConsentTemplate` per `ConsentType` (so
`ConsentService` always resolves a real, versioned template rather than
falling back to the static `ConsentTypeText` constant, even on a freshly
created database) and one starter Platform-scope `IntakeFormTemplate`
(`new-patient-intake`) every organization can build on.

Also seeded: a shared `CptCode` reference catalog (mirroring the ICD-10
catalog above); per-organization `CptCodeMapping`s (therapeutic exercise →
97110, manual therapy → 97140, and more for Source Motion) so
`GenerateFromNoteAsync` has something to resolve immediately; `ServicePrice`
fee-schedule rows for those same codes, including one location-specific
override at Source Motion's Fuquay-Varina clinic (97110 priced higher there
than the organization-wide default) to demonstrate the override actually
taking precedence; and `AppointmentType.DefaultCptCode` set on both orgs'
Initial Evaluation types, so `GenerateFromAppointmentAsync` has a default to
bill against too.

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
