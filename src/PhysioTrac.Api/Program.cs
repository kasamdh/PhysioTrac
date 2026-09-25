using System.Net;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PhysioTrac.Api;
using PhysioTrac.Api.Auth;
using PhysioTrac.Api.Authorization;
using PhysioTrac.Api.Filters;
using PhysioTrac.Api.Logging;
using PhysioTrac.Api.Middleware;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Configuration;
using PhysioTrac.Application.Patients;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Infrastructure;
using PhysioTrac.Infrastructure.Identity;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Seed;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "Frontend";

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Destructure.With<PhiRedactingDestructuringPolicy>()
    .Enrich.FromLogContext()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(context.HostingEnvironment.ContentRootPath, "logs", "physiotrac-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14));

builder.Services.AddControllers(options => options.Filters.Add<FluentValidationActionFilter>());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddValidatorsFromAssemblyContaining<CreatePatientRequestValidator>();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<ICurrentUser, ClaimsCurrentUser>();

var security = builder.Configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>() ?? new SecurityOptions();

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        // Mirrors config/settings.py: FAILED_LOGIN_LOCKOUT_THRESHOLD / LOCKOUT_DURATION_MINUTES.
        options.Lockout.MaxFailedAccessAttempts = security.FailedLoginLockoutThreshold;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(security.LockoutDurationMinutes);
        options.Lockout.AllowedForNewUsers = true;

        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 10;

        options.User.RequireUniqueEmail = false;

        // Let [Authorize(Roles="Admin")] read our custom "role" claim instead
        // of the ASP.NET-default role-claim type, since a user has exactly
        // one Role value (matching the source system), not a role collection.
        options.ClaimsIdentity.RoleClaimType = AppClaimTypes.Role;
    })
    .AddEntityFrameworkStores<PhysioTracDbContext>()
    .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(security.AbsoluteSessionHours);
    options.SlidingExpiration = false;

    // This is a JSON API, not a server-rendered app — never redirect to a
    // login page; return the same coded 401 the frontend's fetch client
    // already expects.
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync("""{"detail":"Authentication required."}""");
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync("""{"detail":"You are not permitted to perform this action."}""");
    };
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddSingleton<IAuthorizationHandler, RoleSetAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(PermissionPolicies.Clinical, policy => policy.Requirements.Add(new RoleSetPolicyRequirement(RoleSets.Clinical)));
    options.AddPolicy(PermissionPolicies.Scheduling, policy => policy.Requirements.Add(new RoleSetPolicyRequirement(RoleSets.Scheduling)));
    options.AddPolicy(PermissionPolicies.Billing, policy => policy.Requirements.Add(new RoleSetPolicyRequirement(RoleSets.Billing)));
    options.AddPolicy(PermissionPolicies.PaymentCollection, policy => policy.Requirements.Add(new RoleSetPolicyRequirement(RoleSets.PaymentCollection)));
    options.AddPolicy(PermissionPolicies.DocumentManagement, policy => policy.Requirements.Add(new RoleSetPolicyRequirement(RoleSets.DocumentManagement)));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Per-IP fixed-window limits, complementing (not replacing) the per-account
// failed-login lockout above -- see SecurityOptions' own doc comment on
// why both exist. RemoteIpAddress is used directly, not a forwarded-header
// value, since this API isn't known to sit behind a trusted proxy that
// sets one; a real deployment behind a load balancer/reverse proxy would
// need to configure ForwardedHeadersOptions first so this reads the real
// client IP instead of the proxy's.
static string ClientIpKey(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

builder.Services.AddRateLimiter(options =>
{
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { detail = "Too many requests. Please wait and try again." }, ct);
    };

    // Baseline for every endpoint, even ones with no [EnableRateLimiting]
    // attribute -- stacks with (doesn't replace) the stricter named
    // policies below on the specific controllers that use them.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(ClientIpKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = security.GlobalRateLimitPermitsPerWindow,
            Window = TimeSpan.FromSeconds(security.GlobalRateLimitWindowSeconds),
        }));

    options.AddPolicy(RateLimitPolicies.Auth, context =>
        RateLimitPartition.GetFixedWindowLimiter(ClientIpKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = security.AuthRateLimitPermitsPerWindow,
            Window = TimeSpan.FromSeconds(security.AuthRateLimitWindowSeconds),
        }));

    options.AddPolicy(RateLimitPolicies.Portal, context =>
        RateLimitPartition.GetFixedWindowLimiter(ClientIpKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = security.PortalRateLimitPermitsPerWindow,
            Window = TimeSpan.FromSeconds(security.PortalRateLimitWindowSeconds),
        }));
});

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseSerilogRequestLogging();

// HSTS tells the browser to upgrade to HTTPS on its own for MaxAge -- only
// meaningful, and only enabled, once there's a real certificate to enforce;
// in Development this API is reached over plain HTTP on localhost, where
// forcing HSTS would just make the browser cache a broken HTTPS upgrade for
// a port that was never serving TLS. UseHttpsRedirection() below still
// applies in every environment.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Applies pending migrations and seeds the demo dataset on startup.
// Development-only, deliberately: a production database is never
// auto-migrated or auto-seeded with known demo credentials as a side
// effect of the app starting. Kestrel doesn't start listening until this
// finishes, so /health only reports healthy once it's done -- Docker
// Compose's web service depends on that to know the schema is ready.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PhysioTracDbContext>();
    await db.Database.MigrateAsync();
    await DemoDataSeeder.SeedAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(FrontendCorsPolicy);
app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<SessionValidationMiddleware>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
app.MapControllers();

app.Run();

public partial class Program { }
