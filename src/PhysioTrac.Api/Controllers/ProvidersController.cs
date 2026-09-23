using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/providers")]
[Authorize]
public class ProvidersController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly PhysioTracDbContext _db;

    public ProvidersController(ITenantAccessService tenantAccess, ICurrentUser currentUser, PhysioTracDbContext db)
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
            var providers = await _db.Providers.Where(p => p.OrganizationId == organization.Id)
                .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
                .ToListAsync(HttpContext.RequestAborted);
            return Ok(providers.Select(ToDto));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProviderRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Scheduling);
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);

            var provider = new Provider
            {
                OrganizationId = organization.Id,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Specialty = request.Specialty,
                Credentials = request.Credentials,
                UserId = request.UserId,
            };
            if (request.LocationIds is { Count: > 0 })
            {
                var locations = await _db.Locations
                    .Where(l => l.OrganizationId == organization.Id && request.LocationIds.Contains(l.Id))
                    .ToListAsync(HttpContext.RequestAborted);
                foreach (var loc in locations) provider.Locations.Add(loc);
            }

            _db.Providers.Add(provider);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return CreatedAtAction(nameof(List), null, ToDto(provider));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    private static ProviderDto ToDto(Provider p) => new(p.Id, p.FirstName, p.LastName, p.FullName, p.Specialty, p.IsActive, p.OnlineBookingEnabled);
}
