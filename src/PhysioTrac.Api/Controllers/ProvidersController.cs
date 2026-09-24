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
                .Include(p => p.Locations)
                .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
                .ToListAsync(HttpContext.RequestAborted);
            return Ok(providers.Select(ToDto));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        try
        {
            var provider = await LoadProviderInOrgAsync(id, HttpContext.RequestAborted);
            return Ok(ToDto(provider));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
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
                NpiNumber = request.NpiNumber,
                UserId = request.UserId,
            };
            await AssignLocationsAsync(provider, organization.Id, request.LocationIds, HttpContext.RequestAborted);

            _db.Providers.Add(provider);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return CreatedAtAction(nameof(Get), new { id = provider.Id }, ToDto(provider));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProviderRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Scheduling);
            var provider = await LoadProviderInOrgAsync(id, HttpContext.RequestAborted);

            provider.FirstName = request.FirstName;
            provider.LastName = request.LastName;
            provider.Specialty = request.Specialty;
            provider.Credentials = request.Credentials;
            provider.NpiNumber = request.NpiNumber;
            provider.IsActive = request.IsActive;
            provider.UpdatedAt = DateTimeOffset.UtcNow;

            if (request.LocationIds is not null)
            {
                provider.Locations.Clear();
                await AssignLocationsAsync(provider, provider.OrganizationId, request.LocationIds, HttpContext.RequestAborted);
            }

            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return Ok(ToDto(provider));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    private async Task AssignLocationsAsync(Provider provider, Guid organizationId, IReadOnlyList<Guid>? locationIds, CancellationToken ct)
    {
        if (locationIds is not { Count: > 0 }) return;

        // Silently drops any id that isn't one of the caller's own org's
        // locations -- the same "never trust a client-supplied id across a
        // tenant boundary" rule every other assignment in this Api follows.
        var locations = await _db.Locations
            .Where(l => l.OrganizationId == organizationId && locationIds.Contains(l.Id))
            .ToListAsync(ct);
        foreach (var location in locations) provider.Locations.Add(location);
    }

    private async Task<Provider> LoadProviderInOrgAsync(Guid id, CancellationToken ct)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, ct);
        var provider = await _db.Providers.Include(p => p.Locations).FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Provider was not found.");
        if (provider.OrganizationId != organization.Id)
        {
            throw new NotFoundException("Provider was not found.");
        }
        return provider;
    }

    private static ProviderDto ToDto(Provider p) => new(
        p.Id, p.FirstName, p.LastName, p.FullName, p.Specialty, p.Credentials, p.NpiNumber,
        p.IsActive, p.OnlineBookingEnabled, p.Locations.Select(l => l.Id).ToList());
}
