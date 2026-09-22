using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using PhysioTrac.Application.Auth;
using PhysioTrac.Application.Configuration;
using PhysioTrac.Infrastructure;
using PhysioTrac.Infrastructure.Identity;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Web.Components;
using PhysioTrac.Web.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ICurrentUser, ClaimsCurrentUser>();
// AddInteractiveServerComponents() already registers the built-in
// AuthenticationStateProvider that populates from HttpContext.User when a
// circuit connects; this just cascades it down the component tree.
builder.Services.AddCascadingAuthenticationState();

var security = builder.Configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>() ?? new SecurityOptions();

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        // Mirrors config/settings.py, same as the JSON API host.
        options.Lockout.MaxFailedAccessAttempts = security.FailedLoginLockoutThreshold;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(security.LockoutDurationMinutes);
        options.Lockout.AllowedForNewUsers = true;

        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 10;

        options.User.RequireUniqueEmail = false;
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

    // A server-rendered app, unlike the JSON API — a real redirect to the
    // login page is the correct UX here.
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/access-denied";
});

builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapAccountEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
