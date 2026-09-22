using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using PhysioTrac.Application.Sessions;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Identity;

namespace PhysioTrac.Web.Endpoints;

/// <summary>Plain (non-interactive) HTTP endpoints for sign-in/out. An
/// interactive Blazor Server component runs over a persistent SignalR
/// connection and can't set an HttpOnly auth cookie itself — the login page
/// posts a regular HTML form here instead, matching the pattern the
/// official ASP.NET Core Identity + Blazor template uses.</summary>
public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        app.MapPost("/account/login", async (
            HttpContext http,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ISessionService sessions) =>
        {
            var form = await http.Request.ReadFormAsync();
            var username = form["username"].ToString();
            var password = form["password"].ToString();
            var returnUrl = form["returnUrl"].ToString();
            if (string.IsNullOrEmpty(returnUrl)) returnUrl = "/";

            var user = await userManager.FindByNameAsync(username) ?? await userManager.FindByEmailAsync(username);
            // Deliberately the same generic failure redirect regardless of
            // reason (unknown user, bad password, locked, suspended) —
            // anti-enumeration, matching the JSON API's login endpoint.
            if (user is null)
            {
                return Results.Redirect($"/login?error=1&returnUrl={Uri.EscapeDataString(returnUrl)}");
            }

            var checkResult = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
            if (!checkResult.Succeeded || user.EffectiveStatus(DateTimeOffset.UtcNow) != UserStatus.Active)
            {
                return Results.Redirect($"/login?error=1&returnUrl={Uri.EscapeDataString(returnUrl)}");
            }

            var ip = http.Connection.RemoteIpAddress?.ToString();
            var userAgent = http.Request.Headers.UserAgent.ToString();
            var session = await sessions.CreateSessionAsync(user.Id, user.OrganizationId, user.Role, ip, userAgent, http.RequestAborted);

            await signInManager.SignInWithClaimsAsync(user, isPersistent: false, additionalClaims: new[]
            {
                new Claim(AppClaimTypes.SessionKey, session.SessionKey),
            });

            return Results.Redirect(returnUrl);
        });

        app.MapPost("/account/logout", async (
            HttpContext http,
            SignInManager<ApplicationUser> signInManager,
            ISessionService sessions) =>
        {
            var sessionKey = http.User.FindFirst(AppClaimTypes.SessionKey)?.Value;
            if (!string.IsNullOrEmpty(sessionKey))
            {
                await sessions.RevokeAsync(sessionKey, SessionRevokedReason.UserLogout, http.RequestAborted);
            }
            await signInManager.SignOutAsync();
            return Results.Redirect("/login");
        });
    }
}
