using Microsoft.AspNetCore.Identity;
using PhysioTrac.Api.Auth;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Configuration;
using PhysioTrac.Infrastructure;
using PhysioTrac.Infrastructure.Identity;
using PhysioTrac.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "Frontend";

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

builder.Services.AddAuthorization();

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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseMiddleware<SessionValidationMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
