using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

/// <summary>Read-only, caller-scoped organization info -- exists specifically
/// so a JSON client (the React SPA) can show "which organization am I in"
/// and "which locations does it have" without ever seeing another tenant's
/// name or address. There is deliberately no "list all organizations" or
/// "get organization by id" action here -- OrganizationRequiredAsync always
/// resolves strictly from the caller's own claims, never from a
/// client-supplied id.</summary>
[ApiController]
[Route("api/v1/organizations")]
[Authorize]
public class OrganizationsController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly PhysioTracDbContext _db;

    public OrganizationsController(ITenantAccessService tenantAccess, ICurrentUser currentUser, PhysioTracDbContext db)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
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
}

public record LocationSummaryDto(Guid Id, string Name);

public record CurrentOrganizationDto(Guid Id, string Name, IReadOnlyList<LocationSummaryDto> Locations);
