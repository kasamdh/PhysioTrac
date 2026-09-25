using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Dashboards;

namespace PhysioTrac.Api.Controllers;

/// <summary>Org-scoped dashboards -- see IDashboardService's own doc
/// comment for role/tenant-isolation rules. "Today's schedule" isn't here;
/// it's GET /api/v1/appointments?from=...&amp;to=... (already exists).</summary>
[ApiController]
[Route("api/v1/dashboards")]
[Authorize]
public class DashboardsController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IDashboardService _dashboards;

    public DashboardsController(ICurrentUser currentUser, IDashboardService dashboards)
    {
        _currentUser = currentUser;
        _dashboards = dashboards;
    }

    [HttpGet("new-patients")]
    public async Task<IActionResult> NewPatients([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        try
        {
            return Ok(await _dashboards.GetNewPatientsAsync(_currentUser, from, to, HttpContext.RequestAborted));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    [HttpGet("cancellations-no-shows")]
    public async Task<IActionResult> CancellationsAndNoShows(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] Guid? providerId = null, [FromQuery] Guid? locationId = null)
    {
        try
        {
            return Ok(await _dashboards.GetCancellationsAndNoShowsAsync(_currentUser, from, to, providerId, locationId, HttpContext.RequestAborted));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    [HttpGet("provider-productivity")]
    public async Task<IActionResult> ProviderProductivity([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        try
        {
            return Ok(await _dashboards.GetProviderProductivityAsync(_currentUser, from, to, HttpContext.RequestAborted));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    [HttpGet("visits-retention")]
    public async Task<IActionResult> VisitsAndRetention([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        try
        {
            return Ok(await _dashboards.GetVisitsAndRetentionAsync(_currentUser, from, to, HttpContext.RequestAborted));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    [HttpGet("referral-sources")]
    public async Task<IActionResult> ReferralSources([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        try
        {
            return Ok(await _dashboards.GetReferralSourcesAsync(_currentUser, from, to, HttpContext.RequestAborted));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }

    [HttpGet("location-performance")]
    public async Task<IActionResult> LocationPerformance([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        try
        {
            return Ok(await _dashboards.GetLocationPerformanceAsync(_currentUser, from, to, HttpContext.RequestAborted));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }
}
