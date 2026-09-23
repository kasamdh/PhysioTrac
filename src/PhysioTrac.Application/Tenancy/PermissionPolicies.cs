namespace PhysioTrac.Application.Tenancy;

/// <summary>ASP.NET Core authorization policy names, one per <see cref="RoleSets"/>
/// grouping. These are a declarative, attribute-level restatement of the
/// same role sets `ITenantAccessService.RequireRole` already enforces inside
/// action bodies -- not a replacement. Applying `[Authorize(Policy = ...)]`
/// alongside an existing `RequireRole` call is defense in depth (the
/// request never even reaches the action if the policy fails), the same
/// spirit as the FluentValidation filter sitting in front of service-layer
/// validation.</summary>
public static class PermissionPolicies
{
    public const string Clinical = "Clinical";
    public const string Scheduling = "Scheduling";
    public const string Billing = "Billing";
    public const string PaymentCollection = "PaymentCollection";
    public const string DocumentManagement = "DocumentManagement";
}
