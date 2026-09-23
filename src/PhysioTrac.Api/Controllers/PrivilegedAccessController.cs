using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Patients;
using PhysioTrac.Application.SuperAdmin;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

/// <summary>Break-glass clinical access for platform super admins, who have
/// zero standing access to any tenant's data — direct port of the
/// `privileged_access_grants` / `privileged_patients*` views in
/// `care/api/super_admin.py`. Every grant, and every read taken under one,
/// is audited.</summary>
[ApiController]
[Route("api/v1/super-admin/clients/{clientNumber:long}")]
[Authorize]
public class PrivilegedAccessController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly IClientProvisioningService _clients;
    private readonly IPrivilegedAccessService _privilegedAccess;
    private readonly Application.Audit.IAuditService _audit;
    private readonly PhysioTracDbContext _db;

    public PrivilegedAccessController(
        ITenantAccessService tenantAccess, ICurrentUser currentUser, IClientProvisioningService clients,
        IPrivilegedAccessService privilegedAccess, Application.Audit.IAuditService audit, PhysioTracDbContext db)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
        _clients = clients;
        _privilegedAccess = privilegedAccess;
        _audit = audit;
        _db = db;
    }

    private void RequireSuperAdmin() => _tenantAccess.RequirePlatformSuperAdmin(_currentUser);

    private async Task<Organization> RequireClientAsync(long clientNumber, CancellationToken ct)
    {
        return await _db.Organizations.FirstOrDefaultAsync(o => o.ClientNumber == clientNumber, ct)
            ?? throw new NotFoundException("Client was not found.");
    }

    [HttpGet("privileged-access")]
    public async Task<IActionResult> ListGrants(long clientNumber)
    {
        try
        {
            RequireSuperAdmin();
            var client = await RequireClientAsync(clientNumber, HttpContext.RequestAborted);
            var grants = await _privilegedAccess.ListAsync(client.Id, HttpContext.RequestAborted);
            return Ok(new { grants = await ToDtosAsync(grants, HttpContext.RequestAborted) });
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpPost("privileged-access")]
    public async Task<IActionResult> RequestGrant(long clientNumber, [FromBody] RequestPrivilegedAccessRequest request)
    {
        try
        {
            RequireSuperAdmin();
            var client = await RequireClientAsync(clientNumber, HttpContext.RequestAborted);
            var grant = await _privilegedAccess.RequestAsync(client.Id, _currentUser, request.Reason, request.DurationHours, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(ListGrants), new { clientNumber }, new { grant = await ToDtoAsync(grant, HttpContext.RequestAborted) });
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    [HttpPatch("privileged-access/{grantId:guid}/revoke")]
    public async Task<IActionResult> RevokeGrant(long clientNumber, Guid grantId)
    {
        try
        {
            RequireSuperAdmin();
            await RequireClientAsync(clientNumber, HttpContext.RequestAborted);
            var grant = await _privilegedAccess.RevokeAsync(grantId, _currentUser, HttpContext.RequestAborted);
            return Ok(new { grant = await ToDtoAsync(grant, HttpContext.RequestAborted) });
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    /// <summary>Returns null and a structured 403 (matching the original's
    /// `PRIVILEGED_ACCESS_REQUIRED` code) if the caller has no active grant —
    /// never a plain 403, so the frontend knows to offer "request access."</summary>
    private async Task<(PrivilegedAccessGrant? Grant, IActionResult? Error)> RequireActiveGrantAsync(Organization client, CancellationToken ct)
    {
        var grant = await _privilegedAccess.ActiveGrantAsync(client.Id, _currentUser.UserId, ct);
        if (grant is null)
        {
            return (null, StatusCode(403, new
            {
                timestamp = DateTimeOffset.UtcNow,
                status = 403,
                code = "PRIVILEGED_ACCESS_REQUIRED",
                message = "Request time-boxed privileged access to this client before viewing clinical records.",
            }));
        }
        return (grant, null);
    }

    [HttpGet("privileged-patients")]
    public async Task<IActionResult> ListPatients(long clientNumber)
    {
        try
        {
            RequireSuperAdmin();
            var client = await RequireClientAsync(clientNumber, HttpContext.RequestAborted);
            var (grant, error) = await RequireActiveGrantAsync(client, HttpContext.RequestAborted);
            if (error is not null) return error;

            var patients = await _db.Patients.Where(p => p.OrganizationId == client.Id)
                .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
                .ToListAsync(HttpContext.RequestAborted);

            await _audit.RecordAuditEventAsync(_currentUser.UserId, "privileged_access.patient_list_viewed",
                nameof(PrivilegedAccessGrant), grant!.Id, client.Id,
                metadata: new { clientNumber, patientCount = patients.Count }, ct: HttpContext.RequestAborted);

            return Ok(new { patients = patients.Select(ToPatientDto) });
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpGet("privileged-patients/{patientId:guid}")]
    public async Task<IActionResult> GetPatient(long clientNumber, Guid patientId)
    {
        try
        {
            RequireSuperAdmin();
            var client = await RequireClientAsync(clientNumber, HttpContext.RequestAborted);
            var (grant, error) = await RequireActiveGrantAsync(client, HttpContext.RequestAborted);
            if (error is not null) return error;

            var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == patientId && p.OrganizationId == client.Id, HttpContext.RequestAborted);
            if (patient is null) return NotFound(new { detail = "Patient was not found." });

            await _audit.RecordAuditEventAsync(_currentUser.UserId, "privileged_access.patient_viewed",
                nameof(PrivilegedAccessGrant), grant!.Id, client.Id, patientId: patient.Id,
                metadata: new { clientNumber, reason = grant.Reason }, ct: HttpContext.RequestAborted);

            // Clinical detail (notes/goals/outcomes) isn't ported yet — those
            // entities land in a later module. This returns the chart header
            // only, unlike the original's fuller clinical bundle.
            return Ok(new { patient = ToPatientDto(patient), grant = await ToDtoAsync(grant!, HttpContext.RequestAborted) });
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    private async Task<string> DisplayNameAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return userId.ToString();
        var name = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrEmpty(name) ? (user.UserName ?? userId.ToString()) : name;
    }

    private async Task<PrivilegedAccessGrantDto> ToDtoAsync(PrivilegedAccessGrant grant, CancellationToken ct) => new(
        grant.Id, await DisplayNameAsync(grant.ActorId, ct), grant.Reason, grant.CreatedAt, grant.ExpiresAt,
        grant.RevokedAt, grant.RevokedById is Guid revokedBy ? await DisplayNameAsync(revokedBy, ct) : null, grant.IsActive);

    private async Task<List<PrivilegedAccessGrantDto>> ToDtosAsync(IReadOnlyList<PrivilegedAccessGrant> grants, CancellationToken ct)
    {
        var result = new List<PrivilegedAccessGrantDto>();
        foreach (var grant in grants) result.Add(await ToDtoAsync(grant, ct));
        return result;
    }

    private static PatientDto ToPatientDto(Patient p) => new(
        p.Id, p.MedicalRecordNumber, p.FirstName, p.LastName, p.FullName,
        p.DateOfBirth, p.Age, p.Phone, p.Email, p.AssignedTherapistId, p.Status);
}
