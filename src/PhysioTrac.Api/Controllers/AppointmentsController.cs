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

    /// <summary>Backs the day/week/month calendar -- providerId/
    /// locationDetailId are the calendar's own filter controls.</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to,
        [FromQuery] Guid? providerId = null, [FromQuery] Guid? locationDetailId = null)
    {
        try
        {
            var appointments = await _appointments.ListForRangeAsync(
                _currentUser, from, to, providerId, locationDetailId, HttpContext.RequestAborted);
            return Ok(appointments.Select(ToDto));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentRequest request)
    {
        try
        {
            var appointment = await _appointments.CreateAsync(request, _currentUser, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(List), null, ToDto(appointment));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (SchedulingConflictException ex) { return SchedulingConflict(ex); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    /// <summary>Drag-and-drop rescheduling lands here -- moves one occurrence
    /// ("edit one"), re-validated server-side with the exact same conflict
    /// check Create runs (provider/patient/room), minus itself.</summary>
    [HttpPatch("{id:guid}/reschedule")]
    public async Task<IActionResult> Reschedule(Guid id, [FromBody] RescheduleAppointmentRequest request)
    {
        try
        {
            var appointment = await _appointments.RescheduleAsync(id, request, _currentUser, HttpContext.RequestAborted);
            return Ok(ToDto(appointment));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (SchedulingConflictException ex) { return SchedulingConflict(ex); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    /// <summary>Dry run of reschedule for the calendar's drag-and-drop
    /// confirmation -- always 200 with IsValid/Violations for a rule problem
    /// (so the UI can show them), 403/404/409 only for permission, tenant,
    /// or appointment-state problems. Saves nothing; the real PATCH
    /// .../reschedule re-runs every check.</summary>
    [HttpPost("{id:guid}/validate-move")]
    public async Task<IActionResult> ValidateMove(Guid id, [FromBody] RescheduleAppointmentRequest request)
    {
        try
        {
            return Ok(await _appointments.ValidateRescheduleAsync(id, request, _currentUser, HttpContext.RequestAborted));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    [HttpPatch("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id) => await TryTransition(() => _appointments.ConfirmAsync(id, _currentUser, HttpContext.RequestAborted));

    [HttpPatch("{id:guid}/check-in")]
    public async Task<IActionResult> CheckIn(Guid id) => await TryTransition(() => _appointments.CheckInAsync(id, _currentUser, HttpContext.RequestAborted));

    [HttpPatch("{id:guid}/start-visit")]
    public async Task<IActionResult> StartVisit(Guid id) => await TryTransition(() => _appointments.StartVisitAsync(id, _currentUser, HttpContext.RequestAborted));

    [HttpPatch("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id) => await TryTransition(() => _appointments.CompleteAsync(id, _currentUser, HttpContext.RequestAborted));

    [HttpPatch("{id:guid}/no-show")]
    public async Task<IActionResult> NoShow(Guid id) => await TryTransition(() => _appointments.MarkNoShowAsync(id, _currentUser, HttpContext.RequestAborted));

    [HttpPatch("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id) => await TryTransition(() => _appointments.CancelAsync(id, _currentUser, HttpContext.RequestAborted));

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> History(Guid id)
    {
        try
        {
            var history = await _appointments.GetStatusHistoryAsync(id, _currentUser, HttpContext.RequestAborted);
            return Ok(history.Select(h => new AppointmentStatusHistoryDto(h.Id, h.FromStatus, h.ToStatus, h.ChangedById, h.Reason, h.CreatedAt)));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    [HttpPost("series")]
    public async Task<IActionResult> CreateSeries([FromBody] CreateAppointmentSeriesRequest request)
    {
        try
        {
            var series = await _appointments.CreateSeriesAsync(request, _currentUser, HttpContext.RequestAborted);
            return CreatedAtAction(nameof(GetSeries), new { seriesId = series.Id }, ToSeriesDto(series));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    /// <summary>Dry run of POST series: each generated date and the rules it
    /// would break, so the UI can show "10 can be booked, 2 conflict".</summary>
    [HttpPost("series/preview")]
    public async Task<IActionResult> PreviewSeries([FromBody] CreateAppointmentSeriesRequest request)
    {
        try
        {
            return Ok(await _appointments.PreviewSeriesAsync(request, _currentUser, HttpContext.RequestAborted));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { detail = ex.Message }); }
    }

    [HttpGet("series/{seriesId:guid}")]
    public async Task<IActionResult> GetSeries(Guid seriesId)
    {
        try
        {
            var series = await _appointments.GetSeriesAsync(seriesId, _currentUser, HttpContext.RequestAborted);
            return Ok(ToSeriesDto(series));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    /// <summary>"Edit series" -- cancels every still-future, still-open
    /// occurrence. See IAppointmentService.CancelSeriesAsync's own doc
    /// comment for exactly what "still-open" means.</summary>
    [HttpPatch("series/{seriesId:guid}/cancel")]
    public async Task<IActionResult> CancelSeries(Guid seriesId)
    {
        try
        {
            var cancelledCount = await _appointments.CancelSeriesAsync(seriesId, _currentUser, HttpContext.RequestAborted);
            return Ok(new { cancelledCount });
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
    }

    private async Task<IActionResult> TryTransition(Func<Task<Appointment>> transition)
    {
        try
        {
            var appointment = await transition();
            return Ok(ToDto(appointment));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    /// <summary>409 with the structured rule list, so the calendar can show
    /// each violation and offer Override only when CanOverride is true.</summary>
    private ConflictObjectResult SchedulingConflict(SchedulingConflictException ex) =>
        Conflict(new { detail = ex.Message, violations = ex.Violations, canOverride = ex.CanOverride });

    private static AppointmentDto ToDto(Appointment a) => new(
        a.Id, a.PatientId, a.TherapistId, a.ProviderId, a.Kind, a.Status,
        a.StartsAt, a.EndsAt, a.LocationDetailId, a.RoomId, a.IsHomeVisit, a.ReasonForVisit,
        a.ConfirmationNumber, a.ConfirmedAt, a.SeriesId);

    private static AppointmentSeriesDto ToSeriesDto(AppointmentSeries s) => new(
        s.Id, s.IntervalWeeks, s.OccurrenceCount, s.IsActive,
        s.Occurrences.OrderBy(a => a.StartsAt).Select(ToDto).ToList());
}
