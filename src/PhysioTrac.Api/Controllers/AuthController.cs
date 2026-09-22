using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Api.Contracts;
using PhysioTrac.Application.Sessions;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Identity;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISessionService _sessions;
    private readonly PhysioTracDbContext _db;

    public AuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ISessionService sessions,
        PhysioTracDbContext db)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _sessions = sessions;
        _db = db;
    }

    /// <summary>Deliberately returns the same generic message for every
    /// failure reason (unknown user, bad password, locked, suspended) —
    /// anti-enumeration, matching the original login view.</summary>
    private static IActionResult InvalidCredentials() =>
        new UnauthorizedObjectResult(new { detail = "Invalid username or password." });

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByNameAsync(request.Username)
            ?? await _userManager.FindByEmailAsync(request.Username);
        if (user is null)
        {
            return InvalidCredentials();
        }

        var checkResult = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!checkResult.Succeeded)
        {
            return InvalidCredentials();
        }

        if (user.EffectiveStatus(DateTimeOffset.UtcNow) != UserStatus.Active)
        {
            return InvalidCredentials();
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var session = await _sessions.CreateSessionAsync(user.Id, user.OrganizationId, user.Role, ip, userAgent, HttpContext.RequestAborted);

        await _signInManager.SignInWithClaimsAsync(user, isPersistent: false, additionalClaims: new[]
        {
            new Claim(AppClaimTypes.SessionKey, session.SessionKey),
        });

        return Ok(ToMeResponse(user));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var sessionKey = User.FindFirst(AppClaimTypes.SessionKey)?.Value;
        if (!string.IsNullOrEmpty(sessionKey))
        {
            await _sessions.RevokeAsync(sessionKey, SessionRevokedReason.UserLogout, HttpContext.RequestAborted);
        }
        await _signInManager.SignOutAsync();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        return Ok(ToMeResponse(user));
    }

    /// <summary>Issues an antiforgery token pair the SPA re-sends as
    /// <c>X-CSRF-TOKEN</c> on every mutating request — the double-submit
    /// pattern the original relies on instead of a bearer token.</summary>
    [HttpGet("csrf")]
    [AllowAnonymous]
    public IActionResult Csrf([FromServices] Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { token = tokens.RequestToken });
    }

    /// <summary>Preview of a pending client-admin invitation, before the
    /// caller sets a password — read-only, no auth required.</summary>
    [HttpGet("activate-invitation")]
    [AllowAnonymous]
    public async Task<IActionResult> GetInvitation([FromQuery] string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return BadRequest(new { detail = "A valid invitation token is required." });
        }

        var invitation = await FindInvitationAsync(token);
        if (invitation is null || !invitation.IsUsable)
        {
            return StatusCode(410, new { detail = "This invitation is expired or has already been used." });
        }

        var user = await _userManager.FindByIdAsync(invitation.UserId.ToString());
        return Ok(new { organizationName = invitation.Organization!.Name, email = user?.Email });
    }

    /// <summary>Consumes a one-time invitation, sets the account's first
    /// real password (it was created with none — <c>set_unusable_password()</c>
    /// in the original), and signs the new administrator in immediately.</summary>
    [HttpPost("activate-invitation")]
    [AllowAnonymous]
    public async Task<IActionResult> ActivateInvitation([FromBody] ActivateInvitationRequest request)
    {
        if (string.IsNullOrEmpty(request.Token) || request.Password.Length < 12)
        {
            return BadRequest(new { detail = "A valid invitation token and password of at least 12 characters are required." });
        }

        var invitation = await FindInvitationAsync(request.Token);
        if (invitation is null || !invitation.IsUsable)
        {
            return StatusCode(410, new { detail = "This invitation is expired or has already been used." });
        }
        if (invitation.Organization!.Status != OrganizationStatus.Active)
        {
            return StatusCode(403, new
            {
                timestamp = DateTimeOffset.UtcNow,
                status = 403,
                code = "ORGANIZATION_SUSPENDED",
                message = "Your organization account is currently suspended. Please contact your administrator.",
            });
        }

        var user = await _userManager.FindByIdAsync(invitation.UserId.ToString());
        if (user is null)
        {
            return StatusCode(410, new { detail = "This invitation is expired or has already been used." });
        }

        var addPasswordResult = await _userManager.AddPasswordAsync(user, request.Password);
        if (!addPasswordResult.Succeeded)
        {
            return UnprocessableEntity(new { errors = addPasswordResult.Errors.Select(e => e.Description) });
        }

        invitation.UsedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(HttpContext.RequestAborted);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var session = await _sessions.CreateSessionAsync(user.Id, user.OrganizationId, user.Role, ip, userAgent, HttpContext.RequestAborted);
        await _signInManager.SignInWithClaimsAsync(user, isPersistent: false, additionalClaims: new[]
        {
            new Claim(AppClaimTypes.SessionKey, session.SessionKey),
        });

        return Ok(ToMeResponse(user));
    }

    private Task<PhysioTrac.Domain.Entities.ClientInvitation?> FindInvitationAsync(string token)
    {
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        return _db.ClientInvitations.Include(i => i.Organization).FirstOrDefaultAsync(i => i.TokenHash == tokenHash);
    }

    private static MeResponse ToMeResponse(ApplicationUser user) => new(
        user.Id, user.UserName ?? string.Empty, user.Email, user.Role,
        user.OrganizationId, user.IsPlatformSuperAdmin, user.MustChangePassword);
}
