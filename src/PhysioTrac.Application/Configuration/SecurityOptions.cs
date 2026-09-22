namespace PhysioTrac.Application.Configuration;

/// <summary>Mirrors the security-relevant settings from the original
/// Django `config/settings.py` so behavior (lockout thresholds, session
/// limits, timeouts) carries over unchanged.</summary>
public class SecurityOptions
{
    public const string SectionName = "Security";

    public int FailedLoginLockoutThreshold { get; set; } = 5;
    public int LockoutDurationMinutes { get; set; } = 15;
    public int IdleTimeoutMinutes { get; set; } = 15;
    public int AbsoluteSessionHours { get; set; } = 12;

    /// <summary>Max concurrent devices per role; oldest session revoked
    /// first when exceeded.</summary>
    public Dictionary<string, int> SessionLimitsByRole { get; set; } = new()
    {
        ["SuperAdmin"] = 1,
        ["Admin"] = 1,
        ["Biller"] = 1,
        ["Scheduler"] = 1,
        ["Director"] = 2,
        ["Therapist"] = 2,
        ["Assistant"] = 2,
        ["Compliance"] = 2,
        ["Patient"] = 3,
    };
}
