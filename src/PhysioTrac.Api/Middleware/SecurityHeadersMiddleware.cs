namespace PhysioTrac.Api.Middleware;

/// <summary>Adds the response headers every API endpoint should carry
/// regardless of what the action itself does -- defense-in-depth against
/// the frontend or a browser doing something this API never intends
/// (rendering API JSON as HTML, framing a response, leaking a referrer with
/// query-string tokens, etc). This is a pure JSON API with no server-
/// rendered HTML pages of its own (the Blazor Web app and the React SPA are
/// both separate origins), so the CSP below is deliberately locked down to
/// "nothing is allowed to render or execute here" rather than a page-
/// specific allowlist.</summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // No inline/external script, style, frame, or object can execute if
        // this response is somehow interpreted as HTML -- this API never
        // serves HTML itself, so there's no legitimate source to allowlist.
        headers.Append("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");

        // Stops a browser from guessing this JSON response is actually
        // HTML/JS and executing it (MIME-sniffing).
        headers.Append("X-Content-Type-Options", "nosniff");

        // Belt-and-suspenders alongside the CSP's frame-ancestors 'none'
        // above -- older browsers that don't honor CSP frame-ancestors
        // still respect this.
        headers.Append("X-Frame-Options", "DENY");

        // Never send the full referrer (which can carry a token/id in the
        // query string) to a cross-origin link; same-origin requests still
        // get the full path.
        headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

        // Explicitly opts this API out of every sensitive browser feature --
        // it never legitimately needs any of them, and a compromised
        // dependency in a page that embeds this API's responses shouldn't
        // inherit access by default.
        headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=()");

        return _next(context);
    }
}
