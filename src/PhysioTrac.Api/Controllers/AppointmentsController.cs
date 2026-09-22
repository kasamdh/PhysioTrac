using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Domain.Entities;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/appointments")]
[Authorize]
public class AppointmentsController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IAppointmentService _appointments;

    public AppointmentsController(ICurrentUser currentUser, IAppointmentService appointments)
    {
        _currentUser = currentUser;
        _appointments = appointments;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to)
    {
        try
        {
            var appointments = await _appointments.ListForRangeAsync(_currentUser, from, to, HttpContext.RequestAborted);
            return Ok(appointments.Select(ToDto));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentRequest request)
    {
        try
        {
            var appointment = await _appointments.CreateAsync(request, _currentUser, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(List), null, ToDto(appointment));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    [HttpPatch("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        try
        {
            var appointment = await _appointments.CancelAsync(id, _currentUser, HttpContext.RequestAborted);
            return Ok(ToDto(appointment));
        }
        catch (ForbiddenException ex) { return Forbid(ex.Message); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    private static AppointmentDto ToDto(Appointment a) => new(
        a.Id, a.PatientId, a.TherapistId, a.ProviderId, a.Kind, a.Status,
        a.StartsAt, a.EndsAt, a.LocationDetailId, a.IsHomeVisit, a.ReasonForVisit,
        a.ConfirmationNumber, a.ConfirmedAt);
}
