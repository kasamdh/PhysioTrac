using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace PhysioTrac.Infrastructure.Identity;

/// <summary>Adds the org/role/super-admin claims every request needs, on top
/// of ASP.NET Identity's defaults, so <see cref="ClaimsCurrentUser"/> never
/// has to hit the database to answer "who is this and what can they see."
/// The dynamic per-login session-key claim (<see cref="AppClaimTypes.SessionKey"/>)
/// is added separately at sign-in time, not here (this factory's output is
/// cached across the lifetime of one login). Shared by both the JSON API
/// and the Blazor Server UI, so a login through either host produces an
/// identical claim set.</summary>
public class AppUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>
{
    public AppUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, roleManager, optionsAccessor)
    {
    }

    public override async Task<ClaimsPrincipal> CreateAsync(ApplicationUser user)
    {
        var principal = await base.CreateAsync(user);
        var identity = (ClaimsIdentity)principal.Identity!;

        identity.AddClaim(new Claim(AppClaimTypes.Role, user.Role.ToString()));
        identity.AddClaim(new Claim(AppClaimTypes.IsPlatformSuperAdmin, user.IsPlatformSuperAdmin.ToString()));
        if (user.OrganizationId is not null)
        {
            identity.AddClaim(new Claim(AppClaimTypes.OrganizationId, user.OrganizationId.Value.ToString()));
        }

        return principal;
    }
}
