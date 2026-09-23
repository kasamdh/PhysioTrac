using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Application.Tenancy;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/availability")]
[Authorize]
public class AvailabilityController : ControllerBase
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly ICurrentUser _currentUser;
    private readonly IAvailabilityService _availability;

    public AvailabilityController(ITenantAccessService tenantAccess, ICurrentUser currentUser, IAvailabilityService availability)
    {
        _tenantAccess = tenantAccess;
        _currentUser = currentUser;
        _availability = availability;
    }

    /// <summary>Authenticated, staff-facing slot lookup — a preview only.
    /// <see cref="IAppointmentService.CreateAsync"/> re-validates everything
    /// independently before committing a booking, exactly as the original's
    /// booking.py never trusts this endpoint's output.</summary>
    [HttpGet("slots")]
    public async Task<IActionResult> GetSlots(
        [FromQuery] Guid locationId, [FromQuery] Guid appointmentTypeId, [FromQuery] DateOnly date,
        [FromQuery] Guid? providerId = null, [FromQuery] Guid? excludeAppointmentId = null)
    {
        try
        {
            var organization = await _tenantAccess.OrganizationRequiredAsync(_currentUser, HttpContext.RequestAborted);
            var slots = await _availability.GetAvailableSlotsAsync(
                organization.Id, locationId, appointmentTypeId, date, providerId, excludeAppointmentId, HttpContext.RequestAborted);
            return Ok(slots);
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }
}
