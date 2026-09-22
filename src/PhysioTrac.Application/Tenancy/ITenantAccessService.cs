using PhysioTrac.Application.Auth;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Application.Tenancy;

/// <summary>Canonical tenant + RBAC gate. Every service/controller must route
/// through this rather than trusting a client-supplied organization or
/// patient id — mirrors the original Django `care/access.py` module, which
/// is the single choke point for org-suspension/archival checks, row-level
/// patient scoping, and role-based caseload narrowing.
///
/// Known, documented, intentional gap carried over from the source system:
/// this is application-layer scoping only — no SQL Server row-level security
/// yet. That is required before real PHI goes into production.</summary>
public interface ITenantAccessService
{
    /// <summary>Resolves the caller's active, non-archived, non-suspended
    /// organization. Throws <see cref="Common.ForbiddenException"/> for a
    /// platform super admin (they have no standing org), an unassigned user,
    /// or an archived/suspended organization.</summary>
    Task<Organization> OrganizationRequiredAsync(ICurrentUser user, CancellationToken ct = default);

    /// <summary>Patients visible to this caller, scoped by organization and
    /// role. A PATIENT-role caller always gets only their own linked chart,
    /// unconditionally, regardless of <paramref name="clinical"/>. For every
    /// other role, <paramref name="clinical"/> toggles "just my assigned
    /// caseload" (Therapist/Assistant) vs. "the whole org"
    /// (Admin/Director/Compliance); Scheduler/Biller get none when
    /// clinical=true.</summary>
    IQueryable<Patient> PatientsFor(ICurrentUser user, bool clinical = true);

    /// <summary>Authorizes a single chart lookup and audits denied attempts
    /// (no PHI in the audit metadata — action/route/ids only). Throws
    /// <see cref="Common.ForbiddenException"/> if not permitted.</summary>
    Task<Patient> RequirePatientAccessAsync(
        ICurrentUser user,
        Guid patientId,
        bool clinical = true,
        string? route = null,
        string? ipAddress = null,
        CancellationToken ct = default);

    /// <summary>Throws <see cref="Common.ForbiddenException"/> unless the
    /// caller's role is in <paramref name="roles"/>.</summary>
    void RequireRole(ICurrentUser user, IReadOnlySet<Domain.Enums.UserRole> roles);

    /// <summary>Throws <see cref="Common.ForbiddenException"/> unless the
    /// caller is the canonical, organization-free platform account.</summary>
    void RequirePlatformSuperAdmin(ICurrentUser user);

    /// <summary>The ONLY way a patient-portal endpoint resolves "which
    /// patient" — the authenticated caller's own linked chart, never a
    /// patient id read from a URL, body, or query parameter. This is the
    /// primary IDOR defense for the entire portal API surface. Throws
    /// <see cref="Common.ForbiddenException"/> if the caller isn't a
    /// Patient-role account or has no chart linked yet.</summary>
    Task<Patient> RequirePortalPatientAsync(ICurrentUser user, CancellationToken ct = default);
}
