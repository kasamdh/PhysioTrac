using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

/// <summary>PT/PTA license-by-state records for one provider. Every route is
/// nested under a providerId so tenant scoping is unavoidable: the provider
/// itself is always re-loaded and org-checked server-side before any of its
/// licenses are touched -- a license id alone is never trusted to imply
/// which organization it belongs to.
///
/// ProviderLicense has no OrganizationId of its own (scoped via
/// ProviderId -> Provider.OrganizationId), so unlike Location/Provider it
/// is NOT covered by EntityChangeAuditInterceptor's automatic entity-change
/// audit -- Create/Update below write explicit audit events for that
/// reason.</summary>
[ApiController]
[Route("api/v1/providers/{providerId:guid}/licenses")]
[Authorize]
public class ProviderLicensesController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;
    private readonly PhysioTracDbContext _db;

    public ProviderLicensesController(ITenantAccessService tenantAccess, ICurrentUser currentUser, IAuditService audit, PhysioTracDbContext db)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
        _audit = audit;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid providerId)
    {
        try
        {
            var provider = await LoadProviderInOrgAsync(providerId, HttpContext.RequestAborted);
            var licenses = await _db.ProviderLicenses.Where(l => l.ProviderId == provider.Id)
                .OrderBy(l => l.State).ToListAsync(HttpContext.RequestAborted);
            return Ok(licenses.Select(ToDto));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid providerId, [FromBody] CreateProviderLicenseRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Scheduling);
            var provider = await LoadProviderInOrgAsync(providerId, HttpContext.RequestAborted);

            if (string.IsNullOrWhiteSpace(request.State) || request.State.Trim().Length != 2)
            {
                return UnprocessableEntity(new { detail = "State must be a 2-letter USPS code (e.g. NC)." });
            }
            if (string.IsNullOrWhiteSpace(request.LicenseNumber))
            {
                return UnprocessableEntity(new { detail = "A license number is required." });
            }

            var license = new ProviderLicense
            {
                ProviderId = provider.Id,
                State = request.State.Trim().ToUpperInvariant(),
                LicenseNumber = request.LicenseNumber.Trim(),
                IssueDate = request.IssueDate,
                ExpirationDate = request.ExpirationDate,
                Status = request.Status,
                IsCompactPrivilege = request.IsCompactPrivilege,
                Notes = request.Notes,
            };
            _db.ProviderLicenses.Add(license);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            await _audit.RecordAuditEventAsync(
                _currentUser.UserId, "entity.created", nameof(ProviderLicense), license.Id, provider.OrganizationId,
                metadata: new { providerId = provider.Id, state = license.State }, ct: HttpContext.RequestAborted);

            return CreatedAtAction(nameof(List), new { providerId }, ToDto(license));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (DbUpdateException) { return Conflict(new { detail = "This provider already has a license on file for that state and license number." }); }
    }

    [HttpPut("{licenseId:guid}")]
    public async Task<IActionResult> Update(Guid providerId, Guid licenseId, [FromBody] UpdateProviderLicenseRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Scheduling);
            var provider = await LoadProviderInOrgAsync(providerId, HttpContext.RequestAborted);
            var license = await _db.ProviderLicenses.FirstOrDefaultAsync(l => l.Id == licenseId && l.ProviderId == provider.Id, HttpContext.RequestAborted)
                ?? throw new NotFoundException("License was not found.");

            license.ExpirationDate = request.ExpirationDate;
            license.Status = request.Status;
            license.Notes = request.Notes;
            license.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            await _audit.RecordAuditEventAsync(
                _currentUser.UserId, "entity.updated", nameof(ProviderLicense), license.Id, provider.OrganizationId,
                metadata: new { providerId = provider.Id, state = license.State }, ct: HttpContext.RequestAborted);

            return Ok(ToDto(license));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    private async Task<Provider> LoadProviderInOrgAsync(Guid providerId, CancellationToken ct)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, ct);
        var provider = await _db.Providers.FirstOrDefaultAsync(p => p.Id == providerId, ct)
            ?? throw new NotFoundException("Provider was not found.");
        if (provider.OrganizationId != organization.Id)
        {
            throw new NotFoundException("Provider was not found.");
        }
        return provider;
    }

    private static ProviderLicenseDto ToDto(ProviderLicense l) => new(
        l.Id, l.ProviderId, l.State, l.LicenseNumber, l.IssueDate, l.ExpirationDate,
        l.Status, l.IsCompactPrivilege, l.Notes, l.IsExpired, l.IsExpiringSoon, l.ExpirationAlertLevel);
}

/// <summary>Org-wide expiration-alert report, deliberately not nested under
/// a providerId -- a Compliance/Admin user needs "everything expiring soon
/// across the whole organization", not one provider at a time.</summary>
[ApiController]
[Route("api/v1/providers/licenses/expiring")]
[Authorize]
public class ExpiringLicensesController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly PhysioTracDbContext _db;

    public ExpiringLicensesController(ITenantAccessService tenantAccess, ICurrentUser currentUser, PhysioTracDbContext db)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        try
        {
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);
            var licenses = await _db.ProviderLicenses
                .Include(l => l.Provider)
                .Where(l => l.Provider!.OrganizationId == organization.Id && l.Status == ProviderLicenseStatus.Active)
                .ToListAsync(HttpContext.RequestAborted);

            var alerts = licenses
                .Select(l => new ExpiringLicenseDto(
                    l.Id, l.ProviderId, l.Provider!.FullName, l.State, l.LicenseNumber,
                    l.ExpirationDate, l.DaysUntilExpiration, l.ExpirationAlertLevel))
                .Where(a => a.AlertLevel != LicenseExpirationAlertLevel.None)
                .OrderBy(a => a.DaysUntilExpiration)
                .ToList();

            return Ok(alerts);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }
}

public record ExpiringLicenseDto(
    Guid LicenseId, Guid ProviderId, string ProviderName, string State, string LicenseNumber,
    DateOnly ExpirationDate, int DaysUntilExpiration, LicenseExpirationAlertLevel AlertLevel);

public record ProviderLicenseDto(
    Guid Id, Guid ProviderId, string State, string LicenseNumber, DateOnly? IssueDate, DateOnly ExpirationDate,
    ProviderLicenseStatus Status, bool IsCompactPrivilege, string? Notes, bool IsExpired, bool IsExpiringSoon,
    LicenseExpirationAlertLevel AlertLevel);

public record CreateProviderLicenseRequest(
    string State, string LicenseNumber, DateOnly? IssueDate, DateOnly ExpirationDate,
    ProviderLicenseStatus Status, bool IsCompactPrivilege, string? Notes);

public record UpdateProviderLicenseRequest(DateOnly ExpirationDate, ProviderLicenseStatus Status, string? Notes);
