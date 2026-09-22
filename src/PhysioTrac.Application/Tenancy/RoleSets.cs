using PhysioTrac.Domain.Enums;

namespace PhysioTrac.Application.Tenancy;

/// <summary>Named role groups shared across services/controllers — mirrors
/// the constants at the top of the original `care/access.py`.</summary>
public static class RoleSets
{
    public static readonly IReadOnlySet<UserRole> Clinical = new HashSet<UserRole>
    {
        UserRole.Admin, UserRole.Director, UserRole.Therapist, UserRole.Assistant, UserRole.Compliance
    };

    public static readonly IReadOnlySet<UserRole> Scheduling = new HashSet<UserRole>
    {
        UserRole.Admin, UserRole.Director, UserRole.Therapist, UserRole.Assistant, UserRole.Scheduler
    };

    public static readonly IReadOnlySet<UserRole> Billing = new HashSet<UserRole>
    {
        UserRole.Admin, UserRole.Biller, UserRole.Director
    };

    /// <summary>Front desk collects payments (e.g. a copay at check-in)
    /// without the full billing visibility <see cref="Billing"/> grants.</summary>
    public static readonly IReadOnlySet<UserRole> PaymentCollection = new HashSet<UserRole>(Billing)
    {
        UserRole.Scheduler
    };
}
