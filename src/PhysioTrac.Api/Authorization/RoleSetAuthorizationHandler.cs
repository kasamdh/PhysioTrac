using Microsoft.AspNetCore.Authorization;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Identity;

namespace PhysioTrac.Api.Authorization;

/// <summary>Reads the same "role" claim <see cref="PhysioTrac.Infrastructure.Identity.ClaimsCurrentUser"/>
/// reads for <c>ICurrentUser.Role</c>, so a policy check and an in-action
/// <c>RequireRole</c> check always agree -- both derive from the exact same
/// claim.</summary>
public class RoleSetAuthorizationHandler : AuthorizationHandler<RoleSetPolicyRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, RoleSetPolicyRequirement requirement)
    {
        var roleClaim = context.User.FindFirst(AppClaimTypes.Role)?.Value;
        if (Enum.TryParse<UserRole>(roleClaim, out var role) && requirement.AllowedRoles.Contains(role))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
