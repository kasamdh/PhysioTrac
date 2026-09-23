using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using PhysioTrac.Api.Middleware;

namespace PhysioTrac.Tests;

/// <summary>The one guarantee this middleware exists for: an unhandled
/// exception never reaches the client as its own message or stack trace
/// (either could echo PHI -- a SQL error repeating a parameter value, a
/// NullReferenceException's object dump, etc.), only ever the fixed
/// generic detail string.</summary>
public class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task UnhandledException_ReturnsGeneric500_NeverTheExceptionMessage()
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("patient SSN 123-45-6789 caused a constraint violation"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(500, context.Response.StatusCode);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        using var json = JsonDocument.Parse(body);
        Assert.Equal("An unexpected error occurred.", json.RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain("SSN", body);
        Assert.DoesNotContain("constraint violation", body);
    }

    [Fact]
    public async Task NoException_PassesThroughUntouched()
    {
        var middleware = new ExceptionHandlingMiddleware(
            ctx =>
            {
                ctx.Response.StatusCode = 201;
                return Task.CompletedTask;
            },
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        await middleware.InvokeAsync(context);

        Assert.Equal(201, context.Response.StatusCode);
    }
}
