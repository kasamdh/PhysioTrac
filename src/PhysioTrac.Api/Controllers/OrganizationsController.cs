using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

/// <summary>Caller-scoped organization info and profile administration --
/// there is deliberately no "list all organizations" or "get organization
/// by id" action here (that's SuperAdminClientsController, platform-super-
/// admin-only); OrganizationRequiredAsync always resolves strictly from the
/// caller's own claims, so an Organization Admin can only ever read or
/// write their own organization, never another tenant's.
///
/// Organization has no OrganizationId property of its own (it IS the
/// tenant boundary), so unlike Location/Provider it is NOT covered by
/// EntityChangeAuditInterceptor's automatic entity-change audit -- Update
/// below writes an explicit audit event for that reason.</summary>
[ApiController]
[Route("api/v1/organizations")]
[Authorize]
public class OrganizationsController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _audit;
    private readonly PhysioTracDbContext _db;

    public OrganizationsController(ITenantAccessService tenantAccess, ICurrentUser currentUser, IAuditService audit, PhysioTracDbContext db)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
        _audit = audit;
        _db = db;
    }

    [HttpGet("current")]
    public async Task<IActionResult> Current()
    {
        try
        {
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);
            var locations = await _db.Locations
                .Where(l => l.OrganizationId == organization.Id && l.IsActive)
                .OrderBy(l => l.Name)
                .Select(l => new LocationSummaryDto(l.Id, l.Name))
                .ToListAsync(HttpContext.RequestAborted);

            return Ok(new CurrentOrganizationDto(organization.Id, organization.Name, locations));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        try
        {
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);
            return Ok(ToProfileDto(organization));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateOrganizationProfileRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.OrganizationAdministration);
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return UnprocessableEntity(new { detail = "An organization name is required." });
            }

            var changedFields = new List<string>();
            void Track<T>(string name, T oldValue, T newValue)
            {
                if (!Equals(oldValue, newValue)) changedFields.Add(name);
            }

            Track(nameof(organization.Name), organization.Name, request.Name.Trim());
            Track(nameof(organization.Timezone), organization.Timezone, request.Timezone);
            Track(nameof(organization.NpiNumber), organization.NpiNumber, request.NpiNumber);
            Track(nameof(organization.TaxId), organization.TaxId, request.TaxId);
            Track(nameof(organization.SupportEmail), organization.SupportEmail, request.SupportEmail);
            Track(nameof(organization.SupportPhone), organization.SupportPhone, request.SupportPhone);
            Track(nameof(organization.PtaCosignRequired), organization.PtaCosignRequired, request.PtaCosignRequired);

            organization.Name = request.Name.Trim();
            organization.Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? organization.Timezone : request.Timezone;
            organization.NpiNumber = request.NpiNumber;
            organization.TaxId = request.TaxId;
            organization.AddressLine1 = request.AddressLine1;
            organization.AddressLine2 = request.AddressLine2;
            organization.City = request.City;
            organization.State = request.State;
            organization.ZipCode = request.ZipCode;
            organization.SupportEmail = request.SupportEmail;
            organization.SupportPhone = request.SupportPhone;
            organization.PtaCosignRequired = request.PtaCosignRequired;
            organization.UpdatedAt = DateTimeOffset.UtcNow;
            organization.UpdatedById = _currentUser.UserId;

            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            if (changedFields.Count > 0)
            {
                await _audit.RecordAuditEventAsync(
                    actorId: _currentUser.UserId,
                    action: "entity.updated",
                    objectType: nameof(Organization),
                    objectId: organization.Id,
                    organizationId: organization.Id,
                    metadata: new { changedProperties = changedFields },
                    ct: HttpContext.RequestAborted);
            }

            return Ok(ToProfileDto(organization));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    private static OrganizationProfileDto ToProfileDto(Organization o) => new(
        o.Id, o.Name, o.Timezone, o.NpiNumber, o.TaxId, o.AddressLine1, o.AddressLine2,
        o.City, o.State, o.ZipCode, o.SupportEmail, o.SupportPhone, o.PtaCosignRequired);
}

public record LocationSummaryDto(Guid Id, string Name);

public record CurrentOrganizationDto(Guid Id, string Name, IReadOnlyList<LocationSummaryDto> Locations);

public record OrganizationProfileDto(
    Guid Id, string Name, string Timezone, string? NpiNumber, string? TaxId,
    string? AddressLine1, string? AddressLine2, string? City, string? State, string? ZipCode,
    string? SupportEmail, string? SupportPhone, bool PtaCosignRequired);

public record UpdateOrganizationProfileRequest(
    string Name, string? Timezone, string? NpiNumber, string? TaxId,
    string? AddressLine1, string? AddressLine2, string? City, string? State, string? ZipCode,
    string? SupportEmail, string? SupportPhone, bool PtaCosignRequired);
