using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

/// <summary>Clinic location administration. Reads are available to any
/// authenticated staff role (front desk needs to see locations to schedule
/// against them) -- writes (create/update/deactivate) are
/// OrganizationAdministration-only (Admin/Director), the same standard
/// applied to the organization profile itself. There's no hard delete: a
/// location with historical appointments/claims/providers attached can
/// never be removed outright, so "delete" here means IsActive = false, the
/// same convention Provider/AppointmentType already use.
///
/// Entity-level create/update/deactivate changes are audited automatically
/// by EntityChangeAuditInterceptor (Location has its own OrganizationId and
/// inherits BaseEntity) -- no explicit IAuditService call needed here.</summary>
[ApiController]
[Route("api/v1/locations")]
[Authorize]
public class LocationsController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly PhysioTracDbContext _db;

    public LocationsController(ITenantAccessService tenantAccess, ICurrentUser currentUser, PhysioTracDbContext db)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive = false)
    {
        try
        {
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);
            var query = _db.Locations.Where(l => l.OrganizationId == organization.Id);
            if (!includeInactive) query = query.Where(l => l.IsActive);

            var locations = await query.OrderBy(l => l.Name).ToListAsync(HttpContext.RequestAborted);
            return Ok(locations.Select(ToDto));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        try
        {
            var location = await LoadLocationInOrgAsync(id, HttpContext.RequestAborted);
            return Ok(ToDto(location));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLocationRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.OrganizationAdministration);
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return UnprocessableEntity(new { detail = "A location name is required." });
            }

            var location = new Location
            {
                OrganizationId = organization.Id,
                Name = request.Name.Trim(),
                AddressLine1 = request.AddressLine1,
                AddressLine2 = request.AddressLine2,
                City = request.City,
                State = request.State,
                ZipCode = request.ZipCode,
                Phone = request.Phone,
                Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? "America/Los_Angeles" : request.Timezone,
                NpiNumber = request.NpiNumber,
                TaxId = request.TaxId,
            };
            _db.Locations.Add(location);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return CreatedAtAction(nameof(Get), new { id = location.Id }, ToDto(location));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLocationRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.OrganizationAdministration);
            var location = await LoadLocationInOrgAsync(id, HttpContext.RequestAborted);

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return UnprocessableEntity(new { detail = "A location name is required." });
            }

            location.Name = request.Name.Trim();
            location.AddressLine1 = request.AddressLine1;
            location.AddressLine2 = request.AddressLine2;
            location.City = request.City;
            location.State = request.State;
            location.ZipCode = request.ZipCode;
            location.Phone = request.Phone;
            location.Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? location.Timezone : request.Timezone;
            location.NpiNumber = request.NpiNumber;
            location.TaxId = request.TaxId;
            location.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return Ok(ToDto(location));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.OrganizationAdministration);
            var location = await LoadLocationInOrgAsync(id, HttpContext.RequestAborted);
            location.IsActive = false;
            location.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return Ok(ToDto(location));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.OrganizationAdministration);
            var location = await LoadLocationInOrgAsync(id, HttpContext.RequestAborted);
            location.IsActive = true;
            location.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return Ok(ToDto(location));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    private async Task<Location> LoadLocationInOrgAsync(Guid id, CancellationToken ct)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, ct);
        var location = await _db.Locations.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new NotFoundException("Location was not found.");
        if (location.OrganizationId != organization.Id)
        {
            throw new NotFoundException("Location was not found.");
        }
        return location;
    }

    private static LocationDto ToDto(Location l) => new(
        l.Id, l.Name, l.AddressLine1, l.AddressLine2, l.City, l.State, l.ZipCode,
        l.Phone, l.Timezone, l.NpiNumber, l.TaxId, l.IsActive);
}

public record LocationDto(
    Guid Id, string Name, string? AddressLine1, string? AddressLine2, string? City, string? State, string? ZipCode,
    string? Phone, string Timezone, string? NpiNumber, string? TaxId, bool IsActive);

public record CreateLocationRequest(
    string Name, string? AddressLine1, string? AddressLine2, string? City, string? State, string? ZipCode,
    string? Phone, string? Timezone, string? NpiNumber, string? TaxId);

public record UpdateLocationRequest(
    string Name, string? AddressLine1, string? AddressLine2, string? City, string? State, string? ZipCode,
    string? Phone, string? Timezone, string? NpiNumber, string? TaxId);
