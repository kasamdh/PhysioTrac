namespace PhysioTrac.Infrastructure.Identity;

public static class AppClaimTypes
{
    public const string OrganizationId = "org_id";
    public const string Role = "role";
    public const string IsPlatformSuperAdmin = "is_platform_super_admin";
    /// <summary>Correlates the auth cookie to a <c>UserSession</c> row.</summary>
    public const string SessionKey = "sid";
}
