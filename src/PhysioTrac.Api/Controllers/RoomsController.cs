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

/// <summary>Treatment rooms/bays within a location -- the resource
/// scheduling's "room conflict" detection checks against. Nested under a
/// locationId so tenant scoping is unavoidable, the same pattern
/// ProviderLicensesController uses for a provider's licenses.</summary>
[ApiController]
[Route("api/v1/locations/{locationId:guid}/rooms")]
[Authorize]
public class RoomsController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly PhysioTracDbContext _db;
    private readonly IAuditService _audit;

    public RoomsController(ITenantAccessService tenantAccess, ICurrentUser currentUser, PhysioTracDbContext db, IAuditService audit)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid locationId, [FromQuery] bool includeInactive = false)
    {
        try
        {
            var location = await LoadLocationInOrgAsync(locationId, HttpContext.RequestAborted);
            var query = _db.Rooms.Where(r => r.LocationId == location.Id);
            if (!includeInactive) query = query.Where(r => r.IsActive);

            var rooms = await query.OrderBy(r => r.Name).ToListAsync(HttpContext.RequestAborted);
            return Ok(rooms.Select(ToDto));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid locationId, [FromBody] CreateRoomRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.OrganizationAdministration);
            var location = await LoadLocationInOrgAsync(locationId, HttpContext.RequestAborted);

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return UnprocessableEntity(new { detail = "A room name is required." });
            }

            var room = new Room { LocationId = location.Id, Name = request.Name.Trim() };
            _db.Rooms.Add(room);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            // Room has no OrganizationId of its own (only via Location), so
            // it isn't covered by EntityChangeAuditInterceptor.
            await _audit.RecordAuditEventAsync(_currentUser.UserId, "room.created", nameof(Room), room.Id,
                location.OrganizationId, ct: HttpContext.RequestAborted);

            return CreatedAtAction(nameof(List), new { locationId }, ToDto(room));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpPatch("{roomId:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid locationId, Guid roomId)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.OrganizationAdministration);
            var location = await LoadLocationInOrgAsync(locationId, HttpContext.RequestAborted);
            var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == roomId && r.LocationId == location.Id, HttpContext.RequestAborted)
                ?? throw new NotFoundException("Room was not found.");

            room.IsActive = false;
            room.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            await _audit.RecordAuditEventAsync(_currentUser.UserId, "room.deactivated", nameof(Room), room.Id,
                location.OrganizationId, ct: HttpContext.RequestAborted);

            return Ok(ToDto(room));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    private async Task<Location> LoadLocationInOrgAsync(Guid locationId, CancellationToken ct)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, ct);
        var location = await _db.Locations.FirstOrDefaultAsync(l => l.Id == locationId, ct)
            ?? throw new NotFoundException("Location was not found.");
        if (location.OrganizationId != organization.Id)
        {
            throw new NotFoundException("Location was not found.");
        }
        return location;
    }

    private static RoomDto ToDto(Room r) => new(r.Id, r.LocationId, r.Name, r.IsActive);
}

public record RoomDto(Guid Id, Guid LocationId, string Name, bool IsActive);

public record CreateRoomRequest(string Name);
