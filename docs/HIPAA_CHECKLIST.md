# HIPAA readiness checklist

## This application is not HIPAA compliant, and no software is

**PhysioTrac is a HIPAA-oriented application foundation, not a
certified-compliant product.** HIPAA compliance is a property of an
*organization's* administrative, physical, and technical safeguards taken
together -- policies, staff training, signed agreements, risk analysis,
and incident response, in addition to the software. No codebase, on its
own, can be "HIPAA compliant." This document tracks which technical
safeguards this codebase implements today, and lists everything else a
deploying organization still has to do before real patient data should
touch it. Treat every ✅ below as "the software helps here," not "this box
is legally checked."

## Technical safeguards (45 CFR § 164.312)

| Safeguard | Status | Notes |
|---|---|---|
| Access control -- unique user identification | ✅ | Every user authenticates as themselves; no shared logins in the app model. |
| Access control -- role-based authorization | ✅ | `RoleSets`/`RequireRole`, enforced at both the imperative service layer and declarative `[Authorize(Policy=...)]` layer -- see [RoleSetAuthorizationHandlerTests.cs](../tests/PhysioTrac.Tests/RoleSetAuthorizationHandlerTests.cs) for the exhaustive role x policy matrix. |
| Access control -- tenant isolation | ✅ | `ITenantAccessService` is the sole chokepoint for cross-organization data access; see the tenant-isolation test suite (`TenantIsolationGapTests.cs` and per-service cross-org tests throughout `tests/`). |
| Access control -- automatic logoff | ✅ | `SecurityOptions.IdleTimeoutMinutes` / `AbsoluteSessionHours`, plus per-role concurrent-session limits. |
| Access control -- account lockout | ✅ | `SecurityOptions.FailedLoginLockoutThreshold` / `LockoutDurationMinutes`. |
| Access control -- emergency access procedure | ❌ | Not implemented. Decide and document how a clinician gets emergency chart access if their normal account is locked out or unavailable, and whether that path needs its own audited "break-glass" mechanism. |
| Multi-factor authentication | ❌ | Not implemented. `ApplicationUser` inherits ASP.NET Core Identity's `TwoFactorEnabled` column, but no 2FA challenge is wired into the login flow. Strongly recommended, especially for Admin/Director/SuperAdmin roles, before production use. |
| Audit controls | ✅ | `AuditEvent`/`IAuditService`, auto-captured for any entity with a direct `OrganizationId` via `EntityChangeAuditInterceptor`, with explicit calls for the entities that don't have one -- see `AuditLogCompletenessTests.cs`. Access-denied events are also audited (`TenantAccessService`). |
| Audit log retention/archival policy | ❌ | Audit events are written but nothing here defines a retention period, archival, or tamper-evidence (e.g. write-once storage) policy. Decide a retention period and implement it. |
| Integrity controls | ⚠️ | EF Core + SQL Server transactions protect record-level integrity; there's no separate checksum/hash-chaining on audit records themselves. |
| Transmission security (encryption in transit) | ⚠️ | The app itself doesn't terminate TLS -- `app.UseHsts()` is enabled outside Development, and it's on you to put a real TLS-terminating reverse proxy/load balancer in front in any real deployment (see [DEPLOYMENT_AZURE.md](DEPLOYMENT_AZURE.md) / [DEPLOYMENT_AWS.md](DEPLOYMENT_AWS.md)). The local dev/Docker Compose setup runs plain HTTP and must never be exposed to the internet as-is. |
| Encryption at rest | ⚠️ | Not the application's responsibility to implement directly -- depends on your database deployment (Azure SQL Database's Transparent Data Encryption, RDS storage encryption, or SQL Server's own TDE if self-hosting) being turned on. Verify it explicitly; it is not always the default. |
| Security headers | ✅ | CSP, X-Content-Type-Options, X-Frame-Options, Referrer-Policy, Permissions-Policy (`SecurityHeadersMiddleware`) plus strict, allowlist-based CORS (`Program.cs`). |
| Rate limiting | ✅ | Per-IP fixed-window limits on auth and patient-portal endpoints, plus a global limiter -- `RateLimitPolicies`/`Program.cs`. |
| Input validation | ✅ | FluentValidation on money/numeric/free-text mutation DTOs across the API, enforced via `FluentValidationActionFilter` before a request reaches a service. |
| Secure error handling | ✅ | `ExceptionHandlingMiddleware` returns generic client-facing messages; stack traces/PHI are never serialized into an API response. Verify this stays true for whatever centralized logging/error-tracking service you add -- PHI must never reach a third-party log aggregator that hasn't signed a BAA. |
| Dependency/secret scanning | ✅ | CI (`.github/workflows/ci.yml`) runs a NuGet/npm vulnerable-dependency check and a secret scan (gitleaks) on every push/PR. |

