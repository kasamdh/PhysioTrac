using Microsoft.AspNetCore.Authorization;
using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Api.Authorization;

/// <summary>An ASP.NET Core authorization requirement satisfied by any of a
/// fixed set of roles -- the policy-system counterpart of the role sets in
/// <c>PhysioTrac.Application.Tenancy.RoleSets</c>.</summary>
public class RoleSetPolicyRequirement : IAuthorizationRequirement
{
    public IReadOnlySet<UserRole> AllowedRoles { get; }

    public RoleSetPolicyRequirement(IReadOnlySet<UserRole> allowedRoles)
    {
        AllowedRoles = allowedRoles;
    }
}
