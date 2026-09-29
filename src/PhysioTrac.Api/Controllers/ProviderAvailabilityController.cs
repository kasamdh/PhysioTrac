using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Scheduling;

namespace PhysioTrac.Api.Controllers;

/// <summary>A provider's weekly working hours and time off. Reading is open
/// to all staff; changes need RoleSets.AvailabilityManagement.</summary>
[ApiController]
[Route("api/v1/providers/{providerId:guid}")]
[Authorize]
public class ProviderAvailabilityController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IProviderAvailabilityService _availability;

    public ProviderAvailabilityController(ICurrentUser currentUser, IProviderAvailabilityService availability)
    {
        _currentUser = currentUser;
        _availability = availability;
    }

    [HttpGet("schedule")]
    public Task<IActionResult> Get(Guid providerId) =>
        Run(async () => Ok(await _availability.GetAsync(providerId, _currentUser, HttpContext.RequestAborted)));

    /// <summary>Replaces the whole weekly pattern.</summary>
    [HttpPut("weekly-hours")]
    public Task<IActionResult> ReplaceWeeklyHours(Guid providerId, [FromBody] ReplaceWeeklyHoursRequest request) =>
        Run(async () => Ok(await _availability.ReplaceWeeklyHoursAsync(providerId, request, _currentUser, HttpContext.RequestAborted)));

    /// <summary>Adds time off; the response lists existing appointments that
    /// now fall inside it (they are not moved automatically).</summary>
    [HttpPost("time-off")]
    public Task<IActionResult> CreateTimeOff(Guid providerId, [FromBody] CreateTimeOffRequest request) =>
        Run(async () => Ok(await _availability.CreateTimeOffAsync(providerId, request, _currentUser, HttpContext.RequestAborted)));

    [HttpDelete("time-off/{timeOffId:guid}")]
    public Task<IActionResult> CancelTimeOff(Guid providerId, Guid timeOffId) =>
        Run(async () =>
        {
            await _availability.CancelTimeOffAsync(providerId, timeOffId, _currentUser, HttpContext.RequestAborted);
            return NoContent();
        });

    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { detail = ex.Message }); }
    }
}
