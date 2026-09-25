using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Common;
using PhysioTrac.Application.Dashboards;

namespace PhysioTrac.Api.Controllers;

/// <summary>Cross-tenant organization/location performance for the
/// platform Super Admin workspace.</summary>
[ApiController]
[Route("api/v1/super-admin/dashboards")]
[Authorize]
public class SuperAdminDashboardController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IPlatformDashboardService _dashboards;

    public SuperAdminDashboardController(ICurrentUser currentUser, IPlatformDashboardService dashboards)
    {
        _currentUser = currentUser;
        _dashboards = dashboards;
    }

    [HttpGet("organization-performance")]
    public async Task<IActionResult> OrganizationPerformance([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        try
        {
            return Ok(await _dashboards.GetOrganizationPerformanceAsync(_currentUser, from, to, HttpContext.RequestAborted));
        }
        catch (ForbiddenException ex) { return StatusCode(403, new { detail = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { detail = ex.Message }); }
    }
}
