using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using PhysioTrac.Api.Authorization;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Domain.Enums;
using PhysioTrac.Infrastructure.Identity;

namespace PhysioTrac.Tests;

/// <summary>The permission-policy layer must agree with RequireRole -- both
/// read the exact same "role" claim, so a role allowed by one is allowed by
/// the other and vice versa.</summary>
public class RoleSetAuthorizationHandlerTests
{
    private static async Task<bool> IsAllowedAsync(UserRole role, IReadOnlySet<UserRole> allowedRoles)
    {
        var identity = new ClaimsIdentity(new[] { new Claim(AppClaimTypes.Role, role.ToString()) }, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var requirement = new RoleSetPolicyRequirement(allowedRoles);
        var context = new AuthorizationHandlerContext(new[] { requirement }, principal, resource: null);

        await new RoleSetAuthorizationHandler().HandleAsync(context);

        return context.HasSucceeded;
    }

    [Fact]
    public async Task Scheduler_IsAllowed_BySchedulingPolicy()
    {
        Assert.True(await IsAllowedAsync(UserRole.Scheduler, RoleSets.Scheduling));
    }

    [Fact]
    public async Task Biller_IsNotAllowed_BySchedulingPolicy()
    {
        Assert.False(await IsAllowedAsync(UserRole.Biller, RoleSets.Scheduling));
    }

    [Fact]
    public async Task MissingRoleClaim_IsNeverAllowed()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var requirement = new RoleSetPolicyRequirement(RoleSets.Clinical);
        var context = new AuthorizationHandlerContext(new[] { requirement }, principal, resource: null);

        await new RoleSetAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    /// <summary>Every named RoleSet crossed with every UserRole -- the full
    /// authorization matrix behind every [Authorize(Policy = ...)] attribute
    /// in the API. Membership here must always match RoleSets' own
    /// definitions; a role added to a set without updating this data would
    /// fail loudly instead of silently under-testing the new grant.</summary>
    public static IEnumerable<object[]> RoleSetMatrix()
    {
        var namedSets = new Dictionary<string, IReadOnlySet<UserRole>>
        {
            ["Clinical"] = RoleSets.Clinical,
            ["Scheduling"] = RoleSets.Scheduling,
            ["Billing"] = RoleSets.Billing,
            ["PaymentCollection"] = RoleSets.PaymentCollection,
            ["DocumentManagement"] = RoleSets.DocumentManagement,
            ["OrganizationAdministration"] = RoleSets.OrganizationAdministration,
            ["AllStaff"] = RoleSets.AllStaff,
        };
        foreach (var (name, set) in namedSets)
        {
            foreach (var role in Enum.GetValues<UserRole>())
            {
                yield return new object[] { name, set, role, set.Contains(role) };
            }
        }
    }

    [Theory]
    [MemberData(nameof(RoleSetMatrix))]
    public async Task RoleSetMatrix_MatchesSetMembership(string setName, IReadOnlySet<UserRole> set, UserRole role, bool expectedAllowed)
    {
        var actualAllowed = await IsAllowedAsync(role, set);
        Assert.True(actualAllowed == expectedAllowed,
            $"{role} vs {setName}: expected allowed={expectedAllowed} but got {actualAllowed}");
    }
}
