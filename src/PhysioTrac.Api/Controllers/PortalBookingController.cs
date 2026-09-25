using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Booking;
using PhysioTrac.Application.Common;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Api.Controllers;

/// <summary>Patient-portal booking/waitlist — direct port of the portal
/// half of `care/booking.py`. Every action resolves "which patient" via
/// <c>ITenantAccessService.RequirePortalPatientAsync</c> inside the service,
/// never from a client-supplied patient id.</summary>
[ApiController]
[Route("api/v1/portal")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.Portal)]
public class PortalBookingController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IPortalBookingService _booking;

    public PortalBookingController(ICurrentUser currentUser, IPortalBookingService booking)
    {
        _currentUser = currentUser;
        _booking = booking;
    }

    private IActionResult BookingError(BookingException ex) =>
        StatusCode(ex.Status, new { detail = ex.Message, code = ex.Code, field = ex.Field });

    [HttpPost("appointments")]
    public async Task<IActionResult> Create([FromBody] PortalBookingRequest request)
    {
        try
        {
            var appointment = await _booking.CreateAsync(_currentUser, request, HttpContext.RequestAborted);
            return StatusCode(201, ToDto(appointment));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (BookingException ex) { return BookingError(ex); }
    }

    [HttpPatch("appointments/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        try
        {
            var appointment = await _booking.CancelAsync(_currentUser, id, HttpContext.RequestAborted);
            return Ok(ToDto(appointment));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (BookingException ex) { return BookingError(ex); }
    }

    [HttpPatch("appointments/{id:guid}/reschedule")]
    public async Task<IActionResult> Reschedule(Guid id, [FromBody] PortalRescheduleRequest request)
    {
        try
        {
            var appointment = await _booking.RescheduleAsync(_currentUser, id, request, HttpContext.RequestAborted);
            return Ok(ToDto(appointment));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (BookingException ex) { return BookingError(ex); }
    }

    [HttpPatch("appointments/{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id)
    {
        try
        {
            var appointment = await _booking.ConfirmAsync(_currentUser, id, HttpContext.RequestAborted);
            return Ok(ToDto(appointment));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (BookingException ex) { return BookingError(ex); }
    }

    [HttpGet("waitlist")]
    public async Task<IActionResult> ListWaitlist()
    {
        try
        {
            var entries = await _booking.ListWaitlistAsync(_currentUser, HttpContext.RequestAborted);
            return Ok(entries.Select(ToWaitlistDto));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost("waitlist")]
    public async Task<IActionResult> JoinWaitlist([FromBody] JoinWaitlistRequest request)
    {
        try
        {
            var entry = await _booking.JoinWaitlistAsync(_currentUser, request, HttpContext.RequestAborted);
            return StatusCode(201, ToWaitlistDto(entry));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (BookingException ex) { return BookingError(ex); }
    }

    [HttpDelete("waitlist/{id:guid}")]
    public async Task<IActionResult> LeaveWaitlist(Guid id)
    {
        try
        {
            var entry = await _booking.LeaveWaitlistAsync(_currentUser, id, HttpContext.RequestAborted);
            return Ok(ToWaitlistDto(entry));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (BookingException ex) { return BookingError(ex); }
    }

    private static object ToDto(Appointment a) => new
    {
        a.Id,
        a.PatientId,
        a.ProviderId,
        a.Kind,
        a.Status,
        a.StartsAt,
        a.EndsAt,
        a.LocationDetailId,
        a.ReasonForVisit,
        a.ConfirmationNumber,
        a.ConfirmedAt,
    };

    private static object ToWaitlistDto(Waitlist w) => new
    {
        w.Id,
        w.LocationId,
        w.AppointmentTypeId,
        w.ProviderId,
        w.EarliestDate,
        w.LatestDate,
        w.Notes,
        w.Status,
    };
}
