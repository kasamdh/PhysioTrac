using PhysioTrac.Application.Tenancy;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Api.Contracts;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Sessions;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Identity;
using PhysioTrac.Infrastructure.Persistence;

namespace PhysioTrac.Api.Controllers;

/// <remarks>The strict Auth rate limit is on the password-guessing actions
/// only (login, invitation activation, change password). /me, /csrf and
/// logout run on every page load and are covered by the global limiter alone
/// -- under the strict one, a clinic sharing one public IP was bounced to the
/// login screen after ~10 page loads a minute across all its staff.</remarks>
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISessionService _sessions;
    private readonly IAuditService _audit;
    private readonly PhysioTracDbContext _db;

    public AuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ISessionService sessions,
        IAuditService audit,
        PhysioTracDbContext db)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _sessions = sessions;
        _audit = audit;
        _db = db;
    }

    /// <summary>Deliberately returns the same generic message for every
    /// failure reason (unknown user, bad password, locked, suspended) —
    /// anti-enumeration, matching the original login view.</summary>
    private static IActionResult InvalidCredentials() =>
        new UnauthorizedObjectResult(new { detail = "Invalid username or password." });

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var user = await _userManager.FindByNameAsync(request.Username)
            ?? await _userManager.FindByEmailAsync(request.Username);
        if (user is null)
        {
            // No audit event here -- there's no user/org to attribute it to,
            // and writing one keyed by the attempted username would let an
            // attacker use audit-log side effects to enumerate accounts.
            return InvalidCredentials();
        }

        var checkResult = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!checkResult.Succeeded)
        {
            await AuditAuthEventAsync(user, "auth.login.failed", ip, HttpContext.RequestAborted);
            return InvalidCredentials();
        }

        if (user.EffectiveStatus(DateTimeOffset.UtcNow) != UserStatus.Active)
        {
            await AuditAuthEventAsync(user, "auth.login.failed", ip, HttpContext.RequestAborted);
            return InvalidCredentials();
        }

        // Unlike the checks above, a suspended/cancelled organization isn't
        // an enumeration risk to disclose -- the account and password are
        // already confirmed correct, and the org's own staff already know
        // their subscription state. Mirrors the structured error
        // ActivateInvitation already returns for the same condition, so the
        // frontend has one shape to handle either way.
        if (user.OrganizationId is Guid organizationId)
        {
            var organization = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, HttpContext.RequestAborted);
            if (organization is not null && organization.Status is OrganizationStatus.Suspended or OrganizationStatus.Cancelled)
            {
                await AuditAuthEventAsync(user, "auth.login.failed", ip, HttpContext.RequestAborted);
                var code = organization.Status == OrganizationStatus.Suspended ? "ORGANIZATION_SUSPENDED" : "ORGANIZATION_CANCELLED";
                var reason = organization.Status == OrganizationStatus.Suspended
                    ? "Your organization account is currently suspended. Please contact your administrator."
                    : "Your organization's subscription has been cancelled. Please contact support to reactivate.";
                return StatusCode(403, new { timestamp = DateTimeOffset.UtcNow, status = 403, code, message = reason });
            }
        }

        var userAgent = Request.Headers.UserAgent.ToString();
        var session = await _sessions.CreateSessionAsync(user.Id, user.OrganizationId, user.Role, ip, userAgent, HttpContext.RequestAborted);

        await _signInManager.SignInWithClaimsAsync(user, isPersistent: false, additionalClaims: new[]
        {
            new Claim(AppClaimTypes.SessionKey, session.SessionKey),
        });

        await AuditAuthEventAsync(user, "auth.login.success", ip, HttpContext.RequestAborted);

        return Ok(await ToMeResponseAsync(user, session.SessionKey));
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

        var user = await _userManager.GetUserAsync(User);
        if (user is not null)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            await AuditAuthEventAsync(user, "auth.logout", ip, HttpContext.RequestAborted);
        }

        await _signInManager.SignOutAsync();
        return NoContent();
    }

    /// <summary>Platform accounts (SuperAdmin, OrganizationId null) have no
    /// tenant to attribute a login/logout event to, so they go through
    /// RecordPlatformAuditEventAsync instead -- everyone else's auth events
    /// are tenant-scoped like any other audit entry.</summary>
    private Task AuditAuthEventAsync(ApplicationUser user, string action, string? ipAddress, CancellationToken ct) =>
        user.OrganizationId is Guid organizationId
            ? _audit.RecordAuditEventAsync(user.Id, action, nameof(ApplicationUser), user.Id, organizationId, ipAddress: ipAddress, ct: ct)
            : _audit.RecordPlatformAuditEventAsync(user.Id, action, nameof(ApplicationUser), user.Id, ipAddress: ipAddress, ct: ct);

    /// <summary>Signed-in user changes their own password. Re-issues the
    /// auth cookie with the same session key afterwards: the password change
    /// rotates the security stamp, which would otherwise end this session at
    /// the next stamp validation.</summary>
    [HttpPost("change-password")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword))
        {
            return BadRequest(new { detail = "Current and new passwords are required." });
        }
        if (request.CurrentPassword == request.NewPassword)
        {
            return UnprocessableEntity(new { detail = "The new password must be different from the current one." });
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var wrongCurrent = result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch));
            return UnprocessableEntity(new
            {
                detail = wrongCurrent ? "The current password is incorrect." : "The new password doesn't meet the password rules.",
                errors = wrongCurrent ? Array.Empty<string>() : result.Errors.Select(e => e.Description).ToArray(),
            });
        }

        if (user.MustChangePassword)
        {
            user.MustChangePassword = false;
            await _userManager.UpdateAsync(user);
        }

        var sessionKey = User.FindFirst(AppClaimTypes.SessionKey)?.Value;
        if (!string.IsNullOrEmpty(sessionKey))
        {
            await _signInManager.SignInWithClaimsAsync(user, isPersistent: false, additionalClaims: new[]
            {
                new Claim(AppClaimTypes.SessionKey, sessionKey),
            });
        }

        await AuditAuthEventAsync(user, "auth.password.changed", HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        return Ok(await ToMeResponseAsync(user, User.FindFirst(AppClaimTypes.SessionKey)?.Value));
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
    [EnableRateLimiting(RateLimitPolicies.Auth)]
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
    [EnableRateLimiting(RateLimitPolicies.Auth)]
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

        // Deactivated (Suspended) after being invited: the link must not
        // bring the account back.
        if (user.Status == UserStatus.Suspended)
        {
            return StatusCode(403, new { detail = "This account has been deactivated. Please contact your administrator." });
        }

        var addPasswordResult = await _userManager.AddPasswordAsync(user, request.Password);
        if (!addPasswordResult.Succeeded)
        {
            return UnprocessableEntity(new { errors = addPasswordResult.Errors.Select(e => e.Description) });
        }

        // Staff invited through UsersController start Inactive (no password
        // yet); setting the first password is what makes them Active --
        // otherwise every later login would be refused.
        if (user.Status == UserStatus.Inactive)
        {
            user.Status = UserStatus.Active;
            user.StatusChangedAt = DateTimeOffset.UtcNow;
            await _userManager.UpdateAsync(user);
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

        return Ok(await ToMeResponseAsync(user, session.SessionKey));
    }

    private Task<PhysioTrac.Domain.Entities.ClientInvitation?> FindInvitationAsync(string token)
    {
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        return _db.ClientInvitations.Include(i => i.Organization).FirstOrDefaultAsync(i => i.TokenHash == tokenHash);
    }

    /// <summary>"Last login" is the start of the user's most recent session
    /// other than the current one -- i.e. the login before this one.</summary>
    private async Task<MeResponse> ToMeResponseAsync(ApplicationUser user, string? currentSessionKey)
    {
        var lastLoginAt = await _db.UserSessions
            .Where(s => s.UserId == user.Id && s.SessionKey != currentSessionKey)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => (DateTimeOffset?)s.CreatedAt)
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        return new MeResponse(
            user.Id, user.UserName ?? string.Empty, user.Email, user.Role,
            user.OrganizationId, user.IsPlatformSuperAdmin, user.MustChangePassword, AccessControl.Enabled,
            user.FirstName, user.LastName, lastLoginAt);
    }
}
