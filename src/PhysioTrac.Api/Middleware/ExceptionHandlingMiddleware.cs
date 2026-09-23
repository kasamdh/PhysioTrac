using System.Net;

namespace PhysioTrac.Api.Middleware;

/// <summary>Last-resort catch-all for exceptions that escape a controller's
/// own try/catch (ForbiddenException/NotFoundException are already handled
/// per-controller with the existing `{ detail }` shape -- this only catches
/// what's left, e.g. a genuine bug or an unexpected infrastructure failure).
/// The client always gets the same generic message: never the exception's
/// own message or stack trace, since either could contain PHI (a SQL error
/// echoing a parameter value, a NullReferenceException's object dump, etc).
/// Full details still go to Serilog server-side for diagnosis.</summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (!context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { detail = "An unexpected error occurred." });
        }
    }
}
