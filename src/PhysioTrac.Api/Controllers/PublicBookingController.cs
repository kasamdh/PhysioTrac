using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Booking;

namespace PhysioTrac.Api.Controllers;

/// <summary>Public, unauthenticated booking API — direct port of
/// `care/api/public_booking.py`. Every action resolves the organization
/// strictly from the URL slug, never a client-supplied id.</summary>
[ApiController]
[Route("api/v1/public/{slug}")]
[AllowAnonymous]
public class PublicBookingController : ControllerBase
{
    private readonly IPublicBookingService _booking;

    public PublicBookingController(IPublicBookingService booking)
    {
        _booking = booking;
    }

    private IActionResult BookingError(BookingException ex) =>
        StatusCode(ex.Status, new { detail = ex.Message, code = ex.Code });

    [HttpGet]
    public async Task<IActionResult> GetOrganization(string slug)
    {
        var organization = await _booking.GetOrganizationAsync(slug, HttpContext.RequestAborted);
        if (organization is null)
        {
            return NotFound(new { detail = "This booking page is not available.", code = "NOT_FOUND" });
        }
        return Ok(organization);
    }

    [HttpGet("locations")]
    public async Task<IActionResult> ListLocations(string slug)
    {
        try
        {
            return Ok(await _booking.ListLocationsAsync(slug, HttpContext.RequestAborted));
        }
        catch (BookingException ex) { return BookingError(ex); }
    }

    [HttpGet("appointment-types")]
    public async Task<IActionResult> ListAppointmentTypes(string slug, [FromQuery(Name = "locationId")] Guid? locationId)
    {
        try
        {
            return Ok(await _booking.ListAppointmentTypesAsync(slug, locationId, HttpContext.RequestAborted));
        }
        catch (BookingException ex) { return BookingError(ex); }
    }

    [HttpGet("providers")]
    public async Task<IActionResult> ListProviders(string slug, [FromQuery] Guid locationId, [FromQuery] Guid appointmentTypeId)
    {
        try
        {
            return Ok(await _booking.ListProvidersAsync(slug, locationId, appointmentTypeId, HttpContext.RequestAborted));
        }
        catch (BookingException ex) { return BookingError(ex); }
    }

    [HttpGet("availability")]
    public async Task<IActionResult> GetAvailability(
        string slug, [FromQuery] Guid locationId, [FromQuery] Guid appointmentTypeId, [FromQuery] DateOnly date, [FromQuery] Guid? providerId)
    {
        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            return Ok(await _booking.GetAvailabilityAsync(slug, locationId, appointmentTypeId, date, providerId, ip, HttpContext.RequestAborted));
        }
        catch (BookingException ex) { return BookingError(ex); }
    }
}

/// <summary>Booking creation is intentionally a separate controller with no
/// `{slug}` route prefix — the slug travels in the body, matching the
/// original's single `public_create_booking` endpoint shape.</summary>
[ApiController]
[Route("api/v1/public/bookings")]
[AllowAnonymous]
public class PublicBookingCreateController : ControllerBase
{
    private readonly IPublicBookingService _booking;

    public PublicBookingCreateController(IPublicBookingService booking)
    {
        _booking = booking;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PublicBookingRequest request)
    {
        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _booking.CreateBookingAsync(request, ip, HttpContext.RequestAborted);
            return StatusCode(201, result);
        }
        catch (BookingException ex)
        {
            return StatusCode(ex.Status, new { detail = ex.Message, code = ex.Code, field = ex.Field });
        }
    }
}
