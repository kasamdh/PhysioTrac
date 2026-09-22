using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhysioTrac.Api.Contracts;
using PhysioTrac.Application.Sessions;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Identity;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/auth/sessions")]
[Authorize]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessions;

    public SessionsController(ISessionService sessions)
    {
        _sessions = sessions;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    private string? CurrentSessionKey => User.FindFirst(AppClaimTypes.SessionKey)?.Value;

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var sessions = await _sessions.ListActiveForUserAsync(CurrentUserId, HttpContext.RequestAborted);
        var current = CurrentSessionKey;
        return Ok(sessions.Select(s => new UserSessionResponse(
            s.Id, s.CreatedAt, s.LastActivityAt, s.DeviceName, s.BrowserName, s.IpAddress,
            s.SessionKey == current)));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id)
    {
        var sessions = await _sessions.ListActiveForUserAsync(CurrentUserId, HttpContext.RequestAborted);
        var target = sessions.FirstOrDefault(s => s.Id == id);
        if (target is null) return NotFound();

        await _sessions.RevokeAsync(target.SessionKey, SessionRevokedReason.UserLogout, HttpContext.RequestAborted);
        return NoContent();
    }
}