## Administrative safeguards (45 CFR § 164.308)

None of these are things software can provide -- they're organizational
processes and documents this repository does not and cannot contain:

- [ ] **Risk analysis.** A documented, periodic assessment of risks and
      vulnerabilities to PHI confidentiality/integrity/availability across
      your actual deployment (not just this codebase).
- [ ] **Risk management plan.** Documented remediation for whatever the
      risk analysis finds.
- [ ] **Sanction policy.** Documented consequences for staff who violate
      your security policies.
- [ ] **Information system activity review.** A defined process (who,
      how often) for actually reviewing the audit logs this app produces
      -- logging events is not the same as anyone looking at them.
- [ ] **Workforce security / access authorization procedures.** Who
      decides which role a new hire gets, and how access is revoked
      on termination -- this app enforces roles, it doesn't decide who
      gets which one.
- [ ] **Security awareness and training program** for all staff with
      access, including periodic reminders/updates.
- [ ] **Incident response plan.** Who does what when something looks
      wrong, before you need it.
- [ ] **Breach notification procedure.** A plan mapped to the HIPAA
      Breach Notification Rule's timelines (individuals, HHS, and media
      for large breaches), decided before an incident, not during one.
- [ ] **Contingency plan** covering data backup (see
      [BACKUP_RESTORE.md](BACKUP_RESTORE.md)), disaster recovery, and
      emergency-mode operation.
- [ ] **Business Associate Agreements (BAAs) with every vendor** that
      will touch PHI -- your cloud provider, your database host if
      different, your error-tracking/logging/APM service if any, your
      email/SMS provider if you add patient notifications, your backup
      storage provider, and any analytics tool. A vendor being
      "HIPAA-eligible" (offering a BAA) does not mean you have signed one
      with them, and does not cover services outside the BAA's own
      service list.
- [ ] **Periodic vendor/business-associate review.** Confirming your
      vendors still meet your requirements and still have a current BAA
      in place, on a recurring schedule, not just at signup.

## Physical safeguards (45 CFR § 164.310)

Also organizational, not software:

- [ ] **Facility access controls** for wherever servers/workstations
      physically live (a real concern if self-hosting; largely delegated
      to the cloud provider's own SOC 2/HIPAA attestations if you deploy
      to Azure/AWS per the deployment guides -- confirm this delegation
      explicitly, don't assume it).
- [ ] **Workstation security policy** for any device staff use to access
      the app (screen lock timeouts, disk encryption, no PHI in
      screenshots/local exports without controls).
- [ ] **Device and media controls** -- policy for decommissioning any
      hardware (laptops, backup media, failed drives) that ever held PHI.

## Remaining business and legal requirements

Before any real patient data goes into a production deployment of this
app, at minimum:

1. **Signed BAAs with every vendor** in your deployment path (cloud
   host, database if separate, logging/monitoring, backups, email/SMS,
   anything else that can touch PHI).
2. **A completed risk analysis** of your actual deployment.
3. **A written incident response and breach notification plan**, ready
   before you need it.
4. **Written policies** covering the administrative and physical
   safeguards above -- workforce access, sanctions, workstation security,
   device/media handling, contingency planning.
5. **Staff training** on those policies, before they get access, with
   periodic refreshers.
6. **Ongoing vendor review** -- BAAs and vendor security postures reverified
   on a recurring schedule, not treated as a one-time signup step.

None of the above is optional, and none of it is something this codebase
-- or any codebase -- can satisfy for you.
