namespace PhysioTrac.Api;

/// <summary>Named RateLimiter policies registered in Program.cs, applied
/// via <c>[EnableRateLimiting(...)]</c> on the controllers the phase spec
/// calls out by name (authentication, patient portal) -- these stack with
/// (don't replace) the global per-IP limiter every other endpoint gets.</summary>
public static class RateLimitPolicies
{
    public const string Auth = "Auth";
    public const string Portal = "Portal";
}
