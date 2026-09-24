using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Entities;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

/// <summary>A patient's request to be seen sooner than their next confirmed
/// slot -- worked from the staff schedule when an opening appears. No
/// automatic slot-matching/notification here (that's a real scheduling-
/// optimization feature on its own, not attempted in this pass) -- staff
/// convert an entry to a real appointment by hand once they spot an
/// opening, via Convert below, which reuses IAppointmentService.CreateAsync
/// so the new appointment gets the exact same conflict checking as any
/// other booking.</summary>
[ApiController]
[Route("api/v1/waitlist")]
[Authorize]
public class WaitlistController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly IAppointmentService _appointments;
    private readonly PhysioTracDbContext _db;

    public WaitlistController(ITenantAccessService tenantAccess, ICurrentUser currentUser, IAppointmentService appointments, PhysioTracDbContext db)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
        _appointments = appointments;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] WaitlistStatus? status = null)
    {
        try
        {
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);
            var query = _db.Waitlists.Where(w => w.OrganizationId == organization.Id);
            if (status is not null) query = query.Where(w => w.Status == status);

            var entries = await query.OrderBy(w => w.EarliestDate).ToListAsync(HttpContext.RequestAborted);
            return Ok(entries.Select(ToDto));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWaitlistEntryRequest request)
    {
        try
        {
            _tenantAccess.RequireRole(_currentUser, RoleSets.Scheduling);
            var patient = await _tenantAccess.RequirePatientAccessAsync(_currentUser, request.PatientId, ct: HttpContext.RequestAborted);
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);

            if (request.LatestDate is DateOnly latest && latest < request.EarliestDate)
            {
                return UnprocessableEntity(new { detail = "Latest date cannot be before the earliest date." });
            }

            var entry = new Waitlist
            {
                OrganizationId = organization.Id,
                PatientId = patient.Id,
                LocationId = request.LocationId,
                AppointmentTypeId = request.AppointmentTypeId,
                ProviderId = request.ProviderId,
                EarliestDate = request.EarliestDate,
                LatestDate = request.LatestDate,
                Notes = request.Notes,
            };
            _db.Waitlists.Add(entry);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return CreatedAtAction(nameof(List), null, ToDto(entry));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPatch("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        try
        {
            var entry = await LoadInOrgAsync(id, HttpContext.RequestAborted);
            entry.Status = WaitlistStatus.Cancelled;
            entry.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);
            return Ok(ToDto(entry));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    /// <summary>Books the concrete slot a staff member found for this
    /// patient and marks the waitlist entry fulfilled -- the new appointment
    /// goes through the exact same CreateAsync (and its conflict check)
    /// every other booking does; a slot that's actually already taken is
    /// still rejected here.</summary>
    [HttpPost("{id:guid}/convert")]
    public async Task<IActionResult> Convert(Guid id, [FromBody] ConvertWaitlistEntryRequest request)
    {
        try
        {
            var entry = await LoadInOrgAsync(id, HttpContext.RequestAborted);
            if (entry.Status != WaitlistStatus.Active)
            {
                return UnprocessableEntity(new { detail = "Only an active waitlist entry can be converted." });
            }

            var createRequest = new CreateAppointmentRequest(
                entry.PatientId, request.TherapistId, request.ProviderId, entry.LocationId, request.RoomId, entry.AppointmentTypeId,
                request.Kind, request.StartsAt, request.EndsAt, false, "From waitlist");
            var appointment = await _appointments.CreateAsync(createRequest, _currentUser, HttpContext.RequestAborted);

            entry.Status = WaitlistStatus.Fulfilled;
            entry.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            return Ok(new { appointmentId = appointment.Id });
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    private async Task<Waitlist> LoadInOrgAsync(Guid id, CancellationToken ct)
    {
        var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, ct);
        var entry = await _db.Waitlists.FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new NotFoundException("Waitlist entry was not found.");
        if (entry.OrganizationId != organization.Id)
        {
            throw new NotFoundException("Waitlist entry was not found.");
        }
        return entry;
    }

    private static WaitlistDto ToDto(Waitlist w) => new(
        w.Id, w.PatientId, w.LocationId, w.AppointmentTypeId, w.ProviderId, w.EarliestDate, w.LatestDate, w.Notes, w.Status);
}

public record WaitlistDto(
    Guid Id, Guid PatientId, Guid? LocationId, Guid? AppointmentTypeId, Guid? ProviderId,
    DateOnly EarliestDate, DateOnly? LatestDate, string? Notes, WaitlistStatus Status);

public record CreateWaitlistEntryRequest(
    Guid PatientId, Guid? LocationId, Guid? AppointmentTypeId, Guid? ProviderId, DateOnly EarliestDate, DateOnly? LatestDate, string? Notes);

public record ConvertWaitlistEntryRequest(
    Guid TherapistId, Guid? ProviderId, Guid? RoomId, AppointmentKind Kind, DateTimeOffset StartsAt, DateTimeOffset EndsAt);
