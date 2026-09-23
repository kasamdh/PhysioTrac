using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
/// which organization it belongs to.</summary>
[ApiController]
[Route("api/v1/providers/{providerId:guid}/licenses")]
[Authorize]
public class ProviderLicensesController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly PhysioTracDbContext _db;

    public ProviderLicensesController(ITenantAccessService tenantAccess, ICurrentUser currentUser, PhysioTracDbContext db)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
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
        l.Status, l.IsCompactPrivilege, l.Notes, l.IsExpired, l.IsExpiringSoon);
}

public record ProviderLicenseDto(
    Guid Id, Guid ProviderId, string State, string LicenseNumber, DateOnly? IssueDate, DateOnly ExpirationDate,
    ProviderLicenseStatus Status, bool IsCompactPrivilege, string? Notes, bool IsExpired, bool IsExpiringSoon);

public record CreateProviderLicenseRequest(
    string State, string LicenseNumber, DateOnly? IssueDate, DateOnly ExpirationDate,
    ProviderLicenseStatus Status, bool IsCompactPrivilege, string? Notes);

public record UpdateProviderLicenseRequest(DateOnly ExpirationDate, ProviderLicenseStatus Status, string? Notes);
