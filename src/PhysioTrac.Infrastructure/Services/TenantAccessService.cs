using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Infrastructure.Services;

/// <summary>Direct C# port of `care/access.py`'s tenant/RBAC gate. See the
/// interface doc comment for the invariants this must preserve.</summary>
public class TenantAccessService : ITenantAccessService
{
    private readonly PhysioTracDbContext _db;
    private readonly IAuditService _audit;

    public TenantAccessService(PhysioTracDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<Organization> OrganizationRequiredAsync(ICurrentUser user, CancellationToken ct = default)
    {
        if (user.IsPlatformSuperAdmin)
        {
            throw new ForbiddenException("Platform administrators must use the super-admin workspace.");
        }
        if (user.OrganizationId is null)
        {
            throw new ForbiddenException(
                "This account is not assigned to an organization. Ask an administrator to assign it.");
        }

        var organization = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == user.OrganizationId, ct)
            ?? throw new ForbiddenException("This account's organization no longer exists.");

        if (organization.ArchivedAt is not null)
        {
            throw new ForbiddenException(
                "Your organization account has been archived. Please contact your administrator.");
        }
        if (!organization.IsActive || organization.Status == OrganizationStatus.Suspended)
        {
            throw new ForbiddenException(
                "Your organization account is currently suspended. Please contact your administrator.");
        }

        return organization;
    }

    public IQueryable<Patient> PatientsFor(ICurrentUser user, bool clinical = true)
    {
        // organization_required() runs synchronously here since callers of
        // PatientsFor need an IQueryable back, not a Task<IQueryable>; any
        // ForbiddenException it raises still propagates to the caller.
        var organizationId = OrganizationRequiredSync(user);
        var query = _db.Patients.Where(p => p.OrganizationId == organizationId && p.DeletedAt == null);

        // The PATIENT-role check is deliberately first and unconditional —
        // before `clinical` is even consulted. Every other role's `clinical`
        // flag toggles "just my assigned patients" vs. "the whole org", but a
        // portal (Patient-role) account must NEVER receive the whole-org
        // queryset under any calling convention.
        if (user.Role == UserRole.Patient)
        {
            return query.Where(p => p.PortalUserId == user.UserId);
        }

        if (!clinical)
        {
            return query;
        }

        // Admin/Director/Compliance/Scheduler/Biller are all administrative-
        // facing roles whose job (oversight, front-desk registration and
        // scheduling, billing) inherently requires seeing the whole roster,
        // not a caseload -- "clinical" narrowing only means something for a
        // role that actually has a caseload to narrow to. Discovered as a
        // real bug: this used to fall through to `Where(_ => false)` for
        // Scheduler/Biller, silently returning zero patients from every
        // patient-scoped read (GET /api/v1/patients, document/consent/
        // message listing) despite registration/scheduling/billing being
        // those roles' entire job per the spec.
        if (user.Role is UserRole.Admin or UserRole.Director or UserRole.Compliance or UserRole.Scheduler or UserRole.Biller)
        {
            return query;
        }

        if (user.Role is UserRole.Therapist or UserRole.Assistant)
        {
            return query.Where(p => p.AssignedTherapistId == user.UserId);
        }

        return query.Where(_ => false);
    }

    public async Task<Patient> RequirePatientAccessAsync(
        ICurrentUser user,
        Guid patientId,
        bool clinical = true,
        string? route = null,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        Patient? patient;
        bool allowed;
        try
        {
            patient = await PatientsFor(user, clinical).FirstOrDefaultAsync(p => p.Id == patientId, ct);
            allowed = patient is not null;
        }
        catch (ForbiddenException)
        {
            allowed = false;
            patient = null;
        }

        if (!allowed)
        {
            if (user.IsAuthenticated && user.OrganizationId is not null)
            {
                await _audit.RecordAuditEventAsync(
                    actorId: user.UserId,
                    action: "access.denied",
                    objectType: nameof(Patient),
                    objectId: patientId,
                    organizationId: user.OrganizationId.Value,
                    patientId: patientId,
                    ipAddress: ipAddress,
                    metadata: new { route },
                    ct: ct);
            }
            throw new ForbiddenException("You are not permitted to access this patient record.");
        }

        return patient!;
    }

    public void RequireRole(ICurrentUser user, IReadOnlySet<UserRole> roles)
    {
        if (!roles.Contains(user.Role))
        {
            throw new ForbiddenException("Your role is not permitted to perform this action.");
        }
    }

    public void RequirePlatformSuperAdmin(ICurrentUser user)
    {
        if (!user.IsPlatformSuperAdmin)
        {
            throw new ForbiddenException("Only platform super administrators can manage clients.");
        }
    }

    public async Task<Patient> RequirePortalPatientAsync(ICurrentUser user, CancellationToken ct = default)
    {
        if (!user.IsAuthenticated || user.Role != UserRole.Patient)
        {
            throw new ForbiddenException("This endpoint is only available to patient portal accounts.");
        }
        var organization = await OrganizationRequiredAsync(user, ct);
        var patient = await _db.Patients.FirstOrDefaultAsync(
            p => p.OrganizationId == organization.Id && p.PortalUserId == user.UserId, ct);
        if (patient is null)
        {
            throw new ForbiddenException("No patient chart is linked to this portal account. Contact your clinic.");
        }
        return patient;
    }

    /// <summary>Synchronous twin of <see cref="OrganizationRequiredAsync"/>,
    /// needed because <see cref="PatientsFor"/> must return a plain
    /// <c>IQueryable</c> rather than a <c>Task</c>. Must apply the exact same
    /// archived/suspended checks — do not let this drift into a shortcut
    /// that only checks <c>OrganizationId is null</c>.</summary>
    private Guid OrganizationRequiredSync(ICurrentUser user)
    {
        if (user.IsPlatformSuperAdmin)
        {
            throw new ForbiddenException("Platform administrators must use the super-admin workspace.");
        }
        if (user.OrganizationId is null)
        {
            throw new ForbiddenException(
                "This account is not assigned to an organization. Ask an administrator to assign it.");
        }

        var organization = _db.Organizations.FirstOrDefault(o => o.Id == user.OrganizationId)
            ?? throw new ForbiddenException("This account's organization no longer exists.");

        if (organization.ArchivedAt is not null)
        {
            throw new ForbiddenException(
                "Your organization account has been archived. Please contact your administrator.");
        }
        if (!organization.IsActive || organization.Status == OrganizationStatus.Suspended)
        {
            throw new ForbiddenException(
                "Your organization account is currently suspended. Please contact your administrator.");
        }

        return organization.Id;
    }
}
