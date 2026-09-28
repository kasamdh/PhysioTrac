using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Api.Controllers;

/// <summary>Read-only queries behind the staff calendar. Creating, moving,
/// and status changes stay on AppointmentsController.</summary>
[ApiController]
[Route("api/v1/schedule")]
[Authorize]
public class ScheduleController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IScheduleService _schedule;

    public ScheduleController(ICurrentUser currentUser, IScheduleService schedule)
    {
        _currentUser = currentUser;
        _schedule = schedule;
    }

    /// <summary>Everything the calendar needs to draw its toolbar: timezone,
    /// slot size, filter options, and what this caller may do.</summary>
    [HttpGet("settings")]
    public Task<IActionResult> Settings() =>
        Run(() => _schedule.GetSettingsAsync(_currentUser, HttpContext.RequestAborted));

    /// <summary>Week/Month views (at most 62 days).</summary>
    [HttpGet("range")]
    public Task<IActionResult> Range(
        [FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to,
        [FromQuery] Guid? providerId = null, [FromQuery] Guid? locationId = null, [FromQuery] AppointmentStatus? status = null,
        [FromQuery] Guid? appointmentTypeId = null, [FromQuery] string? patient = null) =>
        Run(() => _schedule.GetRangeAsync(_currentUser,
            new ScheduleQuery(from, to, providerId, locationId, status, appointmentTypeId, patient), HttpContext.RequestAborted));

    /// <summary>Day view: provider columns, working hours, blocks, summaries.</summary>
    [HttpGet("day")]
    public Task<IActionResult> Day([FromQuery] DateOnly date, [FromQuery] Guid? locationId = null, [FromQuery] Guid? providerId = null) =>
        Run(() => _schedule.GetDayAsync(_currentUser, date, locationId, providerId, HttpContext.RequestAborted));

    /// <summary>Year view: appointment counts per clinic-local day (at most 400 days).</summary>
    [HttpGet("counts")]
    public Task<IActionResult> Counts(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] Guid? locationId = null, [FromQuery] Guid? providerId = null) =>
        Run(() => _schedule.GetCountsAsync(_currentUser, from, to, locationId, providerId, HttpContext.RequestAborted));

    /// <summary>List view, paginated (at most 400 days, 100 per page).</summary>
    [HttpGet("list")]
    public Task<IActionResult> List(
        [FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to,
        [FromQuery] Guid? providerId = null, [FromQuery] Guid? locationId = null, [FromQuery] AppointmentStatus? status = null,
        [FromQuery] Guid? appointmentTypeId = null, [FromQuery] string? patient = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        Run(() => _schedule.GetListAsync(_currentUser,
            new ScheduleQuery(from, to, providerId, locationId, status, appointmentTypeId, patient), page, pageSize, HttpContext.RequestAborted));

    /// <summary>Patient picker for booking: name, MRN, phone, or date of birth.</summary>
    [HttpGet("patients")]
    public Task<IActionResult> Patients([FromQuery] string q) =>
        Run(() => _schedule.SearchPatientsAsync(_currentUser, q, HttpContext.RequestAborted));

    private async Task<IActionResult> Run<T>(Func<Task<T>> query)
    {
        try
        {
            return Ok(await query());
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { detail = ex.Message }); }
    }
}
