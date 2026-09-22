using System.Text.Json;
using PhysioTrac.Application.Sessions;
using PhysioTrac.Infrastructure.Identity;

namespace PhysioTrac.Api.Auth;

/// <summary>Runs after authentication, before authorization. Re-validates the
/// tracked <c>UserSession</c> behind the request's <c>sid</c> claim on every
/// call (idle/absolute timeout) and signs the caller out with a coded 401 —
/// JSON, not a redirect — mirroring the original `api_login_required`, which
/// deliberately returns a specific revocation reason instead of trusting a
/// stale cookie. A request with no <c>sid</c> claim (e.g. before login, or
/// the public booking surface) passes through untouched.</summary>
public class SessionValidationMiddleware
{
    private readonly RequestDelegate _next;

    public SessionValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ISessionService sessions)
    {
        var sessionKey = context.User.FindFirst(AppClaimTypes.SessionKey)?.Value;
        if (context.User.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(sessionKey))
        {
            var session = await sessions.ValidateAndTouchAsync(sessionKey, context.RequestAborted);
            if (session is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    detail = "Your session has ended. Please sign in again.",
                    code = "SESSION_EXPIRED",
                }));
                return;
            }
        }

        await _next(context);
    }
}
